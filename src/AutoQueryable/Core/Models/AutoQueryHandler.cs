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
        private readonly IQueryStringAccessor _queryStringAccessor;
        private readonly ICriteriaFilterManager _criteriaFilterManager;
        private readonly IClauseMapManager _clauseMapManager;
        private readonly IClauseValueManager _clauseValueManager;
        private readonly IAutoQueryableProfile _profile;
        private readonly StaticMappingDescription _staticMappingDescription;
        public IClauseValueManager ClauseValueManager { get; private set; }
        public IQueryable<dynamic> TotalCountQuery { get; private set; }
        public string QueryString { get; private set; }


        public AutoQueryHandler(IQueryStringAccessor queryStringAccessor, ICriteriaFilterManager criteriaFilterManager, IClauseMapManager clauseMapManager, IClauseValueManager clauseValueManager, IAutoQueryableProfile profile,
            StaticMappingDescription staticMappingDescription = null)
        {
            _queryStringAccessor = queryStringAccessor;
            _criteriaFilterManager = criteriaFilterManager;
            _clauseMapManager = clauseMapManager;
            _clauseValueManager = clauseValueManager;
            _profile = profile;
            _staticMappingDescription = staticMappingDescription;
        }

        public dynamic GetAutoQuery<T>(IQueryable<T> query) where T : class
        {
            QueryString = _queryStringAccessor.QueryString;
            ClauseValueManager = _clauseValueManager;
            // Reset the TotalCountQuery
            TotalCountQuery = null;

            // No query string, get only selectable columns
            if (string.IsNullOrEmpty(QueryString))
            {
                _clauseValueManager.SetDefaults(typeof(T));
                TotalCountQuery = query;
                return GetDefaultSelectableQuery(query);
            }

            GetClauses<T>();
            var criterias = _profile.IsClauseAllowed(ClauseType.Filter) ? GetCriterias<T>().ToList() : null;

            query = QueryBuilder.AddCriterias(query, criterias, _criteriaFilterManager);
            TotalCountQuery = query;
            var queryResult = QueryBuilder.Build(ClauseValueManager, query, _profile);

 
            return queryResult;
        }
        
        private void GetClauses<T>() where T : class
        {
            _clauseMapManager.Init();
            foreach (var q in _queryStringAccessor.QueryStringParts.Where(q => !q.IsHandled))
            {
                var clauseQueryFilter = _clauseMapManager.FindClauseQueryFilter(q.Value);
                if(clauseQueryFilter != null)
                {
                    var operandValue = _getOperandValue(q.Value, clauseQueryFilter.Alias);
                    var value = clauseQueryFilter.ParseValue(operandValue, typeof(T), _profile);
                    var propertyInfo = ClauseValueManager.GetType().GetProperty(clauseQueryFilter.ClauseType.ToString());
                    if(propertyInfo.PropertyType == typeof(bool))
                    {
                        value = bool.Parse(value.ToString());
                    }

                    propertyInfo.SetValue(ClauseValueManager, value);
                }
            }
            // Set the defaults to start with, then fill/overwrite with the query string values
            ClauseValueManager.SetDefaults(typeof(T));

            if (ClauseValueManager.PageSize != null)
            {
                ClauseValueManager.Top = ClauseValueManager.PageSize;
            }

            if (ClauseValueManager.Page != null)
            {
                //this.Logger.Information("Overwriting 'skip' clause value because 'page' is set");
                // Calculate skip from page if page query param was set
                ClauseValueManager.Top = ClauseValueManager.Top ?? _profile.DefaultToTake;
                ClauseValueManager.Skip = (ClauseValueManager.Page - 1) * ClauseValueManager.Top;
            }



            if (ClauseValueManager.OrderBy == null && _profile.DefaultOrderBy != null)
            {
                ClauseValueManager.OrderBy = _profile.DefaultOrderBy;
            }

            if (ClauseValueManager.Select.Count == 0)
            {
                _clauseMapManager.GetClauseQueryFilter(ClauseType.Select).ParseValue("", typeof(T), _profile);
            }
        }
        private string _getOperandValue(string q, string clauseAlias) => Regex.Split(q, clauseAlias, RegexOptions.IgnoreCase)[1];

        public IEnumerable<Criteria> GetCriterias<T>() where T : class
        {
            foreach (var qPart in _queryStringAccessor.QueryStringParts.Where(q => !q.IsHandled))
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

        private IQueryable<dynamic> GetDefaultSelectableQuery<T>(IQueryable<T> query) where T : class
        {
            var selectColumns = _clauseValueManager.Select;
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