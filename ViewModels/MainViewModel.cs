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

namespace sqlSense.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // === Sub-ViewModels (Composition) ===
        public SqlEditorViewModel SqlEditor { get; } = new();
        public TablePreviewViewModel TablePreview { get; } = new();
        public ViewCanvasViewModel Canvas { get; } = new();
        public DatabaseExplorerViewModel Explorer { get; } = new();

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private string _connectionString = "";

        [ObservableProperty]
        private string _serverName = "";

        private DatabaseService? _dbService;
        public DatabaseService? DbService => _dbService;

        public event Action? OnNewWorkspaceRequested;

        public MainViewModel()
        {
            Canvas.OnNewWorkspaceRequested += () => OnNewWorkspaceRequested?.Invoke();
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

        [RelayCommand]
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

        [RelayCommand]
        private void GenerateViewSql()
        {
            if (Canvas.CurrentViewDefinition == null) return;
            SqlEditor.SqlText = Canvas.CurrentViewDefinition.ToSql();
            StatusMessage = "SQL preview updated.";
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

            Canvas.CurrentViewDefinition.Joins.Add(new JoinRelationship
            {
                LeftTableAlias = leftAlias, LeftTableSchema = leftTable.Schema, LeftTableName = leftTable.Name, LeftColumn = leftColumn,
                RightTableAlias = rightAlias, RightTableSchema = rightTable.Schema, RightTableName = rightTable.Name, RightColumn = rightColumn,
                JoinType = joinType
            });

            StatusMessage = $"Added {joinType} JOIN: {leftAlias}.{leftColumn} = {rightAlias}.{rightColumn}";
        }

        [RelayCommand]
        public void NewWorkspace()
        {
            TablePreview.Reset();
            Canvas.RequestNewWorkspace();
            SqlEditor.SqlText = "-- New Workspace Ready";
            StatusMessage = "New workspace ready.";
        }
    }
}
