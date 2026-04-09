using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using sqlSense.Models;

namespace sqlSense.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }

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

        public async Task<DataTable> GetTableDataAsync(string database, string schema, string table, int topN = 10)
        {
            var dt = new DataTable();
            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // Use QUOTENAME for safe identifier handling
            string sql = $"SELECT TOP {topN} * FROM [{schema}].[{table}]";
            using var cmd = new SqlCommand(sql, conn);
            using var adapter = new SqlDataAdapter(cmd);
            adapter.Fill(dt);
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
                using var adapter = new SqlDataAdapter(cmd);
                adapter.Fill(dt);
                return dt;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"ExecuteQueryAsync Failed for Database {database}", ex);
                throw;
            }
        }

        // === View Definition / Dependency Analysis ===

        /// <summary>
        /// Fetches the view's SQL definition and parses it to extract
        /// referenced tables, columns, and join relationships.
        /// </summary>
        public async Task<ViewDefinitionInfo> GetViewDefinitionAsync(string database, string schema, string viewName)
        {
            var info = new ViewDefinitionInfo
            {
                ViewName = viewName,
                SchemaName = schema,
                DatabaseName = database
            };

            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // 1. Get the view's SQL text
            using var cmd = new SqlCommand(
                @"SELECT m.definition
                  FROM sys.sql_modules m
                  JOIN sys.views v ON m.object_id = v.object_id
                  JOIN sys.schemas s ON v.schema_id = s.schema_id
                  WHERE s.name = @schema AND v.name = @viewName", conn);
            cmd.Parameters.AddWithValue("@schema", schema);
            cmd.Parameters.AddWithValue("@viewName", viewName);

            var definition = await cmd.ExecuteScalarAsync() as string;
            info.SqlDefinition = definition ?? "-- Definition not available";

            // 2. Also get referenced tables via sys.dm_sql_referenced_entities (reliable)
            try
            {
                using var refCmd = new SqlCommand(
                    @"SELECT DISTINCT 
                        ISNULL(referenced_schema_name, 'dbo') AS RefSchema,
                        referenced_entity_name AS RefTable
                      FROM sys.dm_sql_referenced_entities(@fullName, 'OBJECT')
                      WHERE referenced_entity_name IS NOT NULL
                        AND referenced_minor_name IS NULL", conn);
                refCmd.Parameters.AddWithValue("@fullName", $"{schema}.{viewName}");

                using var refReader = await refCmd.ExecuteReaderAsync();
                while (await refReader.ReadAsync())
                {
                    info.ReferencedTables.Add(new ReferencedTable
                    {
                        Schema = refReader.GetString(0),
                        Name = refReader.GetString(1)
                    });
                }
            }
            catch
            {
                // If dm_sql_referenced_entities fails, fall back to ScriptDom parsing
            }

            // 3. Get column-level dependencies
            try
            {
                using var colCmd = new SqlCommand(
                    @"SELECT DISTINCT 
                        ISNULL(referenced_schema_name, 'dbo') AS RefSchema,
                        referenced_entity_name AS RefTable,
                        referenced_minor_name AS RefColumn
                      FROM sys.dm_sql_referenced_entities(@fullName, 'OBJECT')
                      WHERE referenced_entity_name IS NOT NULL
                        AND referenced_minor_name IS NOT NULL", conn);
                colCmd.Parameters.AddWithValue("@fullName", $"{schema}.{viewName}");

                using var colReader = await colCmd.ExecuteReaderAsync();
                while (await colReader.ReadAsync())
                {
                    var refSchema = colReader.GetString(0);
                    var refTable = colReader.GetString(1);
                    var refColumn = colReader.GetString(2);

                    // Find or add the referenced table
                    var table = info.ReferencedTables.FirstOrDefault(
                        t => t.Name == refTable && t.Schema == refSchema);
                    if (table == null)
                    {
                        table = new ReferencedTable { Schema = refSchema, Name = refTable };
                        info.ReferencedTables.Add(table);
                    }
                    if (!table.UsedColumns.Contains(refColumn))
                        table.UsedColumns.Add(refColumn);

                    info.Columns.Add(new ViewColumnInfo
                    {
                        SourceTable = $"{refSchema}.{refTable}",
                        SourceColumn = refColumn,
                        ColumnName = refColumn
                    });
                }
            }
            catch
            {
                // Silently handle if column-level deps aren't available
            }

            // 4. Parse SQL with ScriptDom to extract JOINs
            if (!string.IsNullOrEmpty(definition))
            {
                ParseViewSqlForJoins(definition, info);
            }

            // 5. Get ALL columns for each referenced table to allow the user to add/remove columns
            foreach (var table in info.ReferencedTables)
            {
                var allCols = await GetColumnsAsync(database, table.Schema, table.Name);
                info.SourceTableAllColumns[table.FullName] = allCols.Select(c => c.Name).ToList();
                
                // Set schema/table names for joins if missing
                foreach (var join in info.Joins)
                {
                    if (join.RightTableName == table.Name && string.IsNullOrEmpty(join.RightTableSchema))
                        join.RightTableSchema = table.Schema;
                }
            }

            // 6. Get view's output columns
            try
            {
                using var viewColCmd = new SqlCommand(
                    @"SELECT c.name, t.name AS TypeName
                      FROM sys.columns c
                      JOIN sys.types t ON c.user_type_id = t.user_type_id
                      JOIN sys.views v ON c.object_id = v.object_id
                      JOIN sys.schemas s ON v.schema_id = s.schema_id
                      WHERE s.name = @schema AND v.name = @viewName
                      ORDER BY c.column_id", conn);
                viewColCmd.Parameters.AddWithValue("@schema", schema);
                viewColCmd.Parameters.AddWithValue("@viewName", viewName);

                using var viewColReader = await viewColCmd.ExecuteReaderAsync();
                // Only add output columns that aren't already tracked
                var existingCols = new HashSet<string>(info.Columns.Select(c => c.ColumnName));
                while (await viewColReader.ReadAsync())
                {
                    var colName = viewColReader.GetString(0);
                    if (!existingCols.Contains(colName))
                    {
                        info.Columns.Add(new ViewColumnInfo
                        {
                            ColumnName = colName,
                            Expression = viewColReader.GetString(1)
                        });
                    }
                }
            }
            catch { }

            return info;
        }

        /// <summary>
        /// Uses ScriptDom to parse the view SQL and extract JOIN relationships.
        /// </summary>
        private void ParseViewSqlForJoins(string sql, ViewDefinitionInfo info)
        {
            try
            {
                var parser = new TSql160Parser(false);
                using var textReader = new StringReader(sql);
                var tree = parser.Parse(textReader, out var errors);

                if (errors != null && errors.Count > 0) return;

                var visitor = new JoinVisitor();
                tree.Accept(visitor);

                foreach (var join in visitor.Joins)
                {
                    info.Joins.Add(join);
                }

                info.WhereClause = visitor.WhereClause;

                // Merge aliases and any missing tables from ScriptDom
                foreach (var tbl in visitor.Tables)
                {
                    // Try to find a matching table by full name first, then by name only if schema is dbo/missing
                    var existing = info.ReferencedTables.FirstOrDefault(t => 
                        string.Equals(t.Name, tbl.Name, StringComparison.OrdinalIgnoreCase) && 
                        string.Equals(t.Schema, tbl.Schema, StringComparison.OrdinalIgnoreCase));
                    
                    if (existing == null && (string.Equals(tbl.Schema, "dbo", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(tbl.Schema)))
                    {
                        existing = info.ReferencedTables.FirstOrDefault(t => 
                             string.Equals(t.Name, tbl.Name, StringComparison.OrdinalIgnoreCase));
                    }

                    if (existing != null)
                    {
                        existing.Alias = tbl.Alias;
                    }
                    else
                    {
                        info.ReferencedTables.Add(tbl);
                    }
                }

                // Ensure every table has an Alias (fallback to Name)
                foreach (var table in info.ReferencedTables)
                {
                    if (string.IsNullOrEmpty(table.Alias))
                    {
                        table.Alias = table.Name;
                    }
                }
            }
            catch
            {
                // Parsing might fail for complex SQL — that's okay
            }
        }

        /// <summary>
        /// Executes a non-query SQL command (e.g. ALTER VIEW).
        /// </summary>
        public async Task ExecuteNonQueryAsync(string database, string sql)
        {
            var connStr = ChangeDatabaseInConnectionString(_connectionString, database);
            LoggerService.LogSql(sql);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Returns all user tables in the database (for the "Add Table" picker).
        /// </summary>
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

        private static string ChangeDatabaseInConnectionString(string connectionString, string database)
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = database
            };
            return builder.ConnectionString;
        }
    }

    /// <summary>
    /// TSqlFragmentVisitor that extracts JOIN conditions and table references.
    /// </summary>
    internal class JoinVisitor : TSqlFragmentVisitor
    {
        public List<JoinRelationship> Joins { get; } = new();
        public List<ReferencedTable> Tables { get; } = new();
        public string WhereClause { get; set; } = "";
        private readonly Dictionary<string, string> _aliasToTable = new(StringComparer.OrdinalIgnoreCase);

        public override void Visit(QuerySpecification node)
        {
            base.Visit(node);
            if (node.WhereClause != null)
            {
                var scriptGenerator = new Sql160ScriptGenerator(new SqlScriptGeneratorOptions { SqlVersion = SqlVersion.Sql160, KeywordCasing = KeywordCasing.Uppercase });
                scriptGenerator.GenerateScript(node.WhereClause.SearchCondition, out string whereSql);
                WhereClause = whereSql;
            }
        }

        public override void Visit(NamedTableReference node)
        {
            var tableName = node.SchemaObject.BaseIdentifier?.Value ?? "";
            var schemaName = node.SchemaObject.SchemaIdentifier?.Value ?? "dbo";
            var alias = node.Alias?.Value ?? tableName;

            Tables.Add(new ReferencedTable
            {
                Schema = schemaName,
                Name = tableName,
                Alias = alias
            });

            _aliasToTable[alias] = $"{schemaName}.{tableName}";
        }

        public override void Visit(QualifiedJoin node)
        {
            base.Visit(node);

            string joinType = node.QualifiedJoinType switch
            {
                QualifiedJoinType.Inner => "INNER",
                QualifiedJoinType.LeftOuter => "LEFT",
                QualifiedJoinType.RightOuter => "RIGHT",
                QualifiedJoinType.FullOuter => "FULL",
                _ => "INNER"
            };

            // Try to extract the ON clause columns
            if (node.SearchCondition is BooleanComparisonExpression comparison)
            {
                ExtractJoinColumns(comparison, joinType);
            }
            else if (node.SearchCondition is BooleanBinaryExpression binaryExpr)
            {
                // Multiple conditions (AND / OR)
                ExtractJoinColumnsRecursive(binaryExpr, joinType);
            }
        }

        private void ExtractJoinColumnsRecursive(BooleanExpression expr, string joinType)
        {
            if (expr is BooleanComparisonExpression comp)
            {
                ExtractJoinColumns(comp, joinType);
            }
            else if (expr is BooleanBinaryExpression bin)
            {
                ExtractJoinColumnsRecursive(bin.FirstExpression, joinType);
                ExtractJoinColumnsRecursive(bin.SecondExpression, joinType);
            }
        }

        private void ExtractJoinColumns(BooleanComparisonExpression comparison, string joinType)
        {
            if (comparison.FirstExpression is ColumnReferenceExpression leftCol &&
                comparison.SecondExpression is ColumnReferenceExpression rightCol)
            {
                var leftParts = leftCol.MultiPartIdentifier?.Identifiers;
                var rightParts = rightCol.MultiPartIdentifier?.Identifiers;

                if (leftParts != null && leftParts.Count >= 2 &&
                    rightParts != null && rightParts.Count >= 2)
                {
                    var leftTable = leftParts[leftParts.Count - 2].Value;
                    var leftColumn = leftParts[leftParts.Count - 1].Value;
                    var rightTable = rightParts[rightParts.Count - 2].Value;
                    var rightColumn = rightParts[rightParts.Count - 1].Value;

                    // Resolve aliases to actual table names
                    string leftTbl = leftTable;
                    string rightTbl = rightTable;
                    string leftSchema = "dbo";
                    string rightSchema = "dbo";

                    if (_aliasToTable.TryGetValue(leftTable, out var resL))
                    {
                        var parts = resL.Split('.');
                        leftSchema = parts[0];
                        leftTbl = parts[1];
                    }
                    if (_aliasToTable.TryGetValue(rightTable, out var resR))
                    {
                        var parts = resR.Split('.');
                        rightSchema = parts[0];
                        rightTbl = parts[1];
                    }

                    Joins.Add(new JoinRelationship
                    {
                        LeftTableAlias = leftTable,
                        LeftColumn = leftColumn,
                        RightTableSchema = rightSchema,
                        RightTableName = rightTbl,
                        RightTableAlias = rightTable,
                        RightColumn = rightColumn,
                        JoinType = joinType
                    });
                }
            }
        }
    }

    public class TableInfo
    {
        public string Schema { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class ProcedureInfo
    {
        public string Schema { get; set; } = "";
        public string Name { get; set; } = "";
        public string TypeDescription { get; set; } = "";
    }

    public class FunctionInfo
    {
        public string Schema { get; set; } = "";
        public string Name { get; set; } = "";
        public string TypeDescription { get; set; } = "";
    }

    public class ColumnInfo
    {
        public string Name { get; set; } = "";
        public string DataType { get; set; } = "";
        public short MaxLength { get; set; }
        public bool IsNullable { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
        public async Task ExecuteNonQueryAsync(string sql, string? database = null)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            if (!string.IsNullOrEmpty(database)) await conn.ChangeDatabaseAsync(database);

            using var cmd = new SqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
