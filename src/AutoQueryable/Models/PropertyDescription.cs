using System;
using System.Collections.Generic;

namespace AutoQueryable.Models
{
    public class StaticMappingDescription : Dictionary<string, Dictionary<string, PropertyDescription>>
    {
    }

    public class PropertyDescription
    {
        public string ClassPropertyName { get; set; }
        public Type Type { get; set; }
        public bool IsEnumerable { get; set; }
    }
}