namespace sqlSense.Models
{
    public class TableInfo
    {
        public string Schema { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class ProcedureInfo
    {
        public string Schema { get; set; } = "";
        public string Name { get; set; } = "";
        public string TypeDescription { get; set; } = "";
    }

    public class FunctionInfo
    {
        public string Schema { get; set; } = "";
        public string Name { get; set; } = "";
        public string TypeDescription { get; set; } = "";
    }

    public class ColumnInfo
    {
        public string Name { get; set; } = "";
        public string DataType { get; set; } = "";
        public short MaxLength { get; set; }
        public bool IsNullable { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
    }
}
