using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using sqlSense.Models;

namespace sqlSense.Services.Modules
{
    public class MetadataService
    {
        private readonly string _connectionString;

        public MetadataService(string connectionString)
        {
            _connectionString = connectionString;
        }

        private string ChangeDatabaseInConnectionString(string connectionString, string newDatabase)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString)
                {
                    InitialCatalog = newDatabase
                };
                return builder.ConnectionString;
            }
            catch (Exception)
            {
                return connectionString;
            }
        }

        public async Task<List<string>> GetDatabasesAsync()
        {
            var databases = new List<string>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT name FROM sys.databases ORDER BY name", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) databases.Add(reader.GetString(0));
            return databases;
        }

        public async Task<List<TableInfo>> GetTablesAsync(string database)
        {
            var tables = new List<TableInfo>();
            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"SELECT s.name AS SchemaName, t.name AS TableName FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id ORDER BY s.name, t.name", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) tables.Add(new TableInfo { Schema = reader.GetString(0), Name = reader.GetString(1) });
            return tables;
        }

        public async Task<List<ColumnInfo>> GetColumnsAsync(string database, string schema, string table)
        {
            var columns = new List<ColumnInfo>();
            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT 
                    c.name, 
                    t.name AS data_type, 
                    c.max_length, 
                    c.is_nullable,
                    ISNULL((SELECT 1 FROM sys.index_columns ic JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id WHERE ic.object_id = c.object_id AND ic.column_id = c.column_id AND i.is_primary_key = 1), 0) AS is_pk,
                    ISNULL((SELECT 1 FROM sys.foreign_key_columns fkc WHERE fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id), 0) AS is_fk,
                    c.is_identity
                FROM sys.columns c
                JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE c.object_id = OBJECT_ID(@full_name)
                ORDER BY c.column_id", conn);
            cmd.Parameters.AddWithValue("@full_name", $"[{schema}].[{table}]");
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(new ColumnInfo
                {
                    Name = reader.GetString(0),
                    DataType = reader.GetString(1),
                    MaxLength = reader.GetInt16(2),
                    IsNullable = reader.GetBoolean(3),
                    IsPrimaryKey = reader.GetInt32(4) == 1,
                    IsForeignKey = reader.GetInt32(5) == 1,
                    IsIdentity = reader.GetBoolean(6)
                });
            }
            return columns;
        }
    }
}
