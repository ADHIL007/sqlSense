using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace sqlSense.Views
{
    public partial class CreateTableWindow : Window
    {
        public string? Script { get; private set; }
        public string? TableName { get; private set; }
        public string? SchemaName { get; private set; }

        public ObservableCollection<TableColumnDef> Columns { get; } = new();

        public CreateTableWindow()
        {
            InitializeComponent();
            ColumnsGrid.ItemsSource = Columns;
            
            TypeCol.ItemsSource = new List<string> { 
                "int", "bigint", "smallint", "tinyint", 
                "nvarchar", "varchar", "nchar", "char",
                "datetime", "datetime2", "date",
                "bit", "decimal", "float", "uniqueidentifier"
            };

            // Add an initial row
            Columns.Add(new TableColumnDef { Name = "Id", DataType = "int", IsPrimaryKey = true, IsNullable = false });
        }

        private void AddColumn_Click(object sender, RoutedEventArgs e)
        {
            Columns.Add(new TableColumnDef { Name = "Column" + (Columns.Count + 1), DataType = "nvarchar", Length = "50", IsNullable = true });
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
            if (string.IsNullOrWhiteSpace(TableNameTxt.Text))
            {
                MessageBox.Show("Please enter a table name.");
                return;
            }

            if (!Columns.Any())
            {
                MessageBox.Show("Please add at least one column.");
                return;
            }

            TableName = TableNameTxt.Text.Trim();
            SchemaName = SchemaNameTxt.Text.Trim();

            var colDefinitions = Columns.Select(c => {
                string len = (!string.IsNullOrEmpty(c.Length) && (c.DataType.Contains("char") || c.DataType == "decimal")) 
                    ? $"({c.Length})" : "";
                string identity = (c.DataType == "int" && c.IsPrimaryKey) ? " IDENTITY(1,1)" : "";
                string pk = c.IsPrimaryKey ? " PRIMARY KEY" : "";
                string nullable = c.IsNullable ? "NULL" : "NOT NULL";
                
                return $"    [{c.Name}] {c.DataType.ToUpper()}{len}{identity}{pk} {nullable}";
            });

            Script = $"CREATE TABLE [{SchemaName}].[{TableName}] (\n{string.Join(",\n", colDefinitions)}\n);";

            DialogResult = true;
            Close();
        }
    }

    public class TableColumnDef
    {
        public string Name { get; set; } = "";
        public string DataType { get; set; } = "nvarchar";
        public string Length { get; set; } = "";
        public bool IsNullable { get; set; } = true;
        public bool IsPrimaryKey { get; set; } = false;
    }
}
