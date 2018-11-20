﻿using System.Linq;
using AutoQueryable.Core.Clauses;

namespace AutoQueryable.Core.Models
{
    public interface IAutoQueryableContext
    {
        dynamic GetAutoQuery<T>(IQueryable<T> query) where T : class;
        IClauseValueManager ClauseValueManager { get; }
        IQueryable<dynamic> TotalCountQuery { get; }
        string QueryString { get; }
    }

}