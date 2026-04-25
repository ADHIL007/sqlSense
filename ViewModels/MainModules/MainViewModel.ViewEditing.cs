using CommunityToolkit.Mvvm.Input;
using sqlSense.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace sqlSense.ViewModels
{
    public partial class MainViewModel
    {
        [RelayCommand(CanExecute = nameof(CanModifyView))]
        private async Task SaveViewChanges()
        {
            if (_dbService == null || Canvas.CurrentViewDefinition == null) return;
            try
            {
                if (!Canvas.CurrentViewDefinition.IsView)
                {
                    Canvas.CurrentViewDefinition.IsView = true;
                    if (string.IsNullOrEmpty(Canvas.CurrentViewDefinition.SchemaName))
                        Canvas.CurrentViewDefinition.SchemaName = "dbo";
                }

                StatusMessage = "Saving changes to database...";
                string sql = Canvas.CurrentViewDefinition.ToSql();
                await _dbService.ExecuteNonQueryAsync(sql, Canvas.CurrentViewDefinition.DatabaseName);
                StatusMessage = $"✓ View {Canvas.CurrentViewDefinition.SchemaName}.{Canvas.CurrentViewDefinition.ViewName} synchronized.";
                SqlEditor.SqlText = sql;
            }
            catch (Exception ex) { StatusMessage = $"Error syncing view: {ex.Message}"; }
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
            OnCreateTableRequested?.Invoke();
        }

        private bool CanModifyView() => Canvas.CurrentViewDefinition != null;

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
    }
}
