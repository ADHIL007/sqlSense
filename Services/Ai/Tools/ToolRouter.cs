using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace sqlSense.Services.Ai.Tools
{
    public class ToolRouter
    {
        private readonly ConcurrentDictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

        public void RegisterTool(ITool tool)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            _tools[tool.Name] = tool;
        }

        public JArray GetAvailableToolsSchema()
        {
            var schemas = new JArray();
            foreach (var tool in _tools.Values)
            {
                var schema = new JObject
                {
                    ["type"] = "function",
                    ["function"] = tool.GetSchema()
                };
                schemas.Add(schema);
            }
            return schemas;
        }

        public async Task<ToolResult> RouteAndExecuteAsync(string toolName, JObject arguments, CancellationToken cancellationToken)
        {
            if (!_tools.TryGetValue(toolName, out var tool))
            {
                return ToolResult.Error($"Tool '{toolName}' is not registered or not available in the current context.");
            }

            try
            {
                var request = new ToolRequest
                {
                    ToolName = toolName,
                    Parameters = arguments?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>()
                };

                return await tool.ExecuteAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                return ToolResult.Error($"Exception during tool execution: {ex.Message}");
            }
        }
    }
}
