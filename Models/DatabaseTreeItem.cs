using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace sqlSense.Models
{
    public enum TreeNodeType
    {
        Server,
        DatabaseFolder,
        Database,
        SystemDatabaseFolder,
        TableFolder,
        Table,
        ViewFolder,
        View,
        StoredProcedureFolder,
        StoredProcedure,
        FunctionFolder,
        Function,
        ColumnFolder,
        Column,
        PrimaryKeyColumn,
        ForeignKeyColumn,
        SecurityFolder,
        SchemaFolder
    }

    public partial class DatabaseTreeItem : ObservableObject
    {
        [ObservableProperty]
        private string _name = "";

        [ObservableProperty]
        private TreeNodeType _nodeType;

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _tooltip = "";

        /// <summary>
        /// Extra data (e.g. schema name, database name) for lazy loading
        /// </summary>
        public string Tag { get; set; } = "";

        /// <summary>
        /// Database this node belongs to (for column queries)
        /// </summary>
        public string DatabaseName { get; set; } = "";

        /// <summary>
        /// Schema this node belongs to
        /// </summary>
        public string SchemaName { get; set; } = "";

        public ObservableCollection<DatabaseTreeItem> Children { get; set; } = new();

        /// <summary>
        /// Returns the icon glyph for this node type (Segoe MDL2 Assets / Segoe Fluent Icons)
        /// </summary>
        public string IconGlyph => NodeType switch
        {
            TreeNodeType.Server => "\uE968",            // Server
            TreeNodeType.DatabaseFolder => "\uE8B7",    // Folder
            TreeNodeType.Database => "\uE964",          // Database cylinder
            TreeNodeType.SystemDatabaseFolder => "\uE8B7",
            TreeNodeType.TableFolder => "\uE8B7",
            TreeNodeType.Table => "\uE80A",             // Grid/Table
            TreeNodeType.ViewFolder => "\uE8B7",
            TreeNodeType.View => "\uE7B3",              // View/Eye
            TreeNodeType.StoredProcedureFolder => "\uE8B7",
            TreeNodeType.StoredProcedure => "\uE943",   // Code
            TreeNodeType.FunctionFolder => "\uE8B7",
            TreeNodeType.Function => "\uE8EF",          // Calculator/Function
            TreeNodeType.ColumnFolder => "\uE8B7",
            TreeNodeType.Column => "\uE71A",            // Column list
            TreeNodeType.PrimaryKeyColumn => "\uE8D7",  // Key
            TreeNodeType.ForeignKeyColumn => "\uE72E",  // Link
            TreeNodeType.SecurityFolder => "\uE72E",
            TreeNodeType.SchemaFolder => "\uE8B7",
            _ => "\uE7C3"
        };

        /// <summary>
        /// Returns the icon color for this node type
        /// </summary>
        public string IconColor => NodeType switch
        {
            TreeNodeType.Server => "#9CDCFE",
            TreeNodeType.DatabaseFolder => "#DCDCAA",
            TreeNodeType.Database => "#4FC3F7",
            TreeNodeType.SystemDatabaseFolder => "#DCDCAA",
            TreeNodeType.TableFolder => "#DCDCAA",
            TreeNodeType.Table => "#4FC3F7",
            TreeNodeType.ViewFolder => "#DCDCAA",
            TreeNodeType.View => "#C586C0",
            TreeNodeType.StoredProcedureFolder => "#DCDCAA",
            TreeNodeType.StoredProcedure => "#CE9178",
            TreeNodeType.FunctionFolder => "#DCDCAA",
            TreeNodeType.Function => "#DCDCAA",
            TreeNodeType.ColumnFolder => "#DCDCAA",
            TreeNodeType.Column => "#9CDCFE",
            TreeNodeType.PrimaryKeyColumn => "#FFB74D",
            TreeNodeType.ForeignKeyColumn => "#F44336",
            TreeNodeType.SecurityFolder => "#FFB74D",
            TreeNodeType.SchemaFolder => "#DCDCAA",
            _ => "#888888"
        };

        /// <summary>
        /// Indicates whether this node has a dummy child for lazy loading
        /// </summary>
        public bool HasDummyChild => Children.Count == 1 && Children[0].Name == "__dummy__";

        public static DatabaseTreeItem CreateDummy()
        {
            return new DatabaseTreeItem { Name = "__dummy__", NodeType = TreeNodeType.Column };
        }
    }
}
