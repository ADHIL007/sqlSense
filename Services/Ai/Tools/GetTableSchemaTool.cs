using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using sqlSense.Services;

namespace sqlSense.Services.Ai.Tools
{
    public class GetTableSchemaTool : ITool
    {
        public string Name => "get_table_schema";
        public string Description => "Returns the column definitions and schema information for a specific database table. Use this to understand the structure of a table.";

        private readonly Func<DatabaseService> _getDbService;
        private readonly Func<string> _getSelectedDatabase;

        public GetTableSchemaTool(Func<DatabaseService> getDbService, Func<string> getSelectedDatabase)
        {
            _getDbService = getDbService;
            _getSelectedDatabase = getSelectedDatabase;
        }

        public JObject GetSchema()
        {
            return new JObject
            {
                ["name"] = Name,
                ["description"] = Description,
                ["parameters"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["tableName"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "The name of the table to get the schema for."
                        },
                        ["schemaName"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional schema name (e.g. 'dbo'). Defaults to 'dbo' if not provided."
                        },
                        ["databaseName"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional database name. Defaults to the currently selected database."
                        }
                    },
                    ["required"] = new JArray("tableName")
                }
            };
        }

        public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var dbService = _getDbService?.Invoke();
                if (dbService == null)
                    return ToolResult.Error("No active database connection. Please connect to a database first.");

                string tableName = request.Parameters.ContainsKey("tableName") ? request.Parameters["tableName"]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(tableName))
                    return ToolResult.Error("tableName parameter is required.");

                string schemaName = request.Parameters.ContainsKey("schemaName") ? request.Parameters["schemaName"]?.ToString() : "dbo";
                if (string.IsNullOrWhiteSpace(schemaName)) schemaName = "dbo";

                string dbName = request.Parameters.ContainsKey("databaseName") ? request.Parameters["databaseName"]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(dbName)) dbName = _getSelectedDatabase?.Invoke();

                if (string.IsNullOrWhiteSpace(dbName))
                    return ToolResult.Error("No active database selected and no databaseName parameter provided.");

                var columns = await dbService.GetColumnsAsync(dbName, schemaName, tableName);
                if (columns == null || columns.Count == 0)
                    return ToolResult.Success($"Table '{schemaName}.{tableName}' not found or has no columns in database '{dbName}'.");

                var result = new JArray();
                foreach (var col in columns)
                {
                    var colObj = new JObject
                    {
                        ["name"] = col.Name,
                        ["type"] = col.DataType,
                        ["maxLength"] = col.MaxLength,
                        ["nullable"] = col.IsNullable
                    };
                    if (col.IsPrimaryKey) colObj["isPrimaryKey"] = true;
                    if (col.IsForeignKey) colObj["isForeignKey"] = true;
                    if (col.IsIdentity) colObj["isIdentity"] = true;

                    result.Add(colObj);
                }

                var summary = new JObject
                {
                    ["database"] = dbName,
                    ["schema"] = schemaName,
                    ["table"] = tableName,
                    ["columns"] = result
                };

                return ToolResult.Success(summary.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                return ToolResult.Error($"Failed to retrieve table schema: {ex.Message}");
            }
        }
    }
}
