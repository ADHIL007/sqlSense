using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using sqlSense.Services;

namespace sqlSense.Services.Ai.Tools
{
    public class GetViewCodeTool : ITool
    {
        public string Name => "get_view_code";
        public string Description => "Returns the SQL code / definition of a database view. Use this to examine the query behind a view.";

        private readonly Func<DatabaseService> _getDbService;
        private readonly Func<string> _getSelectedDatabase;

        public GetViewCodeTool(Func<DatabaseService> getDbService, Func<string> getSelectedDatabase)
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
                        ["viewName"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "The name of the view to get the definition for."
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
                    ["required"] = new JArray("viewName")
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

                string viewName = request.Parameters.ContainsKey("viewName") ? request.Parameters["viewName"]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(viewName))
                    return ToolResult.Error("viewName parameter is required.");

                string schemaName = request.Parameters.ContainsKey("schemaName") ? request.Parameters["schemaName"]?.ToString() : "dbo";
                if (string.IsNullOrWhiteSpace(schemaName)) schemaName = "dbo";

                string dbName = request.Parameters.ContainsKey("databaseName") ? request.Parameters["databaseName"]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(dbName)) dbName = _getSelectedDatabase?.Invoke();

                if (string.IsNullOrWhiteSpace(dbName))
                    return ToolResult.Error("No active database selected and no databaseName parameter provided.");

                var viewInfo = await dbService.GetViewDefinitionAsync(dbName, schemaName, viewName);
                if (viewInfo == null || string.IsNullOrEmpty(viewInfo.SqlDefinition))
                    return ToolResult.Success($"View '{schemaName}.{viewName}' not found or its definition is not available in database '{dbName}'.");

                return ToolResult.Success(viewInfo.SqlDefinition);
            }
            catch (Exception ex)
            {
                return ToolResult.Error($"Failed to retrieve view definition: {ex.Message}");
            }
        }
    }
}
