using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using sqlSense.Models;
using sqlSense.Services;
using sqlSense.ViewModels.Modules;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Text.Json;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using sqlSense.Views;

namespace sqlSense.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // === Sub-ViewModels (Composition) ===
        public SqlEditorViewModel SqlEditor { get; } = new();
        public TablePreviewViewModel TablePreview { get; } = new();
        public ViewCanvasViewModel Canvas { get; } = new();
        public DatabaseExplorerViewModel Explorer { get; } = new();
        
        public ObservableCollection<ViewDefinitionInfo> OpenWorkbooks { get; } = new();

        [ObservableProperty]
        private ViewDefinitionInfo? _activeWorkbook;

        [ObservableProperty]
        private bool _hasUnsavedChanges;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private string _statusBackground = "#007ACC";

        [ObservableProperty]
        private bool _isAiChatVisible = false;

        [RelayCommand]
        private void ToggleAiChat()
        {
            IsAiChatVisible = !IsAiChatVisible;
        }

        partial void OnStatusMessageChanged(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                StatusBackground = "#007ACC";
                return;
            }

            var lower = value.ToLower();
            if (lower.Contains("fail") || lower.Contains("error") || lower.Contains("⚠") || lower.Contains("invalid"))
            {
                StatusBackground = "#CA5100"; // MS SQL Server / VS Code dark red
            }
            else if (lower.Contains("success") || lower.Contains("saved") || lower.Contains("executed"))
            {
                StatusBackground = "#16825D"; // MS SQL Server / VS Code dark green
            }
            else
            {
                StatusBackground = "#007ACC"; // Default blue
            }
        }

        [ObservableProperty]
        private string _connectionString = "";

        [ObservableProperty]
        private string _serverName = "";

        private DatabaseService? _dbService;
        public DatabaseService? DbService => _dbService;

        private readonly WorkbookService _workbookService = new();

        private readonly Stack<string> _undoStack = new();
        private readonly Stack<string> _redoStack = new();
        private bool _isPerformingUndoRedo = false;

        public event Action? OnNewWorkspaceRequested;
        public event Action? OnCreateTableRequested;

        public MainViewModel()
        {
            Canvas.OnNewWorkspaceRequested += () => OnNewWorkspaceRequested?.Invoke();
            Canvas.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(Canvas.CurrentViewDefinition)) {
                    SaveWorkbookCommand.NotifyCanExecuteChanged();
                    GenerateViewSqlCommand.NotifyCanExecuteChanged();
                    
                    // Keep active workbook in sync if updated from elsewhere
                    if (Canvas.CurrentViewDefinition != ActiveWorkbook)
                        ActiveWorkbook = Canvas.CurrentViewDefinition;
                }
            };
        }

        partial void OnActiveWorkbookChanged(ViewDefinitionInfo? value)
        {
            if (value != null)
            {
                if (Canvas.CurrentViewDefinition != value)
                {
                    Canvas.CurrentViewDefinition = value;
                    if (!OpenWorkbooks.Contains(value)) OpenWorkbooks.Add(value);
                }
                
                // Show SQL in editor
                SqlEditor.SqlText = string.IsNullOrEmpty(value.SqlDefinition) ? value.ToSql() : value.SqlDefinition;
                Canvas.Zoom = value.CanvasZoom > 0 ? value.CanvasZoom : 1.0;
            }
            else
            {
                Canvas.CurrentViewDefinition = null;
                SqlEditor.SqlText = "";
            }
        }

        public void InitializeServices(string connectionString, string serverName)
        {
            ConnectionString = connectionString;
            ServerName = serverName;
            _dbService = new DatabaseService(connectionString);
            Explorer.Initialize(_dbService, serverName);
        }

        // === Orchestration Methods ===

        public async Task LoadDatabaseTreeAsync() => await Explorer.LoadDatabaseTreeAsync();
        public async Task ExpandDatabaseNodeAsync(DatabaseTreeItem node) => await Explorer.ExpandDatabaseNodeAsync(node);
        public async Task ExpandTableNodeAsync(DatabaseTreeItem node) => await Explorer.ExpandTableNodeAsync(node);

        public async Task LoadTableDataAsync(string database, string schema, string tableName)
        {
            if (_dbService == null) return;

            Canvas.IsVisible = false;
            TablePreview.IsLoading = true;
            TablePreview.IsVisible = true;
            TablePreview.TableName = $"{schema}.{tableName}";

            try
            {
                StatusMessage = $"Loading data from {schema}.{tableName}...";
                TablePreview.TableData = await _dbService.GetTableDataAsync(database, schema, tableName);
                TablePreview.CurrentPage = 1;
                TablePreview.TotalPages = Math.Max(1, (int)Math.Ceiling((double)TablePreview.TableData.Rows.Count / 10));
                TablePreview.UpdatePagedData();
                StatusMessage = $"{TablePreview.TableData.Rows.Count} row(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                TablePreview.IsVisible = false;
            }
            finally { TablePreview.IsLoading = false; }
        }

        public async Task LoadViewDefinitionAsync(string database, string schema, string viewName)
        {
            if (_dbService == null) return;

            TablePreview.IsVisible = false;
            Canvas.IsLoading = true;

            try
            {
                StatusMessage = $"Analyzing view {schema}.{viewName}...";
                Canvas.CurrentViewDefinition = await _dbService.GetViewDefinitionAsync(database, schema, viewName);
                if (!OpenWorkbooks.Any(w => w.ViewName == viewName && w.SchemaName == schema))
                    OpenWorkbooks.Add(Canvas.CurrentViewDefinition);
                    
                ActiveWorkbook = Canvas.CurrentViewDefinition;
                Canvas.IsVisible = true;
                SqlEditor.SqlText = Canvas.CurrentViewDefinition.SqlDefinition;
                StatusMessage = $"View {schema}.{viewName} loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                Canvas.IsVisible = false;
            }
            finally { Canvas.IsLoading = false; }
        }

        [RelayCommand(CanExecute = nameof(CanModifyView))]
        private async Task SaveViewChanges()
        {
            if (_dbService == null || Canvas.CurrentViewDefinition == null) return;
            try
            {
                StatusMessage = "Saving changes...";
                string sql = Canvas.CurrentViewDefinition.ToSql();
                await _dbService.ExecuteNonQueryAsync(Canvas.CurrentViewDefinition.DatabaseName, sql);
                StatusMessage = "✓ View synchronized.";
                SqlEditor.SqlText = sql;
            }
            catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        }

        [RelayCommand(CanExecute = nameof(CanModifyView))]
        private void GenerateViewSql()
        {
            if (Canvas.CurrentViewDefinition == null) return;
            SqlEditor.SqlText = Canvas.CurrentViewDefinition.ToSql();
            StatusMessage = "SQL preview updated.";
        }

        [RelayCommand]
        public void ShowCreateTable()
        {
            // Delegate to the canvas-based CreateTableCard
            OnCreateTableRequested?.Invoke();
        }

        private bool CanModifyView() => Canvas.CurrentViewDefinition != null;

        public void NotifyModification(bool pushUndo = true)
        {
            if (!_isPerformingUndoRedo && pushUndo)
            {
                _undoStack.Push(JsonSerializer.Serialize(ActiveWorkbook));
                _redoStack.Clear();
                UndoCommand.NotifyCanExecuteChanged();
                RedoCommand.NotifyCanExecuteChanged();
            }

            HasUnsavedChanges = true;
            GenerateViewSqlCommand.Execute(null);
        }

        [RelayCommand(CanExecute = nameof(CanUndo))]
        private void Undo()
        {
            if (_undoStack.Count == 0) return;

            _isPerformingUndoRedo = true;
            _redoStack.Push(JsonSerializer.Serialize(ActiveWorkbook));
            
            var state = _undoStack.Pop();
            var workbook = JsonSerializer.Deserialize<ViewDefinitionInfo>(state);
            
            if (workbook != null)
            {
                // Restore state
                ActiveWorkbook!.ReferencedTables = workbook.ReferencedTables;
                ActiveWorkbook.Joins = workbook.Joins;
                ActiveWorkbook.Columns = workbook.Columns;
                ActiveWorkbook.NodePositions = workbook.NodePositions;
                ActiveWorkbook.WhereClause = workbook.WhereClause;
                ActiveWorkbook.GroupByClause = workbook.GroupByClause;
                ActiveWorkbook.OrderByClause = workbook.OrderByClause;
                ActiveWorkbook.ViewName = workbook.ViewName;

                // Fire property changed for the whole workbook to trigger renderer
                OnPropertyChanged(nameof(ActiveWorkbook));
            }
            
            _isPerformingUndoRedo = false;
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }

        private bool CanUndo() => _undoStack.Count > 0;

        [RelayCommand(CanExecute = nameof(CanRedo))]
        private void Redo()
        {
            if (_redoStack.Count == 0) return;

            _isPerformingUndoRedo = true;
            _undoStack.Push(JsonSerializer.Serialize(ActiveWorkbook));

            var state = _redoStack.Pop();
            var workbook = JsonSerializer.Deserialize<ViewDefinitionInfo>(state);

            if (workbook != null)
            {
                // Similar restoration as Undo...
                ActiveWorkbook!.ReferencedTables = workbook.ReferencedTables;
                ActiveWorkbook.Joins = workbook.Joins;
                ActiveWorkbook.Columns = workbook.Columns;
                ActiveWorkbook.NodePositions = workbook.NodePositions;
                ActiveWorkbook.WhereClause = workbook.WhereClause;
                ActiveWorkbook.GroupByClause = workbook.GroupByClause;
                ActiveWorkbook.OrderByClause = workbook.OrderByClause;
                ActiveWorkbook.ViewName = workbook.ViewName;

                OnPropertyChanged(nameof(ActiveWorkbook));
            }

            _isPerformingUndoRedo = false;
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }

        private bool CanRedo() => _redoStack.Count > 0;

        [RelayCommand(CanExecute = nameof(CanModifyView))]
        private void SaveWorkbook()
        {
            if (ActiveWorkbook == null) return;

            var saveDialog = new SaveFileDialog
            {
                Filter = "SqlSense Workbook (*.sqv)|*.sqv",
                FileName = ActiveWorkbook.ViewName ?? "Workbook1"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    // Update model name to match file name before saving
                    string newName = Path.GetFileNameWithoutExtension(saveDialog.FileName);
                    ActiveWorkbook.ViewName = newName;
                    
                    // Sync current zoom before saving
                    ActiveWorkbook.CanvasZoom = Canvas.Zoom;
                    
                    _workbookService.SaveWorkbook(ActiveWorkbook, saveDialog.FileName);
                    HasUnsavedChanges = false;
                    StatusMessage = $"✓ Workbook saved as {newName}.sqv";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Save Error: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void OpenWorkbook()
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "SqlSense Workbook (*.sqv)|*.sqv"
            };

            if (openDialog.ShowDialog() == true)
            {
                LoadWorkbookFromFile(openDialog.FileName);
            }
        }

        public void LoadWorkbookFromFile(string path)
        {
            try
            {
                var workbook = _workbookService.LoadWorkbook(path);
                if (workbook != null)
                {
                    if (string.IsNullOrEmpty(workbook.ViewName)) 
                        workbook.ViewName = Path.GetFileNameWithoutExtension(path);
                    
                    OpenWorkbooks.Add(workbook);
                    ActiveWorkbook = workbook;
                    HasUnsavedChanges = false;
                    StatusMessage = $"✓ Loaded workbook: {workbook.ViewName}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Open Error: {ex.Message}";
            }
        }

        public async Task AddTableToViewAsync(string schema, string tableName)
        {
            if (_dbService == null || Canvas.CurrentViewDefinition == null) return;

            var alias = tableName;
            int suffix = 2;
            while (Canvas.CurrentViewDefinition.ReferencedTables.Any(t => string.Equals(t.Alias, alias, StringComparison.OrdinalIgnoreCase)))
            {
                alias = $"{tableName}{suffix++}";
            }

            var newTable = new ReferencedTable { Schema = schema, Name = tableName, Alias = alias };
            Canvas.CurrentViewDefinition.ReferencedTables.Add(newTable);
            NotifyModification();

            var allCols = await _dbService.GetColumnsAsync(Canvas.CurrentViewDefinition.DatabaseName, schema, tableName);
            Canvas.CurrentViewDefinition.SourceTableAllColumns[newTable.FullName] = allCols.Select(c => c.Name).ToList();

            StatusMessage = $"Added table {schema}.{tableName} as {alias}";
        }

        public void AddJoinRelationship(string leftAlias, string leftColumn, string rightAlias, string rightColumn, string joinType = "INNER")
        {
            if (Canvas.CurrentViewDefinition == null) return;

            var leftTable = Canvas.CurrentViewDefinition.ReferencedTables.FirstOrDefault(t => string.Equals(t.Alias, leftAlias, StringComparison.OrdinalIgnoreCase));
            var rightTable = Canvas.CurrentViewDefinition.ReferencedTables.FirstOrDefault(t => string.Equals(t.Alias, rightAlias, StringComparison.OrdinalIgnoreCase));

            if (leftTable == null || rightTable == null) return;

            var relationship = new JoinRelationship
            {
                LeftTableAlias = leftAlias, LeftTableSchema = leftTable.Schema, LeftTableName = leftTable.Name, LeftColumn = leftColumn,
                RightTableAlias = rightAlias, RightTableSchema = rightTable.Schema, RightTableName = rightTable.Name, RightColumn = rightColumn,
                JoinType = joinType
            };
            Canvas.CurrentViewDefinition.Joins.Add(relationship);
            NotifyModification();

            StatusMessage = $"Added {joinType} JOIN: {leftAlias}.{leftColumn} = {rightAlias}.{rightColumn}";
        }

        [RelayCommand]
        private async Task ShowMetadata()
        {
            await LoadDatabaseTreeAsync();
            StatusMessage = "Database metadata synchronized.";
        }

        [RelayCommand]
        private async Task RunQuery()
        {
            if (_dbService == null || string.IsNullOrWhiteSpace(SqlEditor.SqlText)) return;
            
            TablePreview.IsLoading = true;
            TablePreview.IsVisible = true;
            Canvas.IsVisible = false;
            
            try
            {
                StatusMessage = "Executing query...";
                var dbName = Canvas.CurrentViewDefinition?.DatabaseName ?? Explorer.SelectedDatabaseName ?? "master";
                TablePreview.TableData = await _dbService.ExecuteQueryAsync(dbName, SqlEditor.SqlText);
                TablePreview.TableName = "Query Results";
                TablePreview.CurrentPage = 1;
                TablePreview.TotalPages = Math.Max(1, (int)Math.Ceiling((double)TablePreview.TableData.Rows.Count / 10));
                TablePreview.UpdatePagedData();
                StatusMessage = $"Query executed. {TablePreview.TableData.Rows.Count} rows returned.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Query Error: {ex.Message}";
                TablePreview.IsVisible = false;
            }
            finally
            {
                TablePreview.IsLoading = false;
            }
        }

        [RelayCommand]
        public void NewWorkspace()
        {
            TablePreview.Reset();
            var newView = new ViewDefinitionInfo { ViewName = "New Workbook", DatabaseName = Explorer.SelectedDatabaseName ?? "master" };
            OpenWorkbooks.Add(newView);
            ActiveWorkbook = newView;
            SqlEditor.SqlText = "";
            StatusMessage = "New workbook started.";
        }

        [RelayCommand]
        public void CloseWorkbook(ViewDefinitionInfo workbook)
        {
            if (workbook == null) return;
            
            int index = OpenWorkbooks.IndexOf(workbook);
            bool wasActive = (ActiveWorkbook == workbook);
            
            OpenWorkbooks.Remove(workbook);
            
            if (wasActive && OpenWorkbooks.Count > 0)
            {
                // Try to select the previous neighbor, or the new item at the same index
                int nextIndex = Math.Clamp(index - 1, 0, OpenWorkbooks.Count - 1);
                ActiveWorkbook = OpenWorkbooks[nextIndex];
            }
            
            if (OpenWorkbooks.Count == 0)
            {
                NewWorkspace();
            }
        }
    }
}
