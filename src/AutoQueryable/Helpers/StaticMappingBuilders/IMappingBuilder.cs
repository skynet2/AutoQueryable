using AutoQueryable.Models;

namespace AutoQueryable.Helpers.StaticMappingBuilders
{
    public interface IMappingBuilder
    {
        StaticMappingDescription Build(StaticMappingConfiguration configuration, params object[] items);
    }
}