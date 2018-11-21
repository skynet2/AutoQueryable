﻿using System;
using System.Collections.Generic;
using System.Linq;
using AutoQueryable.Core.Models;

namespace AutoQueryable.Core.Clauses.ClauseHandlers
{
    public class DefaultWrapWithClauseHandler : IWrapWithClauseHandler
    {
        public IEnumerable<string> Handle(string wrapWithQueryStringPart, Type type = default, IAutoQueryableProfile profile = null)
        {
            return wrapWithQueryStringPart.Split(new []{','}, StringSplitOptions.RemoveEmptyEntries).Select(s => s.ToLowerInvariant());
        }
    }
}
