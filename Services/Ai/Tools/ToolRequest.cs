using System.Collections.Generic;

namespace sqlSense.Services.Ai.Tools
{
    public class ToolRequest
    {
        public string ToolName { get; set; } = "";
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}
