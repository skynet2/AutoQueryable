using AutoQueryable.Core.Clauses;
using AutoQueryable.Core.Clauses.ClauseHandlers;
using AutoQueryable.Core.CriteriaFilters;
using AutoQueryable.Core.Models;
using AutoQueryable.UnitTest.Mock;

namespace AutoQueryable.UnitTest
{
    public static class AutoQueryableContextFactory
    {
        public static (AutoQueryableContext queryableContext, AutoQueryableProfile queryableProfile,
            SimpleQueryStringAccessor queryStringAccessor) Create(
                AutoQueryableSettings settings)
        {
            var profile = new AutoQueryableProfile(settings);
            var queryStringAccessor = new SimpleQueryStringAccessor();
            var selectClauseHandler = new DefaultSelectClauseHandler();
            var orderByClauseHandler = new DefaultOrderByClauseHandler();
            var wrapWithClauseHandler = new DefaultWrapWithClauseHandler();
            var clauseMapManager = new ClauseMapManager(selectClauseHandler, orderByClauseHandler,
                wrapWithClauseHandler, profile);
            var criteriaFilterManager = new CriteriaFilterManager();
            var defaultAutoQueryHandler = new AutoQueryHandler(criteriaFilterManager,
                clauseMapManager, profile);

            var autoQueryableContext = new AutoQueryableContext(defaultAutoQueryHandler, queryStringAccessor,
                selectClauseHandler, orderByClauseHandler, wrapWithClauseHandler, profile);

            return (autoQueryableContext, profile, queryStringAccessor);
        }
    }
}