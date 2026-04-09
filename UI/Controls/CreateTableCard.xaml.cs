using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using sqlSense.Views;

namespace sqlSense.UI.Controls
{
    /// <summary>
    /// An in-canvas card for creating a new SQL table.
    /// Themed to match the TablePreviewCard design language.
    /// </summary>
    public partial class CreateTableCard : UserControl
    {
        public ObservableCollection<TableColumnDef> Columns { get; } = new();

        /// <summary>
        /// Fired when the user confirms creation. Passes the generated SQL script, table name, and schema.
        /// </summary>
        public event Action<string, string, string>? OnCreateRequested;

        /// <summary>
        /// Fired when the user cancels or closes the card.
        /// </summary>
        public event Action? OnCancelled;

        public CreateTableCard()
        {
            InitializeComponent();
            ColumnsGrid.ItemsSource = Columns;

            TypeCol.ItemsSource = new List<string>
            {
                "int", "bigint", "smallint", "tinyint",
                "nvarchar", "varchar", "nchar", "char",
                "datetime", "datetime2", "date",
                "bit", "decimal", "float", "uniqueidentifier"
            };

            // Start with one default column
            Columns.Add(new TableColumnDef { Name = "Id", DataType = "int", IsPrimaryKey = true, IsAutoIncrement = true, IsNullable = false });
        }

        private void AddColumn_Click(object sender, RoutedEventArgs e)
        {
            Columns.Add(new TableColumnDef
            {
                Name = "Column" + (Columns.Count + 1),
                DataType = "nvarchar",
                Length = "50",
                IsNullable = true
            });
        }

        private void RemoveColumn_Click(object sender, RoutedEventArgs e)
        {
            if (ColumnsGrid.SelectedItem is TableColumnDef def)
            {
                Columns.Remove(def);
            }
        }

        private void CreateTable_Click(object sender, RoutedEventArgs e)
        {
            string tableName = TableNameTxt.Text.Trim();
            string schemaName = SchemaNameTxt.Text.Trim();

            if (string.IsNullOrWhiteSpace(tableName))
            {
                StatusLabel.Text = "⚠ Enter a table name";
                StatusLabel.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
                return;
            }

            if (!Columns.Any())
            {
                StatusLabel.Text = "⚠ Add at least one column";
                StatusLabel.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
                return;
            }

            // Build the CREATE TABLE SQL
            var colDefinitions = Columns.Select(c =>
            {
                string len = (!string.IsNullOrEmpty(c.Length) && (c.DataType.Contains("char") || c.DataType == "decimal"))
                    ? $"({c.Length})" : "";
                string identity = c.IsAutoIncrement ? " IDENTITY(1,1)" : "";
                string pk = c.IsPrimaryKey ? " PRIMARY KEY" : "";
                string nullable = c.IsNullable ? "NULL" : "NOT NULL";

                return $"    [{c.Name}] {c.DataType.ToUpper()}{len}{identity}{pk} {nullable}";
            });

            string script = $"CREATE TABLE [{schemaName}].[{tableName}] (\n{string.Join(",\n", colDefinitions)}\n);";

            OnCreateRequested?.Invoke(script, tableName, schemaName);
        }

        private void CloseCard_Click(object sender, RoutedEventArgs e)
        {
            OnCancelled?.Invoke();
        }

        /// <summary>
        /// Select all text on focus to improve inline editing UX.
        /// </summary>
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.SelectAll();
            }
        }
    }
}
