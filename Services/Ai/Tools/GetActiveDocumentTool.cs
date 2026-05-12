using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace sqlSense.Services.Ai.Tools
{
    public class GetActiveDocumentTool : ITool
    {
        public string Name => "get_active_document";
        public string Description => "Returns the SQL code currently open in the editor. Call this whenever the user asks about their query, code, or document. No parameters needed.";

        private readonly Func<string> _getEditorText;

        public GetActiveDocumentTool(Func<string> getEditorText)
        {
            _getEditorText = getEditorText;
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
                    ["properties"] = new JObject(),
                    ["required"] = new JArray()
                }
            };
        }

        public Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var text = _getEditorText?.Invoke();
                if (string.IsNullOrWhiteSpace(text))
                    return Task.FromResult(ToolResult.Success("The editor is empty. No SQL document is currently open."));

                // Truncate if extremely large
                if (text.Length > 10000)
                    text = text.Substring(0, 10000) + "\n-- [Document truncated. Use SEARCH_INDEX and LOAD_SPAN tools for full access to large files.]";

                return Task.FromResult(ToolResult.Success(text));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResult.Error($"Failed to read editor: {ex.Message}"));
            }
        }
    }
}
