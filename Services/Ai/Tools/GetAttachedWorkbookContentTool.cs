using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using sqlSense.Models;

namespace sqlSense.Services.Ai.Tools
{
    public class GetAttachedWorkbookContentTool : ITool
    {
        public string Name => "get_attached_workbook_content";
        public string Description => "Returns the SQL/code content of a specific open workbook by its name. Use this when the user mentions a specific open workbook.";

        private readonly Func<List<ViewDefinitionInfo>> _getOpenWorkbooks;

        public GetAttachedWorkbookContentTool(Func<List<ViewDefinitionInfo>> getOpenWorkbooks)
        {
            _getOpenWorkbooks = getOpenWorkbooks;
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
                        ["workbookName"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "The name of the open workbook/view to fetch content for (e.g. 'query.sql')."
                        }
                    },
                    ["required"] = new JArray("workbookName")
                }
            };
        }

        public Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken)
        {
            try
            {
                string wbName = request.Parameters.ContainsKey("workbookName") ? request.Parameters["workbookName"]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(wbName))
                    return Task.FromResult(ToolResult.Error("workbookName parameter is required."));

                var workbooks = _getOpenWorkbooks?.Invoke();
                if (workbooks == null || workbooks.Count == 0)
                    return Task.FromResult(ToolResult.Success("No open workbooks found."));

                var wb = workbooks.FirstOrDefault(w => w.ViewName.Equals(wbName, StringComparison.OrdinalIgnoreCase));
                if (wb == null)
                    return Task.FromResult(ToolResult.Success($"Workbook '{wbName}' is not currently open. Available workbooks are: {string.Join(", ", workbooks.Select(w => w.ViewName))}"));

                string content = string.IsNullOrEmpty(wb.SqlDefinition) ? wb.ToSql() : wb.SqlDefinition;
                
                // Truncate if extremely large to remain token friendly
                if (content.Length > 10000)
                {
                    content = content.Substring(0, 10000) + "\n-- [Workbook content truncated for token limits. Please read specific spans if needed.]";
                }

                return Task.FromResult(ToolResult.Success(content));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResult.Error($"Failed to fetch workbook content: {ex.Message}"));
            }
        }
    }
}
