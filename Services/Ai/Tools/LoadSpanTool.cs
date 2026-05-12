using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace sqlSense.Services.Ai.Tools
{
    public class LoadSpanTool : ITool
    {
        public string Name => "LOAD_SPAN";
        public string Description => "Loads an exact SQL text span from the file using character offsets to avoid loading the full document.";

        private readonly Func<string> _getCurrentFilePath;
        private readonly Func<string> _getEditorText;

        public LoadSpanTool(Func<string> getCurrentFilePath, Func<string> getEditorText = null)
        {
            _getCurrentFilePath = getCurrentFilePath;
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
                    ["properties"] = new JObject
                    {
                        ["startOffset"] = new JObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Start character offset"
                        },
                        ["endOffset"] = new JObject
                        {
                            ["type"] = "integer",
                            ["description"] = "End character offset"
                        }
                    },
                    ["required"] = new JArray("startOffset", "endOffset")
                }
            };
        }

        public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken)
        {
            if (!request.Parameters.TryGetValue("startOffset", out var startObj) || !int.TryParse(startObj?.ToString(), out int startOffset))
                return ToolResult.Error("Invalid or missing startOffset");

            if (!request.Parameters.TryGetValue("endOffset", out var endObj) || !int.TryParse(endObj?.ToString(), out int endOffset))
                return ToolResult.Error("Invalid or missing endOffset");

            if (startOffset < 0 || endOffset <= startOffset)
                return ToolResult.Error("Invalid offset bounds.");

            int length = endOffset - startOffset;

            // Try to read from current editor text first if available (for unsaved changes)
            var currentText = _getEditorText?.Invoke();
            if (!string.IsNullOrEmpty(currentText) && currentText.Length >= endOffset)
            {
                return ToolResult.Success(currentText.Substring(startOffset, length));
            }

            var filePath = _getCurrentFilePath?.Invoke();
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return ToolResult.Error("Target file path is not available or does not exist.");

            try
            {
                char[] buffer = new char[length];

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs))
                {
                    fs.Seek(startOffset, SeekOrigin.Begin);
                    int charsRead = await reader.ReadAsync(buffer, 0, length);
                    
                    var snippet = new string(buffer, 0, charsRead);
                    return ToolResult.Success(snippet);
                }
            }
            catch (Exception ex)
            {
                return ToolResult.Error($"Failed to read span: {ex.Message}");
            }
        }
    }
}
