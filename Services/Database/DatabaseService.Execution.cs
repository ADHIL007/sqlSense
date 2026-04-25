using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace sqlSense.Services
{
    public partial class DatabaseService
    {
        public async Task<DataTable> GetTableDataAsync(string database, string schema, string table, int topN = 10)
        {
            var dt = new DataTable();
            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // Use QUOTENAME for safe identifier handling
            string sql = $"SELECT TOP {topN} * FROM [{schema}].[{table}]";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            
            await FillDataTableSafelyAsync(dt, reader);
            return dt;
        }

        public async Task<DataTable> ExecuteQueryAsync(string database, string sqlCommand)
        {
            var dt = new DataTable();
            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            
            LoggerService.LogSql(sqlCommand);
            
            using var conn = new SqlConnection(connStr);
            try
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlCommand, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                
                await FillDataTableSafelyAsync(dt, reader);
                return dt;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"ExecuteQueryAsync Failed for Database {database}", ex);
                throw;
            }
        }

        private async Task FillDataTableSafelyAsync(DataTable dt, SqlDataReader reader)
        {
            // Initialize Columns
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string colName = reader.GetName(i);
                if (string.IsNullOrEmpty(colName)) colName = $"Column{i}";

                Type colType = typeof(string); // Default fallback
                try
                {
                    colType = reader.GetFieldType(i) ?? typeof(string);
                }
                catch
                {
                    // Ignore and use string
                }
                
                // If multiple columns have the same name, DataTable throws. Ensure unique.
                int suffix = 1;
                string uniqueColName = colName;
                while (dt.Columns.Contains(uniqueColName))
                {
                    uniqueColName = $"{colName}_{suffix++}";
                }

                dt.Columns.Add(uniqueColName, colType);
            }

            while (await reader.ReadAsync())
            {
                var row = dt.NewRow();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    try
                    {
                        var val = reader.GetValue(i);
                        if (val == null || val == DBNull.Value)
                        {
                            row[i] = DBNull.Value;
                        }
                        else
                        {
                            if (dt.Columns[i].DataType == typeof(string) && !(val is string))
                            {
                                row[i] = val.ToString() ?? "";
                            }
                            else
                            {
                                row[i] = val;
                            }
                        }
                    }
                    catch
                    {
                        // Fallback if GetValue throws (e.g., missing UDT assembly)
                        row[i] = "<Unsupported UDT>";
                    }
                }
                dt.Rows.Add(row);
            }
        }

        /// <summary>
        /// Executes a non-query SQL command (e.g. ALTER VIEW).
        /// </summary>
        public async Task ExecuteNonQueryAsync(string sql, string? database = null)
        {
            var connStr = string.IsNullOrEmpty(database) 
                ? _connectionString 
                : ChangeDatabaseInConnectionString(_connectionString, database);
            
            LoggerService.LogSql(sql);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // Split script by 'GO' batches to correctly support sequential DDL followed by DML operations
            string[] batches = System.Text.RegularExpressions.Regex.Split(sql, @"^\s*GO\s*$", 
                                 System.Text.RegularExpressions.RegexOptions.IgnoreCase | 
                                 System.Text.RegularExpressions.RegexOptions.Multiline);

            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                using var cmd = new SqlCommand(batch, conn);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
