using System;

namespace sqlSense.Services.Ai
{
    public enum ToolStatusState
    {
        Loading,
        Success,
        Error
    }

    public static class AiToolStatusManager
    {
        public static string GetStatusTag(string id, ToolStatusState state, string message)
        {
            string stateStr = state.ToString().ToLower();
            return $"<tool_status id=\"{id}\" state=\"{stateStr}\">{message}</tool_status>";
        }

        public static string GetFriendlyName(string toolName)
        {
            return toolName switch
            {
                "get_software_information" => "Software Info",
                "parsing" => "Parsing",
                "analyzing" => "Analyzing",
                "debugging" => "Debugging",
                "searching" => "Searching",
                _ => toolName
            };
        }
    }
}
