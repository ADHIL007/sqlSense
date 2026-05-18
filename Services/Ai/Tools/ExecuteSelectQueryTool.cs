using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using sqlSense.Services;

namespace sqlSense.Services.Ai.Tools
{
    public class ExecuteSelectQueryTool : ITool
    {
        public string Name => "execute_select_query";
        public string Description => "Executes a read-only SELECT query against the connected database and returns the resulting data. Use this tool when you need to fetch records, lookup metadata, check database settings, or verify specific details. ONLY SELECT queries are permitted.";

        private readonly Func<DatabaseService> _getDbService;
        private readonly Func<string> _getSelectedDatabase;

        public ExecuteSelectQueryTool(Func<DatabaseService> getDbService, Func<string> getSelectedDatabase)
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
                        ["query"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "The read-only SELECT query to execute on the SQL Server."
                        },
                        ["databaseName"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional database name to run the query against. Defaults to the currently selected database."
                        }
                    },
                    ["required"] = new JArray("query")
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

                string query = request.Parameters.ContainsKey("query") ? request.Parameters["query"]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(query))
                    return ToolResult.Error("query parameter is required.");

                // Validate read-only SELECT or WITH statement
                string trimmedQuery = query.TrimStart(' ', '\r', '\n', '\t');
                if (!trimmedQuery.StartsWith("select", StringComparison.OrdinalIgnoreCase) &&
                    !trimmedQuery.StartsWith("with", StringComparison.OrdinalIgnoreCase))
                {
                    return ToolResult.Error("Execution blocked: Only SELECT or WITH queries are permitted via this tool for security reasons. Destructive operations (INSERT, UPDATE, DELETE, DROP, ALTER, TRUNCATE, etc.) are prohibited.");
                }

                string dbName = request.Parameters.ContainsKey("databaseName") ? request.Parameters["databaseName"]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(dbName)) dbName = _getSelectedDatabase?.Invoke();

                if (string.IsNullOrWhiteSpace(dbName))
                    return ToolResult.Error("No active database selected and no databaseName parameter provided.");

                DataTable dt = await dbService.ExecuteQueryAsync(dbName, query);
                if (dt == null)
                    return ToolResult.Success("Query executed successfully, but returned no result sets.");

                int totalCount = dt.Rows.Count;
                int maxRows = 100; // Cap to keep it extremely token-friendly
                var rows = new JArray();

                foreach (DataRow row in dt.Rows.Cast<DataRow>().Take(maxRows))
                {
                    var rowObj = new JObject();
                    foreach (DataColumn col in dt.Columns)
                    {
                        rowObj[col.ColumnName] = row[col] == DBNull.Value ? null : JToken.FromObject(row[col]);
                    }
                    rows.Add(rowObj);
                }

                var summary = new JObject
                {
                    ["database"] = dbName,
                    ["totalRows"] = totalCount,
                    ["returnedRows"] = rows.Count,
                    ["isTruncated"] = totalCount > maxRows,
                    ["columns"] = new JArray(dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName)),
                    ["data"] = rows
                };

                return ToolResult.Success(summary.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                return ToolResult.Error($"Database query execution failed: {ex.Message}");
            }
        }
    }
}
