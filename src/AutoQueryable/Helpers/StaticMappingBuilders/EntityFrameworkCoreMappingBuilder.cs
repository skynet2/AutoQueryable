using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoQueryable.Core.Extensions;
using AutoQueryable.Models;

namespace AutoQueryable.Helpers.StaticMappingBuilders
{
    public class EntityFrameworkCoreMappingBuilder : IMappingBuilder
    {
        public StaticMappingDescription Build(StaticMappingConfiguration configuration, params object[] items)
        {
            if (items == null)
                return null;

            var combinedResult = new StaticMappingDescription();

            foreach (var dbContext in items)
            {
                var buildResult = BuildInternal(configuration, dbContext);

                if (buildResult == null)
                    continue;

                foreach (var kv in buildResult)
                {
                    combinedResult[kv.Key] = kv.Value;
                }
            }

            return combinedResult;
        }

        private static StaticMappingDescription BuildInternal(StaticMappingConfiguration configuration,
            object dbContext)
        {
            var result = new StaticMappingDescription();

            var modelData = dbContext.GetType().GetProperty("Model")?.GetValue(dbContext);

            if (modelData == null)
                return result;

            var entityTypes = (IDictionary) modelData.GetType()
                .GetField("_entityTypes", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)?
                .GetValue(modelData);

            if (entityTypes == null)
                return result;

            foreach (DictionaryEntry kv in entityTypes)
            {
                var normalizedTypeName = kv.Key.ToString().ToLowerInvariant();
                var typePropDict = new Dictionary<string, PropertyDescription>();

                result[normalizedTypeName] = typePropDict;

                var props = kv.Value.GetType().GetField("_properties",
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)?
                    .GetValue(kv.Value) as IDictionary;

                if (props == null)
                    continue;


                string ToSnakeCase(string input)
                {
                    return string
                        .Concat(input.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString()))
                        .ToLower();
                }

                void AddMap(string fieldName, PropertyInfo classPropInfo)
                {
                    typePropDict[fieldName] = new PropertyDescription
                    {
                        ClassPropertyName = classPropInfo.Name,
                        IsEnumerable = classPropInfo.PropertyType.IsEnumerable(),
                        Type = classPropInfo.PropertyType
                    };
                }

                var navigators = kv.Value.GetType().GetField("_navigations",
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)?
                    .GetValue(kv.Value) as IDictionary;

                if (navigators != null && navigators.Count > 0)
                {
                    foreach (DictionaryEntry nav in navigators)
                    {
                        AddMap(configuration.UseSnakeCaseForNavigationProperties ? ToSnakeCase(nav.Key.ToString()) : nav.Key.ToString(),
                            (PropertyInfo) nav.Value.GetType().GetProperty("PropertyInfo").GetValue(nav.Value));
                    }
                }

                foreach (DictionaryEntry prop in props)
                {
                    var resultPropName = prop.Key.ToString();
                    var propInfo = (PropertyInfo) prop.Value.GetType().GetProperty("PropertyInfo").GetValue(prop.Value);

                    var getAnnotationsMethodRef = prop.Value.GetType()
                        .GetMethod("GetAnnotations",
                            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

                    if (getAnnotationsMethodRef == null)
                    {
                        AddMap(resultPropName, propInfo);
                        continue;
                    }

                    var annotationsMethodResult = getAnnotationsMethodRef.Invoke(prop.Value, null) as IEnumerable;

                    if (annotationsMethodResult == null)
                    {
                        AddMap(resultPropName, propInfo);
                        continue;
                    }

                    foreach (var anotation in annotationsMethodResult)
                    {
                        var name = anotation.GetType().GetProperty("Name").GetValue(anotation)?.ToString();

                        if (name == "Relational:ColumnName")
                        {
                            resultPropName = anotation.GetType().GetProperty("Value").GetValue(anotation).ToString();
                        }

                        AddMap(resultPropName, propInfo);
                    }
                }
            }

            return result;
        }
    }
}