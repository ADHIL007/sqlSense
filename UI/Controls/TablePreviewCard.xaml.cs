using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace sqlSense.UI.Controls
{
    public partial class TablePreviewCard : UserControl
    {
        public event Action<string>? OnDataSaveRequested;

        public TablePreviewCard()
        {
            InitializeComponent();
            PreviewGrid.RowEditEnding += PreviewGrid_RowEditEnding;
        }

        private void PreviewGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            var viewModel = DataContext as ViewModels.Modules.TablePreviewViewModel;
            if (viewModel == null || e.EditAction != DataGridEditAction.Commit) return;

            if (e.Row.Item is System.Data.DataRowView drv)
            {
                // We track modifications directly in the array as requested
                if (!viewModel.InsertedRows.Contains(drv.Row) && !viewModel.ModifiedRows.Contains(drv.Row))
                {
                    viewModel.ModifiedRows.Add(drv.Row);
                }
            }
        }

        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as ViewModels.Modules.TablePreviewViewModel;
            if (viewModel?.PagedData == null) return;

            var newDrv = viewModel.PagedData.AddNew();
            newDrv.EndEdit(); // Commits the new row to the view
            
            viewModel.InsertedRows.Add(newDrv.Row);
            
            // Scroll to the new row
            PreviewGrid.ScrollIntoView(newDrv);
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as ViewModels.Modules.TablePreviewViewModel;
            if (viewModel == null) return;

            // Stop any active editing completely
            PreviewGrid.CommitEdit();
            PreviewGrid.CommitEdit(); 

            if (!viewModel.InsertedRows.Any() && !viewModel.ModifiedRows.Any())
            {
                return; // Nothing to save
            }

            StringBuilder sb = new StringBuilder();
            
            // Assume the first column is the Primary Key if no explicit schema is known
            string pkColumn = viewModel.TableData?.Columns.Count > 0 ? viewModel.TableData.Columns[0].ColumnName : "Id";

            // Handle schema table name appropriately
            string fullTableName = viewModel.TableName;
            string schema = "dbo";
            string rawTable = fullTableName;
            if (fullTableName.Contains("."))
            {
                var parts = fullTableName.Split(new[] { '.' }, 2);
                schema = parts[0];
                rawTable = parts[1];
            }
            string safeTablename = $"[{schema}].[{rawTable}]";

            // If we have any inserts, ensure the table exists or auto-create it (Save draft behavior)
            if (viewModel.InsertedRows.Any() && viewModel.TableData?.Columns != null)
            {
                sb.AppendLine($"IF OBJECT_ID('{schema}.{rawTable}', 'U') IS NULL");
                sb.AppendLine("BEGIN");
                sb.AppendLine($"    CREATE TABLE {safeTablename} (");
                
                var colDefs = new List<string>();
                foreach (System.Data.DataColumn col in viewModel.TableData.Columns)
                {
                    string dbType = col.DataType == typeof(int) ? "INT" :
                                    col.DataType == typeof(long) ? "BIGINT" :
                                    col.DataType == typeof(bool) ? "BIT" :
                                    col.DataType == typeof(DateTime) ? "DATETIME" : "NVARCHAR(MAX)";
                    
                    if (col.ColumnName == pkColumn)
                    {
                        colDefs.Add($"        [{col.ColumnName}] {dbType} PRIMARY KEY" + (dbType == "INT" ? " IDENTITY(1,1)" : ""));
                    }
                    else
                    {
                        colDefs.Add($"        [{col.ColumnName}] {dbType} NULL");
                    }
                }
                sb.AppendLine(string.Join(",\n", colDefs));
                sb.AppendLine("    );");
                sb.AppendLine("END");
                sb.AppendLine("GO");
                sb.AppendLine("");
            }

            // Process Updates (modify data with its primary key)
            foreach (var row in viewModel.ModifiedRows)
            {
                var pkValue = row[pkColumn];
                if (pkValue == DBNull.Value) continue;

                var updates = new List<string>();
                foreach (System.Data.DataColumn col in row.Table.Columns)
                {
                    if (col.ColumnName == pkColumn) continue;
                    var val = row[col];
                    string valStr = val == DBNull.Value ? "NULL" : $"'{val.ToString()?.Replace("'", "''")}'";
                    updates.Add($"[{col.ColumnName}] = {valStr}");
                }

                if (updates.Any())
                {
                    sb.AppendLine($"UPDATE {safeTablename} SET {string.Join(", ", updates)} WHERE [{pkColumn}] = '{pkValue}';");
                }
            }

            // Process Inserts (insert data separately)
            foreach (var row in viewModel.InsertedRows)
            {
                var columns = new List<string>();
                var values = new List<string>();

                foreach (System.Data.DataColumn col in row.Table.Columns)
                {
                    var val = row[col];
                    if (val == DBNull.Value && col.ColumnName == pkColumn) continue; // Skip PK on insert if it's null (Identity)
                    
                    columns.Add($"[{col.ColumnName}]");
                    values.Add(val == DBNull.Value ? "NULL" : $"'{val.ToString()?.Replace("'", "''")}'");
                }

                if (columns.Any())
                {
                    sb.AppendLine($"INSERT INTO {safeTablename} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)});");
                }
            }

            // Clear arrays after generating script
            viewModel.ModifiedRows.Clear();
            viewModel.InsertedRows.Clear();

            // Pass the generated SQL string up to the Canvas/Database service to actually run it
            OnDataSaveRequested?.Invoke(sb.ToString());
        }

        private void PreviewGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var viewModel = DataContext as ViewModels.Modules.TablePreviewViewModel;
            if (viewModel == null) return;

            string colName = e.PropertyName;
            bool isChecked = viewModel.UsedColumns.Contains(colName);

            // Logic: if UsedColumns is populated and HideUnselectedColumns is true, only show those columns.
            if (viewModel.HideUnselectedColumns && viewModel.UsedColumns.Any() && !isChecked)
            {
                e.Column.Visibility = Visibility.Collapsed;
                return;
            }

            var headerStack = new StackPanel { Orientation = Orientation.Horizontal };
            
            var check = new CheckBox
            {
                IsChecked = isChecked,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Style = Application.Current.FindResource(typeof(CheckBox)) as Style // Use theme style
            };

            check.Click += (s, ev) => {
                viewModel.OnColumnToggle?.Invoke(colName);
            };

            var text = new TextBlock
            {
                Text = colName,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = isChecked ? System.Windows.Media.Brushes.White : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88))
            };

            headerStack.Children.Add(check);
            headerStack.Children.Add(text);

            e.Column.Header = headerStack;
        }
    }
}
