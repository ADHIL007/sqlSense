using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace sqlSense.Services.Ai.Tools
{
    public class GetSoftwareInformationTool : ITool
    {
        public string Name => "get_software_information";
        public string Description => "Get information about the SQLSense software, its developer, and GitHub repository.";

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
            string info = "Software Name: SQLSense\nDeveloper: adhil\nGitHub: https://github.com/ADHIL007\nDescription: Open-source software for database analysis and optimization.";
            return Task.FromResult(ToolResult.Success(info));
        }
    }
}
