using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using sqlSense.Services.Sql.Indexing;

namespace sqlSense.Services.Ai.Tools
{
    public class SearchIndexTool : ITool
    {
        public string Name => "SEARCH_INDEX";
        public string Description => "Searches the SQL structural index for matching queries, procedures, and batch bounds.";

        // We assume we have access to the cached indexes.
        // In a real implementation, this would be injected via DI.
        private readonly Func<SqlFileIndex> _getCurrentIndex;

        public SearchIndexTool(Func<SqlFileIndex> getCurrentIndex)
        {
            _getCurrentIndex = getCurrentIndex;
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
                        ["queryType"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional type of query to search for (e.g. SELECT, UPDATE, INSERT, PROCEDURE). Leave empty to get an overview of the entire document."
                        },
                        ["tableName"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional table or temp table name to filter by"
                        }
                    },
                    ["required"] = new JArray()
                }
            };
        }

        public Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken)
        {
            var index = _getCurrentIndex?.Invoke();
            if (index == null || index.Batches == null)
                return Task.FromResult(ToolResult.Error("No structural index available for the current context. Please build the index first."));

            string qTypeStr = request.Parameters.ContainsKey("queryType") ? request.Parameters["queryType"]?.ToString() : null;
            string tName = request.Parameters.ContainsKey("tableName") ? request.Parameters["tableName"]?.ToString() : null;

            bool filterByType = !string.IsNullOrEmpty(qTypeStr) && qTypeStr != "ALL";
            SqlNodeType nodeType = SqlNodeType.Unknown;
            
            if (filterByType && !Enum.TryParse<SqlNodeType>(qTypeStr, true, out nodeType))
            {
                return Task.FromResult(ToolResult.Error($"Invalid query type: {qTypeStr}"));
            }

            var results = new JArray();

            foreach (var batch in index.Batches)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // Check batch level
                if ((!filterByType || batch.NodeType == nodeType) && (string.IsNullOrEmpty(tName) || batch.Name.Contains(tName, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add(new JObject
                    {
                        ["nodeId"] = batch.Id.ToString(),
                        ["type"] = batch.NodeType.ToString(),
                        ["startLine"] = batch.StartLine,
                        ["endLine"] = batch.EndLine,
                        ["startOffset"] = batch.StartOffset,
                        ["endOffset"] = batch.EndOffset
                    });
                }

                // Check queries
                foreach (var q in batch.Queries)
                {
                    if (!filterByType || q.QueryType == nodeType)
                    {
                        bool matchesTable = string.IsNullOrEmpty(tName) || 
                                            q.Tables.Any(t => t.Equals(tName, StringComparison.OrdinalIgnoreCase)) ||
                                            q.TempTables.Any(t => t.Equals(tName, StringComparison.OrdinalIgnoreCase));

                        if (matchesTable)
                        {
                            results.Add(new JObject
                            {
                                ["nodeId"] = q.Id.ToString(),
                                ["type"] = q.QueryType.ToString(),
                                ["startLine"] = q.StartLine,
                                ["endLine"] = q.EndLine,
                                ["startOffset"] = q.StartOffset,
                                ["endOffset"] = q.EndOffset,
                                ["preview"] = q.QueryPreview
                            });
                        }
                    }
                }
            }

            return Task.FromResult(ToolResult.Success(results.ToString(Newtonsoft.Json.Formatting.None)));
        }
    }
}
