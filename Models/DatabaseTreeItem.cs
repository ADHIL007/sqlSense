using CommunityToolkit.Mvvm.ComponentModel;
using System;
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
        [NotifyPropertyChangedFor(nameof(IconGlyph))]
        [NotifyPropertyChangedFor(nameof(IconColor))]
        private TreeNodeType _nodeType;

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _tooltip = "";

        public string Tag { get; set; } = "";
        public string DatabaseName { get; set; } = "";
        public string SchemaName { get; set; } = "";

        public ObservableCollection<DatabaseTreeItem> Children { get; set; } = new();

        /// <summary>
        /// Icon glyphs — ALL using \u escape (verified working).
        /// Reverted to first-screenshot proven codes where possible.
        /// </summary>
        public string IconGlyph => NodeType switch
        {
            TreeNodeType.Server           => "\uE7F4",   // PC/Server (proven)
            TreeNodeType.DatabaseFolder   => "\uE8B7",   // Folder (proven)
            TreeNodeType.Database         => "\uE8F1",   // HardDrive/Storage (cylinder-like)
            TreeNodeType.SystemDatabaseFolder => "\uE8B7", // Folder (proven)
            TreeNodeType.TableFolder      => "\uE8B7",   // Folder (proven)
            TreeNodeType.Table            => "\uE8A5",   // Grid/Table (proven)
            TreeNodeType.ViewFolder       => "\uE8B7",   // Folder (proven)
            TreeNodeType.View             => "\uE890",   // Eye/Preview (proven)
            TreeNodeType.StoredProcedureFolder => "\uE8B7", // Folder (proven)
            TreeNodeType.StoredProcedure  => "\uE943",   // Code (proven)
            TreeNodeType.FunctionFolder   => "\uE8B7",   // Folder (proven)
            TreeNodeType.Function         => "\uE8A0",   // Fx/Calc (proven)
            TreeNodeType.ColumnFolder     => "\uE8B7",   // Folder (proven)
            TreeNodeType.Column           => "\uE70A",   // List item (proven)
            TreeNodeType.PrimaryKeyColumn => "\uE72E",   // Key (proven)
            TreeNodeType.ForeignKeyColumn => "\uE8D4",   // Link (proven)
            TreeNodeType.SecurityFolder   => "\uE72E",   // Lock (proven)
            TreeNodeType.SchemaFolder     => "\uE902",   // Hierarchy (proven)
            _ => "\uE946"
        };

        /// <summary>
        /// Icon colors per node type
        /// </summary>
        public string IconColor => NodeType switch
        {
            TreeNodeType.Server           => "#9CDCFE",
            TreeNodeType.DatabaseFolder   => "#DCDCAA",
            TreeNodeType.Database         => "#4FC3F7",
            TreeNodeType.SystemDatabaseFolder => "#A9A9A9",
            TreeNodeType.TableFolder      => "#DCDCAA",
            TreeNodeType.Table            => "#4FC3F7",
            TreeNodeType.ViewFolder       => "#DCDCAA",
            TreeNodeType.View             => "#C586C0",
            TreeNodeType.StoredProcedureFolder => "#DCDCAA",
            TreeNodeType.StoredProcedure  => "#CE9178",
            TreeNodeType.FunctionFolder   => "#DCDCAA",
            TreeNodeType.Function         => "#CE9178",
            TreeNodeType.ColumnFolder     => "#DCDCAA",
            TreeNodeType.Column           => "#9CDCFE",
            TreeNodeType.PrimaryKeyColumn => "#EBCB8B",
            TreeNodeType.ForeignKeyColumn => "#F44336",
            TreeNodeType.SecurityFolder   => "#DCDCAA",
            TreeNodeType.SchemaFolder     => "#DCDCAA",
            _ => "#888888"
        };

        public bool HasDummyChild => Children.Count == 1 && Children[0].Name == "__dummy__";

        public static DatabaseTreeItem CreateDummy()
        {
            return new DatabaseTreeItem { Name = "__dummy__", NodeType = TreeNodeType.Column };
        }
    }
}