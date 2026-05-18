using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using sqlSense.Services;

namespace sqlSense.Services.Ai.Tools
{
    public class GetStoredProcedureDefinitionTool : ITool
    {
        public string Name => "get_stored_procedure_definition";
        public string Description => "Returns the SQL code / definition of a stored procedure. Use this to examine the code of a stored procedure.";

        private readonly Func<DatabaseService> _getDbService;
        private readonly Func<string> _getSelectedDatabase;

        public GetStoredProcedureDefinitionTool(Func<DatabaseService> getDbService, Func<string> getSelectedDatabase)
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
                        ["procedureName"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "The name of the stored procedure to get the definition for."
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
                    ["required"] = new JArray("procedureName")
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

                string procName = request.Parameters.ContainsKey("procedureName") ? request.Parameters["procedureName"]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(procName))
                    return ToolResult.Error("procedureName parameter is required.");

                string schemaName = request.Parameters.ContainsKey("schemaName") ? request.Parameters["schemaName"]?.ToString() : "dbo";
                if (string.IsNullOrWhiteSpace(schemaName)) schemaName = "dbo";

                string dbName = request.Parameters.ContainsKey("databaseName") ? request.Parameters["databaseName"]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(dbName)) dbName = _getSelectedDatabase?.Invoke();

                if (string.IsNullOrWhiteSpace(dbName))
                    return ToolResult.Error("No active database selected and no databaseName parameter provided.");

                string definition = await dbService.GetProcedureDefinitionAsync(dbName, schemaName, procName);
                return ToolResult.Success(definition);
            }
            catch (Exception ex)
            {
                return ToolResult.Error($"Failed to retrieve stored procedure definition: {ex.Message}");
            }
        }
    }
}
