using CommunityToolkit.Mvvm.Input;
using sqlSense.Services;
using System;
using System.Threading.Tasks;

namespace sqlSense.ViewModels
{
    public partial class MainViewModel
    {
        [RelayCommand]
        private async Task RunQuery()
        {
            if (_dbService == null || string.IsNullOrWhiteSpace(SqlEditor.SqlText)) return;
            
            StatusMessage = "Executing query...";
            SqlEditor.HasResults = true;
            SqlEditor.QueryResults = null;
            SqlEditor.QueryMessages = "Executing query...";
            SqlEditor.SelectedTabIndex = 1;
            
            try
            {
                var dbName = Canvas.CurrentViewDefinition?.DatabaseName ?? Explorer.SelectedDatabaseName ?? "master";
                
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var dt = await _dbService.ExecuteQueryAsync(dbName, SqlEditor.SqlText);
                sw.Stop();
                
                SqlEditor.QueryResults = dt;
                SqlEditor.QueryMessages = $"({dt.Rows.Count} rows affected)\n\nCompletion time: {DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffffffzzz}";
                SqlEditor.SelectedTabIndex = 0;
                
                StatusMessage = "Query executed successfully.";
            }
            catch (Exception ex)
            {
                SqlEditor.QueryResults = null;
                SqlEditor.QueryMessages = $"Msg 0, Level 16, State 1, Line 1\n{ex.Message}\n\nCompletion time: {DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffffffzzz}";
                SqlEditor.SelectedTabIndex = 1;
                StatusMessage = "Query executed with errors.";
            }
        }

        public void SyncSqlToChart()
        {
            if (ActiveWorkbook == null || string.IsNullOrWhiteSpace(SqlEditor.SqlText)) return;
            if (SqlEditor.IsChartDisabled) return;

            bool success = SqlParserService.TrySyncSqlToModel(SqlEditor.SqlText, ActiveWorkbook);
            if (success)
            {
                Canvas.CurrentViewDefinition = ActiveWorkbook;
                OnChartNeedsRerender?.Invoke();
                StatusMessage = "Chart synced from code.";
            }
            else
            {
                StatusMessage = "⚠ Could not parse SQL for chart — check syntax.";
            }
        }

        [RelayCommand]
        private async Task ShowMetadata()
        {
            await LoadDatabaseTreeAsync();
            StatusMessage = "Database metadata synchronized.";
        }
    }
}
