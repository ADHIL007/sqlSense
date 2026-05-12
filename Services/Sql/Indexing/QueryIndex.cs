using System;
using System.Collections.Generic;

namespace sqlSense.Services.Sql.Indexing
{
    public class QueryIndex
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public SqlNodeType QueryType { get; set; }
        public string QueryPreview { get; set; } = "";
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int StartOffset { get; set; }
        public int EndOffset { get; set; }
        public List<string> Tables { get; set; } = new();
        public List<string> TempTables { get; set; } = new();
        public List<QueryIndex> Children { get; set; } = new();
    }
}
