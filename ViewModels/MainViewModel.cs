using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using sqlSense.Models;
using sqlSense.Services;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace sqlSense.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _sqlText = "-- Select a table from the Object Explorer\n-- to preview its data here";

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private string _connectionString = "";

        [ObservableProperty]
        private string _serverName = "";

        // === Table Data Preview ===
        [ObservableProperty]
        private DataTable? _tableData;

        [ObservableProperty]
        private DataView? _pagedData;

        [ObservableProperty]
        private string _previewTableName = "";

        [ObservableProperty]
        private bool _isPreviewVisible;

        [ObservableProperty]
        private bool _isPreviewLoading;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _totalPages = 1;

        [ObservableProperty]
        private string _pageInfo = "Page 1 of 1";

        private const int PageSize = 5;

        // === View Visualization ===
        [ObservableProperty]
        private ViewDefinitionInfo? _currentViewDefinition;

        [ObservableProperty]
        private bool _isViewVisualizationVisible;

        [ObservableProperty]
        private bool _isViewLoading;

        // === Canvas Zoom ===
        [ObservableProperty]
        private double _canvasZoom = 1.0;

        [ObservableProperty]
        private string _zoomPercentage = "100%";

        public ObservableCollection<DatabaseTreeItem> TreeItems { get; } = new();

        private DatabaseService? _dbService;

        public MainViewModel()
        {
        }

        // === Table Data Preview Methods ===

        public async Task LoadTableDataAsync(string database, string schema, string tableName)
        {
            if (_dbService == null) return;

            // Hide view visualization when showing table data
            IsViewVisualizationVisible = false;

            IsPreviewLoading = true;
            IsPreviewVisible = true;
            PreviewTableName = $"{schema}.{tableName}";

            try
            {
                StatusMessage = $"Loading data from {schema}.{tableName}...";
                TableData = await _dbService.GetTableDataAsync(database, schema, tableName);
                CurrentPage = 1;
                TotalPages = Math.Max(1, (int)Math.Ceiling((double)TableData.Rows.Count / PageSize));
                UpdatePagedData();
                StatusMessage = $"{TableData.Rows.Count} row(s) loaded from {schema}.{tableName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading data: {ex.Message}";
                IsPreviewVisible = false;
            }
            finally
            {
                IsPreviewLoading = false;
            }
        }

        private void UpdatePagedData()
        {
            if (TableData == null) return;

            var paged = TableData.Clone();
            int start = (CurrentPage - 1) * PageSize;
            int end = Math.Min(start + PageSize, TableData.Rows.Count);

            for (int i = start; i < end; i++)
            {
                paged.ImportRow(TableData.Rows[i]);
            }

            PagedData = paged.DefaultView;
            PageInfo = $"Page {CurrentPage} of {TotalPages}  ({TableData.Rows.Count} rows)";
        }

        [RelayCommand]
        private void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                UpdatePagedData();
            }
        }

        [RelayCommand]
        private void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                UpdatePagedData();
            }
        }

        // === View Definition / Visualization Methods ===

        public async Task LoadViewDefinitionAsync(string database, string schema, string viewName)
        {
            if (_dbService == null) return;

            // Hide table data card when showing view visualization
            IsPreviewVisible = false;
            IsViewLoading = true;

            try
            {
                StatusMessage = $"Analyzing view {schema}.{viewName}...";
                CurrentViewDefinition = await _dbService.GetViewDefinitionAsync(database, schema, viewName);
                IsViewVisualizationVisible = true;

                // Also show the SQL in the editor panel
                SqlText = CurrentViewDefinition.SqlDefinition;

                int tableCount = CurrentViewDefinition.ReferencedTables.Count;
                int joinCount = CurrentViewDefinition.Joins.Count;
                StatusMessage = $"View {schema}.{viewName}: {tableCount} source table(s), {joinCount} join(s)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error analyzing view: {ex.Message}";
                IsViewVisualizationVisible = false;
            }
            finally
            {
                IsViewLoading = false;
            }
        }

        [RelayCommand]
        private async Task SaveViewChanges()
        {
            if (_dbService == null || CurrentViewDefinition == null) return;

            try
            {
                StatusMessage = "Saving view changes...";
                string sql = CurrentViewDefinition.ToSql();
                
                // Execute the ALTER VIEW script
                // We'll reuse GetDatabasesAsync's connection logic for a raw execution
                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(
                    _dbService.GetType().GetField("_connectionString", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_dbService) as string))
                {
                    await conn.OpenAsync();
                    var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(conn.ConnectionString)
                    {
                        InitialCatalog = CurrentViewDefinition.DatabaseName
                    };
                    conn.Close();
                    conn.ConnectionString = builder.ConnectionString;
                    await conn.OpenAsync();

                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                StatusMessage = "View synchronized successfully.";
                SqlText = sql; // show the final script
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving view: {ex.Message}";
            }
        }

        // === Canvas Zoom Methods ===

        [RelayCommand]
        private void ZoomIn()
        {
            CanvasZoom = Math.Min(CanvasZoom + 0.1, 5.0);
            ZoomPercentage = $"{(int)(CanvasZoom * 100)}%";
        }

        [RelayCommand]
        private void ZoomOut()
        {
            CanvasZoom = Math.Max(CanvasZoom - 0.1, 0.1);
            ZoomPercentage = $"{(int)(CanvasZoom * 100)}%";
        }

        [RelayCommand]
        private void ZoomReset()
        {
            CanvasZoom = 1.0;
            ZoomPercentage = "100%";
        }

        [RelayCommand]
        private void ZoomFit()
        {
            CanvasZoom = 0.5;
            ZoomPercentage = "50%";
        }

        public void SetZoom(double zoom)
        {
            CanvasZoom = Math.Clamp(zoom, 0.1, 5.0);
            ZoomPercentage = $"{(int)(CanvasZoom * 100)}%";
        }

        // === Tree Loading Methods ===

        public async Task LoadDatabaseTreeAsync()
        {
            if (string.IsNullOrEmpty(ConnectionString)) return;

            _dbService = new DatabaseService(ConnectionString);
            TreeItems.Clear();

            try
            {
                StatusMessage = "Loading database tree...";

                var serverNode = new DatabaseTreeItem
                {
                    Name = ServerName,
                    NodeType = TreeNodeType.Server,
                    IsExpanded = true
                };

                var dbFolder = new DatabaseTreeItem
                {
                    Name = "Databases",
                    NodeType = TreeNodeType.DatabaseFolder,
                    IsExpanded = true
                };

                var databases = await _dbService.GetDatabasesAsync();
                var systemDbs = new[] { "master", "model", "msdb", "tempdb" };

                var systemDbFolder = new DatabaseTreeItem
                {
                    Name = "System Databases",
                    NodeType = TreeNodeType.SystemDatabaseFolder,
                    IsExpanded = false
                };

                foreach (var db in databases)
                {
                    var dbNode = new DatabaseTreeItem
                    {
                        Name = db,
                        NodeType = TreeNodeType.Database,
                        DatabaseName = db,
                        IsExpanded = false
                    };

                    dbNode.Children.Add(DatabaseTreeItem.CreateDummy());

                    if (systemDbs.Contains(db, StringComparer.OrdinalIgnoreCase))
                    {
                        systemDbFolder.Children.Add(dbNode);
                    }
                    else
                    {
                        dbFolder.Children.Add(dbNode);
                    }
                }

                dbFolder.Children.Insert(0, systemDbFolder);

                var securityFolder = new DatabaseTreeItem
                {
                    Name = "Security",
                    NodeType = TreeNodeType.SecurityFolder
                };

                serverNode.Children.Add(dbFolder);
                serverNode.Children.Add(securityFolder);

                TreeItems.Add(serverNode);

                StatusMessage = $"Connected — {databases.Count} databases found";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading tree: {ex.Message}";
            }
        }

        public async Task ExpandDatabaseNodeAsync(DatabaseTreeItem dbNode)
        {
            if (_dbService == null || !dbNode.HasDummyChild) return;

            dbNode.IsLoading = true;
            dbNode.Children.Clear();

            try
            {
                string db = dbNode.DatabaseName;

                var tablesFolder = new DatabaseTreeItem
                {
                    Name = "Tables",
                    NodeType = TreeNodeType.TableFolder,
                    DatabaseName = db
                };

                var tables = await _dbService.GetTablesAsync(db);
                foreach (var t in tables)
                {
                    var tableNode = new DatabaseTreeItem
                    {
                        Name = $"{t.Schema}.{t.Name}",
                        NodeType = TreeNodeType.Table,
                        DatabaseName = db,
                        SchemaName = t.Schema,
                        Tag = t.Name,
                        Tooltip = $"Table: {t.Schema}.{t.Name}"
                    };
                    tableNode.Children.Add(DatabaseTreeItem.CreateDummy());
                    tablesFolder.Children.Add(tableNode);
                }
                tablesFolder.Tooltip = $"{tables.Count} table(s)";

                var viewsFolder = new DatabaseTreeItem
                {
                    Name = "Views",
                    NodeType = TreeNodeType.ViewFolder,
                    DatabaseName = db
                };

                var views = await _dbService.GetViewsAsync(db);
                foreach (var v in views)
                {
                    viewsFolder.Children.Add(new DatabaseTreeItem
                    {
                        Name = $"{v.Schema}.{v.Name}",
                        NodeType = TreeNodeType.View,
                        DatabaseName = db,
                        SchemaName = v.Schema,
                        Tag = v.Name,
                        Tooltip = $"View: {v.Schema}.{v.Name}"
                    });
                }
                viewsFolder.Tooltip = $"{views.Count} view(s)";

                var procsFolder = new DatabaseTreeItem
                {
                    Name = "Stored Procedures",
                    NodeType = TreeNodeType.StoredProcedureFolder,
                    DatabaseName = db
                };

                var procs = await _dbService.GetStoredProceduresAsync(db);
                foreach (var p in procs)
                {
                    procsFolder.Children.Add(new DatabaseTreeItem
                    {
                        Name = $"{p.Schema}.{p.Name}",
                        NodeType = TreeNodeType.StoredProcedure,
                        DatabaseName = db,
                        SchemaName = p.Schema,
                        Tag = p.Name,
                        Tooltip = $"Stored Procedure: {p.Schema}.{p.Name}"
                    });
                }
                procsFolder.Tooltip = $"{procs.Count} stored procedure(s)";

                var funcsFolder = new DatabaseTreeItem
                {
                    Name = "Functions",
                    NodeType = TreeNodeType.FunctionFolder,
                    DatabaseName = db
                };

                var funcs = await _dbService.GetFunctionsAsync(db);
                foreach (var f in funcs)
                {
                    funcsFolder.Children.Add(new DatabaseTreeItem
                    {
                        Name = $"{f.Schema}.{f.Name}",
                        NodeType = TreeNodeType.Function,
                        DatabaseName = db,
                        SchemaName = f.Schema,
                        Tag = f.Name,
                        Tooltip = $"Function ({f.TypeDescription}): {f.Schema}.{f.Name}"
                    });
                }
                funcsFolder.Tooltip = $"{funcs.Count} function(s)";

                dbNode.Children.Add(tablesFolder);
                dbNode.Children.Add(viewsFolder);
                dbNode.Children.Add(procsFolder);
                dbNode.Children.Add(funcsFolder);
            }
            catch (Exception ex)
            {
                dbNode.Children.Add(new DatabaseTreeItem
                {
                    Name = $"Error: {ex.Message}",
                    NodeType = TreeNodeType.Column
                });
            }
            finally
            {
                dbNode.IsLoading = false;
            }
        }

        public async Task ExpandTableNodeAsync(DatabaseTreeItem tableNode)
        {
            if (_dbService == null || !tableNode.HasDummyChild) return;

            tableNode.IsLoading = true;
            tableNode.Children.Clear();

            try
            {
                var columnsFolder = new DatabaseTreeItem
                {
                    Name = "Columns",
                    NodeType = TreeNodeType.ColumnFolder,
                    DatabaseName = tableNode.DatabaseName
                };

                var columns = await _dbService.GetColumnsAsync(
                    tableNode.DatabaseName, tableNode.SchemaName, tableNode.Tag);

                foreach (var col in columns)
                {
                    var nodeType = col.IsPrimaryKey ? TreeNodeType.PrimaryKeyColumn
                                 : col.IsForeignKey ? TreeNodeType.ForeignKeyColumn
                                 : TreeNodeType.Column;

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
            }
            catch (Exception ex)
            {
                tableNode.Children.Add(new DatabaseTreeItem
                {
                    Name = $"Error: {ex.Message}",
                    NodeType = TreeNodeType.Column
                });
            }
            finally
            {
                tableNode.IsLoading = false;
            }
        }

        [RelayCommand]
        private void RunQuery()
        {
            StatusMessage = "Executing query...";
        }

        [RelayCommand]
        private void ShowMetadata()
        {
            StatusMessage = "Refreshing metadata...";
        }
    }
}

