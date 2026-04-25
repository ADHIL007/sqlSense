using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using sqlSense.Models;

namespace sqlSense.Services
{
    public partial class DatabaseService
    {
        public async Task<List<string>> GetDatabasesAsync()
        {
            var databases = new List<string>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                "SELECT name FROM sys.databases ORDER BY name", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                databases.Add(reader.GetString(0));
            }
            return databases;
        }

        public async Task<List<TableInfo>> GetTablesAsync(string database)
        {
            var tables = new List<TableInfo>();
            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                @"SELECT s.name AS SchemaName, t.name AS TableName 
                  FROM sys.tables t 
                  JOIN sys.schemas s ON t.schema_id = s.schema_id 
                  ORDER BY s.name, t.name", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(new TableInfo
                {
                    Schema = reader.GetString(0),
                    Name = reader.GetString(1)
                });
            }
            return tables;
        }

        public async Task<List<TableInfo>> GetViewsAsync(string database)
        {
            var views = new List<TableInfo>();
            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                @"SELECT s.name AS SchemaName, v.name AS ViewName 
                  FROM sys.views v 
                  JOIN sys.schemas s ON v.schema_id = s.schema_id 
                  ORDER BY s.name, v.name", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                views.Add(new TableInfo
                {
                    Schema = reader.GetString(0),
                    Name = reader.GetString(1)
                });
            }
            return views;
        }

        public async Task<List<ProcedureInfo>> GetStoredProceduresAsync(string database)
        {
            var procs = new List<ProcedureInfo>();
            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                @"SELECT s.name AS SchemaName, p.name AS ProcName, p.type_desc
                  FROM sys.procedures p 
                  JOIN sys.schemas s ON p.schema_id = s.schema_id 
                  WHERE p.is_ms_shipped = 0
                  ORDER BY s.name, p.name", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                procs.Add(new ProcedureInfo
                {
                    Schema = reader.GetString(0),
                    Name = reader.GetString(1),
                    TypeDescription = reader.GetString(2)
                });
            }
            return procs;
        }

        public async Task<List<FunctionInfo>> GetFunctionsAsync(string database)
        {
            var funcs = new List<FunctionInfo>();
            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                @"SELECT s.name AS SchemaName, o.name AS FuncName, o.type_desc
                  FROM sys.objects o 
                  JOIN sys.schemas s ON o.schema_id = s.schema_id 
                  WHERE o.type IN ('FN','IF','TF','AF') AND o.is_ms_shipped = 0
                  ORDER BY s.name, o.name", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                funcs.Add(new FunctionInfo
                {
                    Schema = reader.GetString(0),
                    Name = reader.GetString(1),
                    TypeDescription = reader.GetString(2)
                });
            }
            return funcs;
        }

        public async Task<List<ColumnInfo>> GetColumnsAsync(string database, string schema, string table)
        {
            var columns = new List<ColumnInfo>();
            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                @"SELECT c.name, t.name AS TypeName, c.max_length, c.is_nullable, c.is_identity,
                         CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey,
                         CASE WHEN fk.parent_column_id IS NOT NULL THEN 1 ELSE 0 END AS IsForeignKey
                  FROM sys.columns c
                  JOIN sys.types t ON c.user_type_id = t.user_type_id
                  JOIN sys.tables tbl ON c.object_id = tbl.object_id
                  JOIN sys.schemas s ON tbl.schema_id = s.schema_id
                  LEFT JOIN (
                      SELECT ic.object_id, ic.column_id 
                      FROM sys.index_columns ic 
                      JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id 
                      WHERE i.is_primary_key = 1
                  ) pk ON c.object_id = pk.object_id AND c.column_id = pk.column_id
                  LEFT JOIN sys.foreign_key_columns fk ON c.object_id = fk.parent_object_id AND c.column_id = fk.parent_column_id
                  WHERE s.name = @schema AND tbl.name = @table
                  ORDER BY c.column_id", conn);
            cmd.Parameters.AddWithValue("@schema", schema);
            cmd.Parameters.AddWithValue("@table", table);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(new ColumnInfo
                {
                    Name = reader.GetString(0),
                    DataType = reader.GetString(1),
                    MaxLength = reader.GetInt16(2),
                    IsNullable = reader.GetBoolean(3),
                    IsIdentity = reader.GetBoolean(4),
                    IsPrimaryKey = reader.GetInt32(5) == 1,
                    IsForeignKey = reader.GetInt32(6) == 1
                });
            }
            return columns;
        }

        public async Task<List<TableInfo>> GetAllTablesForDatabaseAsync(string database)
        {
            var tables = new List<TableInfo>();
            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                @"SELECT s.name AS SchemaName, t.name AS TableName 
                  FROM sys.tables t 
                  JOIN sys.schemas s ON t.schema_id = s.schema_id 
                  UNION ALL
                  SELECT s.name, v.name
                  FROM sys.views v
                  JOIN sys.schemas s ON v.schema_id = s.schema_id
                  ORDER BY 1, 2", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(new TableInfo
                {
                    Schema = reader.GetString(0),
                    Name = reader.GetString(1)
                });
            }
            return tables;
        }
    }
}
