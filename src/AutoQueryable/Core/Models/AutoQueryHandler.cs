﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using AutoQueryable.Core.Clauses;
using AutoQueryable.Core.CriteriaFilters;
using AutoQueryable.Core.Enums;
using AutoQueryable.Helpers;
using AutoQueryable.Models;

namespace AutoQueryable.Core.Models
{
    public class AutoQueryHandler : IAutoQueryHandler
    {
        private readonly ICriteriaFilterManager _criteriaFilterManager;
        private readonly IClauseMapManager _clauseMapManager;
        private readonly IAutoQueryableProfile _profile;
        private readonly StaticMappingDescription _staticMappingDescription;

        public AutoQueryHandler(ICriteriaFilterManager criteriaFilterManager, IClauseMapManager clauseMapManager, IAutoQueryableProfile profile,
            StaticMappingDescription staticMappingDescription = null)
        {
            _criteriaFilterManager = criteriaFilterManager;
            _clauseMapManager = clauseMapManager;
            _profile = profile;
            _staticMappingDescription = staticMappingDescription;
        }

        public dynamic GetAutoQuery<T>(IQueryable<T> query, IQueryStringAccessor queryStringAccessor, IClauseValueManager clauseValueManager) where T : class
        {
            var queryString = queryStringAccessor.QueryString;
            // Reset the TotalCountQuery
            IQueryable<dynamic> totalCountQuery = null;

            // No query string, get only selectable columns
            if (string.IsNullOrEmpty(queryString))
            {
                clauseValueManager.SetDefaults(typeof(T));
                totalCountQuery = query;
                return GetDefaultSelectableQuery(query, clauseValueManager);
            }

            GetClauses<T>(queryStringAccessor, clauseValueManager);
            var criterias = _profile.IsClauseAllowed(ClauseType.Filter) ? GetCriterias<T>(queryStringAccessor).ToList() : null;

            query = QueryBuilder.AddCriterias(query, criterias, _criteriaFilterManager);
            totalCountQuery = query;
            var queryResult = QueryBuilder.Build(clauseValueManager, query, _profile);

 
            return queryResult;
        }
        
        private void GetClauses<T>(IQueryStringAccessor queryStringAccessor, IClauseValueManager clauseValueManager) where T : class
        {
            _clauseMapManager.Init();
            foreach (var q in queryStringAccessor.QueryStringParts.Where(q => !q.IsHandled))
            {
                var clauseQueryFilter = _clauseMapManager.FindClauseQueryFilter(q.Value);
                if(clauseQueryFilter != null)
                {
                    var operandValue = _getOperandValue(q.Value, clauseQueryFilter.Alias);
                    var value = clauseQueryFilter.ParseValue(operandValue, typeof(T), _profile);
                    var propertyInfo = clauseValueManager.GetType().GetProperty(clauseQueryFilter.ClauseType.ToString());
                    if(propertyInfo.PropertyType == typeof(bool))
                    {
                        value = bool.Parse(value.ToString());
                    }

                    propertyInfo.SetValue(clauseValueManager, value);
                }
            }
            // Set the defaults to start with, then fill/overwrite with the query string values
            clauseValueManager.SetDefaults(typeof(T));

            if (clauseValueManager.PageSize != null)
            {
                clauseValueManager.Top = clauseValueManager.PageSize;
            }

            if (clauseValueManager.Page != null)
            {
                //this.Logger.Information("Overwriting 'skip' clause value because 'page' is set");
                // Calculate skip from page if page query param was set
                clauseValueManager.Top = clauseValueManager.Top ?? _profile.DefaultToTake;
                clauseValueManager.Skip = (clauseValueManager.Page - 1) * clauseValueManager.Top;
            }



            if (clauseValueManager.OrderBy == null && _profile.DefaultOrderBy != null)
            {
                clauseValueManager.OrderBy = _profile.DefaultOrderBy;
            }

            if (clauseValueManager.Select.Count == 0)
            {
                _clauseMapManager.GetClauseQueryFilter(ClauseType.Select).ParseValue("", typeof(T), _profile);
            }
        }
        private string _getOperandValue(string q, string clauseAlias) => Regex.Split(q, clauseAlias, RegexOptions.IgnoreCase)[1];

        public IEnumerable<Criteria> GetCriterias<T>(IQueryStringAccessor queryStringAccessor) where T : class
        {
            foreach (var qPart in queryStringAccessor.QueryStringParts.Where(q => !q.IsHandled))
            {
                var q = WebUtility.UrlDecode(qPart.Value);
                
                int subIndex = q.IndexOf('=');
                if (subIndex == -1)
                {
                    subIndex = q.IndexOf('>');
                }
                if (subIndex == -1)
                {
                    subIndex = q.IndexOf('<');
                }
                List<string> orEntries = q.Substring(0, subIndex).Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(s => s + q.Substring(subIndex, q.Length - subIndex)).ToList();
                if (orEntries.Count <= 1)
                {
                    var criteria = GetCriteria<T>(orEntries[0].Trim());
                    if (criteria != null)
                    {
                        yield return criteria;
                    }
                    continue;
                }
                
                List<Criteria> criterias = new List<Criteria>();
                for (int i = 0; i < orEntries.Count; i++)
                {
                    var criteria = GetCriteria<T>(orEntries[i].Trim());
                    if (i > 0)
                    {
                        criteria.Or = true;
                    }
                    if (criteria != null)
                    {
                        criterias.Add(criteria);
                    }
                }
                if (criterias.Count == 0) continue;
                yield return new Criteria
                {
                    Criterias = criterias
                };
            }
        }

        private static bool TryMapStatic(StaticMappingDescription staticMappings
            , Type targetType, string requestedProperty, out string classPropertyName, out Type newType)
        {
            classPropertyName = null;
            newType = targetType;
            
            if (staticMappings == null || staticMappings.Count == 0)
                return false;

            var targetTypeStr = targetType.FullName.ToLowerInvariant();

            if (!staticMappings.TryGetValue(targetTypeStr, out var propertyDescriptions))
                return false;

            if (!propertyDescriptions.TryGetValue(requestedProperty, out var propertyDescription))
                return false;

            classPropertyName = propertyDescription.ClassPropertyName;
            newType = propertyDescription.Type;
            
            return true;
        }
        
        private Criteria GetCriteria<T>(string q) where T : class
        {
            var filter = _criteriaFilterManager.FindFilter(q);
            if (filter == null)
            {
                return null;
            }

            var operands = Regex.Split(q, filter.Alias, RegexOptions.IgnoreCase);

            PropertyInfo property = null;
            var columnPath = new List<string>();
            var columns = operands[0].Split('.');
            
            var typeInfo = typeof(T);
            
            foreach (var column in columns)
            {
                if(TryMapStatic(_staticMappingDescription, typeInfo, column, out var classPropertyName, out var newType))
                {
                    columnPath.Add(classPropertyName);
                    typeInfo = newType;
                    
                    continue;
                }
                
                if (property == null)
                {
                    property = typeof(T).GetProperties().FirstOrDefault(p => p.Name.Equals(column, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    var isCollection = property.PropertyType.GetInterfaces().Any(x => x.Name == "IEnumerable");
                    if (isCollection)
                    {
                        var childType = property.PropertyType.GetGenericArguments()[0];
                        property = childType.GetProperties().FirstOrDefault(p => p.Name.Equals(column, StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        property = property.PropertyType.GetProperties().FirstOrDefault(p => p.Name.Equals(column, StringComparison.OrdinalIgnoreCase));

                    }
                }

                if (property == null)
                {
                    return null;
                }
                columnPath.Add(property.Name);
            }
            var criteria = new Criteria
            {
                ColumnPath = columnPath,
                Filter = filter,
                Values = operands[1].Split(',')
            };
            return criteria;
        }

        private IQueryable<dynamic> GetDefaultSelectableQuery<T>(IQueryable<T> query, IClauseValueManager clauseValueManager) where T : class
        {
            var selectColumns = clauseValueManager.Select;
            query = query.Take(_profile.DefaultToTake);

            if (_profile.MaxToTake.HasValue)
            {
                query = query.Take(_profile.MaxToTake.Value);
            }

            if(_profile.ToListBeforeSelect)
            {
                query = query.ToList().AsQueryable();
            }

            return _profile.UseBaseType ? query.Select(SelectHelper.GetSelector<T, T>(selectColumns, _profile)) : query.Select(SelectHelper.GetSelector<T, object>(selectColumns, _profile));
        }
    }
}