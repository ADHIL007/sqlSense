using System;
using System.Collections.Generic;

namespace sqlSense.Services.Sql.Indexing
{
    public class BatchIndex
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public SqlNodeType NodeType { get; set; } = SqlNodeType.Batch;
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int StartOffset { get; set; }
        public int EndOffset { get; set; }
        public List<QueryIndex> Queries { get; set; } = new();
    }
}
