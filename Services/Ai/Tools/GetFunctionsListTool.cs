using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using sqlSense.Services;

namespace sqlSense.Services.Ai.Tools
{
    public class GetFunctionsListTool : ITool
    {
        public string Name => "get_functions_list";
        public string Description => "Lists all user-defined functions in the database, including their index, schema, name, and type. Use this to discover available functions.";

        private readonly Func<DatabaseService> _getDbService;
        private readonly Func<string> _getSelectedDatabase;

        public GetFunctionsListTool(Func<DatabaseService> getDbService, Func<string> getSelectedDatabase)
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
                            ["description"] = "Optional search pattern to filter function names."
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

                var funcs = await dbService.GetFunctionsAsync(dbName);
                if (funcs == null || funcs.Count == 0)
                    return ToolResult.Success($"No user-defined functions found in database '{dbName}'.");

                if (!string.IsNullOrEmpty(searchPattern))
                {
                    funcs = funcs.Where(f => f.Name.Contains(searchPattern, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                var result = new JArray();
                for (int i = 0; i < funcs.Count; i++)
                {
                    var f = funcs[i];
                    result.Add(new JObject
                    {
                        ["index"] = i + 1,
                        ["schema"] = f.Schema,
                        ["name"] = f.Name,
                        ["type"] = f.TypeDescription?.Trim()
                    });
                }

                var summary = new JObject
                {
                    ["database"] = dbName,
                    ["functionsCount"] = funcs.Count,
                    ["functions"] = result
                };

                return ToolResult.Success(summary.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                return ToolResult.Error($"Failed to retrieve functions list: {ex.Message}");
            }
        }
    }
}
