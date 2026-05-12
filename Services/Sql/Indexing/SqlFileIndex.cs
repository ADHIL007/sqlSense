using System.Collections.Generic;

namespace sqlSense.Services.Sql.Indexing
{
    public class SqlFileIndex
    {
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
        public string FileHash { get; set; } = "";
        public List<BatchIndex> Batches { get; set; } = new();
    }
}
