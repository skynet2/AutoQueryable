using System.Linq;
using AutoQueryable.Core.Clauses;

namespace AutoQueryable.Core.Models
{
    public interface IAutoQueryHandler
    {
        dynamic GetAutoQuery<T>(IQueryable<T> query, IQueryStringAccessor queryStringAccessor,
            IClauseValueManager clauseValueManager) where T : class;
    }
}