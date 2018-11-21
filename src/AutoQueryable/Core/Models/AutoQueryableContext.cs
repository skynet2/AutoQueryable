using System.Linq;
using AutoQueryable.Core.Clauses;
using AutoQueryable.Core.Clauses.ClauseHandlers;

namespace AutoQueryable.Core.Models
{
    public class AutoQueryableContext : IAutoQueryableContext
    {
        private readonly IAutoQueryHandler _autoQueryHandler;
        private readonly IQueryStringAccessor _queryStringAccessor;
        private readonly ISelectClauseHandler _selectClauseHandler;
        private readonly IOrderByClauseHandler _orderByClauseHandler;
        private readonly IWrapWithClauseHandler _wrapWithClauseHandler;
        private readonly IAutoQueryableProfile _profile;

        public AutoQueryableContext(IAutoQueryHandler autoQueryHandler, IQueryStringAccessor queryStringAccessor,
            ISelectClauseHandler selectClauseHandler, IOrderByClauseHandler orderByClauseHandler,
            IWrapWithClauseHandler wrapWithClauseHandler, IAutoQueryableProfile profile)
        {
            _autoQueryHandler = autoQueryHandler;
            _queryStringAccessor = queryStringAccessor;
            _selectClauseHandler = selectClauseHandler;
            _orderByClauseHandler = orderByClauseHandler;
            _wrapWithClauseHandler = wrapWithClauseHandler;
            _profile = profile;
        }

        public dynamic GetAutoQuery<T>(IQueryable<T> query) where T : class
        {
            return GetAutoQueryInternal(query, _queryStringAccessor);
        }

        public dynamic GetAutoQuery<T>(IQueryable<T> query, string queryString) where T : class
        {
            return GetAutoQueryInternal(query, new TestQueryStringAccessor(queryString));
        }

        private dynamic GetAutoQueryInternal<T>(IQueryable<T> query, IQueryStringAccessor queryStringAccessor)
            where T : class
        {
            var result = _autoQueryHandler.GetAutoQuery(query, queryStringAccessor, new ClauseValueManager(
                _selectClauseHandler, _orderByClauseHandler,
                _wrapWithClauseHandler, _profile)); // todo

            return result;
        }
    }
}