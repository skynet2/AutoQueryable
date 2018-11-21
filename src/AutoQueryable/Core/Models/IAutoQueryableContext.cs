using System.Linq;
using AutoQueryable.Core.Clauses;

namespace AutoQueryable.Core.Models
{
    public interface IAutoQueryableContext
    {
        dynamic GetAutoQuery<T>(IQueryable<T> query) where T : class;
        dynamic GetAutoQuery<T>(IQueryable<T> query,string queryString) where T : class;
    }

}