using Newtonsoft.Json.Linq;

namespace sqlSense.Services.Ai
{
    public static class AiToolRegistry
    {
        public static JArray GetAvailableTools()
        {
            return new JArray
            {
                new JObject
                {
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = "get_software_information",
                        ["description"] = "Get information about the SQLSense software, its developer, and GitHub repository.",
                        ["parameters"] = new JObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JObject(),
                            ["required"] = new JArray()
                        }
                    }
                }
            };
        }

        public static string ExecuteTool(string toolName, JObject arguments = null)
        {
            switch (toolName)
            {
                case "get_software_information":
                    return "Software Name: SQLSense\nDeveloper: mewmew\nGitHub: https://github.com/ADHIL007\nDescription: Open-source software for database analysis and optimization.";
                default:
                    return $"Tool '{toolName}' not recognized.";
            }
        }
    }
}
