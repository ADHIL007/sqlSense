using System.Collections.Generic;

namespace sqlSense.Services.Sql.Indexing
{
    public interface IXshdKeywordProvider
    {
        IReadOnlySet<string> GetKeywords();
    }
}
