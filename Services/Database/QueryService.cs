using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace sqlSense.Services.Modules
{
    public class QueryService
    {
        private readonly string _connectionString;

        public QueryService(string connectionString)
        {
            _connectionString = connectionString;
        }

        private string ChangeDatabaseInConnectionString(string connectionString, string newDatabase)
        {
            var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = newDatabase };
            return builder.ConnectionString;
        }

        public async Task<DataTable> GetTableDataAsync(string database, string schema, string table, int top = 100)
        {
            var dt = new DataTable();
            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand($"SELECT TOP (@top) * FROM [{schema}].[{table}]", conn);
            cmd.Parameters.AddWithValue("@top", top);
            using var adapter = new SqlDataAdapter(cmd);
            adapter.Fill(dt);
            return dt;
        }

        public async Task ExecuteNonQueryAsync(string database, string sql)
        {
            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
