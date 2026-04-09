using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using sqlSense.Models;
using sqlSense.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace sqlSense.ViewModels.Modules
{
    public partial class DatabaseExplorerViewModel : ObservableObject
    {
        public ObservableCollection<DatabaseTreeItem> TreeItems { get; } = new();
        public ObservableCollection<string> Databases { get; } = new();

        [ObservableProperty]
        private string? _selectedDatabaseName;

        [ObservableProperty]
        private string _serverName = "";

        [ObservableProperty]
        private string _statusMessage = "Ready";

        private DatabaseService? _dbService;

        public DatabaseExplorerViewModel() { }

        public void Initialize(DatabaseService dbService, string serverName)
        {
            _dbService = dbService;
            ServerName = serverName;
        }

        public async Task LoadDatabaseTreeAsync()
        {
            if (_dbService == null) return;

            try
            {
                StatusMessage = "Loading databases...";
                TreeItems.Clear();
                Databases.Clear();
                var databases = await _dbService.GetDatabasesAsync();

                foreach (var db in databases)
                {
                    Databases.Add(db);
                    var dbNode = new DatabaseTreeItem
                    {
                        Name = db,
                        NodeType = TreeNodeType.Database,
                        DatabaseName = db,
                        Tooltip = $"Database: {db}"
                    };
                    // Add dummy child to show expansion arrow
                    dbNode.Children.Add(DatabaseTreeItem.CreateDummy());
                    TreeItems.Add(dbNode);
                }

                // Default selection: If no database is selected, pick the first one
                if (string.IsNullOrEmpty(SelectedDatabaseName) && databases.Any())
                {
                    SelectedDatabaseName = databases.First();
                }

                StatusMessage = $"{databases.Count} database(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading databases: {ex.Message}";
            }
        }

        public async Task ExpandDatabaseNodeAsync(DatabaseTreeItem dbNode)
        {
            if (_dbService == null || !dbNode.HasDummyChild) return;

            dbNode.IsLoading = true;
            string db = dbNode.DatabaseName;

            try
            {
                // Fetch all metadata before updating the UI to prevent multiple layout passes or empty states
                var tables = await _dbService.GetTablesAsync(db);
                var views = await _dbService.GetViewsAsync(db);
                var procs = await _dbService.GetStoredProceduresAsync(db);
                var funcs = await _dbService.GetFunctionsAsync(db);

                // Now update the collection on the UI thread
                dbNode.Children.Clear();

                var tablesFolder = new DatabaseTreeItem { Name = "Tables", NodeType = TreeNodeType.TableFolder, DatabaseName = db };
                foreach (var t in tables)
                {
                    var tNode = new DatabaseTreeItem { Name = $"{t.Schema}.{t.Name}", NodeType = TreeNodeType.Table, DatabaseName = db, SchemaName = t.Schema, Tag = t.Name, Tooltip = $"Table: {t.Schema}.{t.Name}" };
                    tNode.Children.Add(DatabaseTreeItem.CreateDummy());
                    tablesFolder.Children.Add(tNode);
                }
                tablesFolder.Tooltip = $"{tables.Count} table(s)";

                var viewsFolder = new DatabaseTreeItem { Name = "Views", NodeType = TreeNodeType.ViewFolder, DatabaseName = db };
                foreach (var v in views)
                {
                    viewsFolder.Children.Add(new DatabaseTreeItem { Name = $"{v.Schema}.{v.Name}", NodeType = TreeNodeType.View, DatabaseName = db, SchemaName = v.Schema, Tag = v.Name, Tooltip = $"View: {v.Schema}.{v.Name}" });
                }
                viewsFolder.Tooltip = $"{views.Count} view(s)";

                var procsFolder = new DatabaseTreeItem { Name = "Stored Procedures", NodeType = TreeNodeType.StoredProcedureFolder, DatabaseName = db };
                foreach (var p in procs)
                {
                    procsFolder.Children.Add(new DatabaseTreeItem { Name = $"{p.Schema}.{p.Name}", NodeType = TreeNodeType.StoredProcedure, DatabaseName = db, SchemaName = p.Schema, Tag = p.Name, Tooltip = $"Stored Procedure: {p.Schema}.{p.Name}" });
                }
                procsFolder.Tooltip = $"{procs.Count} stored procedure(s)";

                var funcsFolder = new DatabaseTreeItem { Name = "Functions", NodeType = TreeNodeType.FunctionFolder, DatabaseName = db };
                foreach (var f in funcs)
                {
                    funcsFolder.Children.Add(new DatabaseTreeItem { Name = $"{f.Schema}.{f.Name}", NodeType = TreeNodeType.Function, DatabaseName = db, SchemaName = f.Schema, Tag = f.Name, Tooltip = $"Function ({f.TypeDescription}): {f.Schema}.{f.Name}" });
                }
                funcsFolder.Tooltip = $"{funcs.Count} function(s)";

                dbNode.Children.Add(tablesFolder);
                dbNode.Children.Add(viewsFolder);
                dbNode.Children.Add(procsFolder);
                dbNode.Children.Add(funcsFolder);
                
                dbNode.IsExpanded = true;
            }
            catch (Exception ex)
            {
                dbNode.Children.Clear();
                dbNode.Children.Add(new DatabaseTreeItem { Name = $"Error: {ex.Message}", NodeType = TreeNodeType.Column });
            }
            finally { dbNode.IsLoading = false; }
        }

        public async Task ExpandTableNodeAsync(DatabaseTreeItem tableNode)
        {
            if (_dbService == null || !tableNode.HasDummyChild) return;

            tableNode.IsLoading = true;

            try
            {
                var columns = await _dbService.GetColumnsAsync(tableNode.DatabaseName, tableNode.SchemaName, tableNode.Tag);
                
                tableNode.Children.Clear();
                var columnsFolder = new DatabaseTreeItem { Name = "Columns", NodeType = TreeNodeType.ColumnFolder, DatabaseName = tableNode.DatabaseName };

                foreach (var col in columns)
                {
                    var nodeType = col.IsPrimaryKey ? TreeNodeType.PrimaryKeyColumn : col.IsForeignKey ? TreeNodeType.ForeignKeyColumn : TreeNodeType.Column;
                    string suffix = col.IsNullable ? ", null" : ", not null";
                    string pkLabel = col.IsPrimaryKey ? " (PK)" : "";
                    string fkLabel = col.IsForeignKey ? " (FK)" : "";
                    string idLabel = col.IsIdentity ? ", identity" : "";

                    columnsFolder.Children.Add(new DatabaseTreeItem
                    {
                        Name = $"{col.Name} ({col.DataType}{suffix}{idLabel}){pkLabel}{fkLabel}",
                        NodeType = nodeType,
                        Tooltip = $"Column: {col.Name}\nType: {col.DataType}({col.MaxLength})\nNullable: {col.IsNullable}"
                    });
                }
                columnsFolder.Tooltip = $"{columns.Count} column(s)";
                tableNode.Children.Add(columnsFolder);
                tableNode.IsExpanded = true;
            }
            catch (Exception ex)
            {
                tableNode.Children.Clear();
                tableNode.Children.Add(new DatabaseTreeItem { Name = $"Error: {ex.Message}", NodeType = TreeNodeType.Column });
            }
            finally { tableNode.IsLoading = false; }
        }
        
        partial void OnSelectedDatabaseNameChanged(string? value)
        {
            if (!string.IsNullOrEmpty(value))
                StatusMessage = $"Switching context to {value}...";
        }
    }
}
