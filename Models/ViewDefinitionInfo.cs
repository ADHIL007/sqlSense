using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace sqlSense.Models
{
    /// <summary>
    /// Holds the parsed definition of a SQL view, 
    /// including the SQL text, referenced tables, and column mappings.
    /// </summary>
    public class ViewDefinitionInfo : INotifyPropertyChanged
    {
        private string _viewName = "";
        public string ViewName 
        { 
            get => _viewName; 
            set { _viewName = value; OnPropertyChanged(); } 
        }

        public string SchemaName { get; set; } = "";
        public string DatabaseName { get; set; } = "";
        public string SqlDefinition { get; set; } = "";
        public bool IsView { get; set; } = false;

        [System.Text.Json.Serialization.JsonIgnore]
        public string? FilePath { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        
        /// <summary>
        /// Tables/views referenced in the FROM / JOIN clauses
        /// </summary>
        public List<ReferencedTable> ReferencedTables { get; set; } = new();
        
        /// <summary>
        /// Columns currently selected by the view (for the SELECT clause)
        /// </summary>
        public List<ViewColumnInfo> Columns { get; set; } = new();

        /// <summary>
        /// All available columns of source tables (even those NOT in current view)
        /// </summary>
        public Dictionary<string, List<string>> SourceTableAllColumns { get; set; } = new();

        /// <summary>
        /// JOIN relationships between referenced tables
        /// </summary>
        public List<JoinRelationship> Joins { get; set; } = new();

        /// <summary>
        /// Optional WHERE clause
        /// </summary>
        public string WhereClause { get; set; } = "";

        /// <summary>
        /// Optional GROUP BY clause
        /// </summary>
        public string GroupByClause { get; set; } = "";

        /// <summary>
        /// Optional HAVING clause
        /// </summary>
        public string HavingClause { get; set; } = "";

        /// <summary>
        /// Optional ORDER BY clause
        /// </summary>
        public string OrderByClause { get; set; } = "";
        
        /// <summary>
        /// Persistent layout positions for table nodes
        /// </summary>
        public Dictionary<string, (double X, double Y)> NodePositions { get; set; } = new();

        public double CanvasZoom { get; set; } = 1.0;
        public double CanvasOffsetX { get; set; } = 0;
        public double CanvasOffsetY { get; set; } = 0;

        /// <summary>
        /// Generates the ALTER VIEW SQL statement based on the current state.
        /// </summary>
        public string ToSql()
        {
            var selectCols = string.Join(",\n    ", Columns.Select(c => 
                (string.IsNullOrEmpty(c.Expression) ? $"{c.SourceTable}.[{c.SourceColumn}]" : c.Expression) +
                (string.IsNullOrEmpty(c.Alias) || c.Alias == c.ColumnName ? "" : $" AS [{c.Alias}]")));

            if (string.IsNullOrEmpty(selectCols)) selectCols = "*";

            string fromClause = "";
            if (ReferencedTables.Count > 0)
            {
                var first = ReferencedTables[0];
                fromClause = $"FROM [{first.Schema}].[{first.Name}]" + 
                    (string.IsNullOrEmpty(first.Alias) ? "" : $" AS {first.Alias}");

                foreach (var join in Joins)
                {
                    fromClause += $"\n{join.JoinType} JOIN [{join.RightTableSchema}].[{join.RightTableName}]" + 
                        (string.IsNullOrEmpty(join.RightTableAlias) ? "" : $" AS {join.RightTableAlias}") +
                        $" ON {join.LeftTableAlias}.[{join.LeftColumn}] = {join.RightTableAlias}.[{join.RightColumn}]";
                }
            }

            string where = string.IsNullOrEmpty(WhereClause) ? "" : $"\nWHERE {WhereClause}";
            string groupBy = string.IsNullOrEmpty(GroupByClause) ? "" : $"\nGROUP BY {GroupByClause}";
            string having = string.IsNullOrEmpty(HavingClause) ? "" : $"\nHAVING {HavingClause}";
            string orderBy = string.IsNullOrEmpty(OrderByClause) ? "" : $"\nORDER BY {OrderByClause}";

            string query = $"SELECT\n    {selectCols}\n{fromClause}{where}{groupBy}{having}";

            if (IsView)
            {
                return $"ALTER VIEW [{SchemaName}].[{ViewName}]\nAS\n{query}";
            }

            return query;
        }
    }

    public class ReferencedTable
    {
        public string Schema { get; set; } = "";
        public string Name { get; set; } = "";
        public string Alias { get; set; } = "";
        
        /// <summary>
        /// Columns from this table that are USED in the current view
        /// </summary>
        public List<string> UsedColumns { get; set; } = new();

        public string DisplayName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
        public string FullName => $"{Schema}.{Name}";
    }

    public class ViewColumnInfo
    {
        public string ColumnName { get; set; } = "";
        public string SourceTable { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        public string Alias { get; set; } = "";
        public string Expression { get; set; } = "";
    }

    public class JoinRelationship
    {
        public string LeftTableAlias { get; set; } = "";
        public string LeftTableSchema { get; set; } = "";
        public string LeftTableName { get; set; } = "";
        public string LeftColumn { get; set; } = "";
        
        public string RightTableSchema { get; set; } = "";
        public string RightTableName { get; set; } = "";
        public string RightTableAlias { get; set; } = "";
        public string RightColumn { get; set; } = "";
        
        public string JoinType { get; set; } = "INNER";
    }
}
