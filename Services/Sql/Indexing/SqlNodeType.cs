using System;

namespace sqlSense.Services.Sql.Indexing
{
    public enum SqlNodeType
    {
        Batch,
        Select,
        Update,
        Insert,
        Delete,
        Merge,
        Procedure,
        Function,
        Trigger,
        Cte,
        Unknown
    }
}
