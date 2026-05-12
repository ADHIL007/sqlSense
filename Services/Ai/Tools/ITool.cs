using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace sqlSense.Services.Ai.Tools
{
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        JObject GetSchema();
        Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken);
    }
}
