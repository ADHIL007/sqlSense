using sqlSense.Models;
using System;
using System.Threading.Tasks;

namespace sqlSense.ViewModels
{
    public partial class MainViewModel
    {
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
                Canvas.CurrentViewDefinition.IsView = true;
                if (!OpenWorkbooks.Contains(Canvas.CurrentViewDefinition))
                    OpenWorkbooks.Add(Canvas.CurrentViewDefinition);
                    
                ActiveWorkbook = Canvas.CurrentViewDefinition;
                Canvas.IsVisible = true;
                SqlEditor.SqlText = Canvas.CurrentViewDefinition.SqlDefinition;
                SqlEditor.IsChartDisabled = false;
                SqlEditor.LanguageMode = "T-SQL";
                StatusMessage = $"View {schema}.{viewName} loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                Canvas.IsVisible = false;
            }
            finally { Canvas.IsLoading = false; }
        }

        public async Task LoadStoredProcedureAsync(string database, string schema, string procName)
        {
            if (_dbService == null) return;

            Canvas.IsVisible = false;
            try
            {
                StatusMessage = $"Loading {schema}.{procName}...";
                var definition = await _dbService.GetProcedureDefinitionAsync(database, schema, procName);

                SqlEditor.SqlText = definition;
                SqlEditor.IsChartDisabled = true;
                SqlEditor.ViewMode = 1;
                SqlEditor.IsVisible = true;
                SqlEditor.LanguageMode = "T-SQL (Procedure)";

                StatusMessage = $"Stored procedure {schema}.{procName} loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading procedure: {ex.Message}";
            }
        }

        public async Task LoadFunctionAsync(string database, string schema, string funcName)
        {
            if (_dbService == null) return;

            Canvas.IsVisible = false;
            try
            {
                StatusMessage = $"Loading {schema}.{funcName}...";
                var definition = await _dbService.GetFunctionDefinitionAsync(database, schema, funcName);

                SqlEditor.SqlText = definition;
                SqlEditor.IsChartDisabled = true;
                SqlEditor.ViewMode = 1;
                SqlEditor.IsVisible = true;
                SqlEditor.LanguageMode = "T-SQL (Function)";

                StatusMessage = $"Function {schema}.{funcName} loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading function: {ex.Message}";
            }
        }
    }
}
