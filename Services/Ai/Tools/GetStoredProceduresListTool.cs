using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using sqlSense.Services;

namespace sqlSense.Services.Ai.Tools
{
    public class GetStoredProceduresListTool : ITool
    {
        public string Name => "get_stored_procedures_list";
        public string Description => "Lists all stored procedures in the database, including their index, schema, and name. Use this to discover available stored procedures.";

        private readonly Func<DatabaseService> _getDbService;
        private readonly Func<string> _getSelectedDatabase;

        public GetStoredProceduresListTool(Func<DatabaseService> getDbService, Func<string> getSelectedDatabase)
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
                        ["databaseName"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional database name. Defaults to the currently selected database."
                        },
                        ["searchPattern"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional search pattern to filter stored procedure names."
                        }
                    },
                    ["required"] = new JArray()
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

                string dbName = request.Parameters.ContainsKey("databaseName") ? request.Parameters["databaseName"]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(dbName)) dbName = _getSelectedDatabase?.Invoke();

                if (string.IsNullOrWhiteSpace(dbName))
                    return ToolResult.Error("No active database selected and no databaseName parameter provided.");

                string searchPattern = request.Parameters.ContainsKey("searchPattern") ? request.Parameters["searchPattern"]?.ToString() : null;

                var procs = await dbService.GetStoredProceduresAsync(dbName);
                if (procs == null || procs.Count == 0)
                    return ToolResult.Success($"No stored procedures found in database '{dbName}'.");

                if (!string.IsNullOrEmpty(searchPattern))
                {
                    procs = procs.Where(p => p.Name.Contains(searchPattern, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                var result = new JArray();
                for (int i = 0; i < procs.Count; i++)
                {
                    var p = procs[i];
                    result.Add(new JObject
                    {
                        ["index"] = i + 1,
                        ["schema"] = p.Schema,
                        ["name"] = p.Name,
                        ["type"] = p.TypeDescription?.Trim()
                    });
                }

                var summary = new JObject
                {
                    ["database"] = dbName,
                    ["proceduresCount"] = procs.Count,
                    ["procedures"] = result
                };

                return ToolResult.Success(summary.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                return ToolResult.Error($"Failed to retrieve stored procedures list: {ex.Message}");
            }
        }
    }
}
