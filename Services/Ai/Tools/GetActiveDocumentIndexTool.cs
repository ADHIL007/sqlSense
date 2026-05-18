using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using sqlSense.Services.Sql.Indexing;

namespace sqlSense.Services.Ai.Tools
{
    public class GetActiveDocumentIndexTool : ITool
    {
        public string Name => "get_active_document_index";
        public string Description => "Returns the structural index (batches, procedures, and queries) of the active SQL document. If the index is empty, it will automatically parse and index the code first. Use this to get an architectural breakdown of the document.";

        private readonly Func<string> _getEditorText;
        private readonly Func<SqlFileIndex> _getCurrentIndex;
        private readonly Action<SqlFileIndex> _setCurrentIndex;

        public GetActiveDocumentIndexTool(
            Func<string> getEditorText,
            Func<SqlFileIndex> getCurrentIndex,
            Action<SqlFileIndex> setCurrentIndex)
        {
            _getEditorText = getEditorText;
            _getCurrentIndex = getCurrentIndex;
            _setCurrentIndex = setCurrentIndex;
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

        public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var index = _getCurrentIndex?.Invoke();
                var text = _getEditorText?.Invoke();

                if (index == null || index.Batches == null || index.Batches.Count == 0)
                {
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return ToolResult.Success("The active document is empty. No indexing performed.");
                    }

                    // Index the code first
                    var indexer = new SqlStructuralIndexer();
                    index = await indexer.IndexTextAsync(text, cancellationToken);
                    _setCurrentIndex?.Invoke(index);
                }

                if (index.Batches == null || index.Batches.Count == 0)
                {
                    return ToolResult.Success("No structural elements (batches, procedures, or major queries) found in the document.");
                }

                // Format it in a token-friendly way
                var batchesArray = new JArray();
                foreach (var batch in index.Batches)
                {
                    var batchObj = new JObject
                    {
                        ["type"] = batch.NodeType.ToString(),
                        ["startLine"] = batch.StartLine,
                        ["endLine"] = batch.EndLine
                    };

                    if (!string.IsNullOrEmpty(batch.Name))
                    {
                        batchObj["name"] = batch.Name;
                    }

                    if (batch.Queries != null && batch.Queries.Count > 0)
                    {
                        var queriesArray = new JArray();
                        foreach (var q in batch.Queries)
                        {
                            var qObj = new JObject
                            {
                                ["type"] = q.QueryType.ToString(),
                                ["startLine"] = q.StartLine,
                                ["endLine"] = q.EndLine,
                                ["preview"] = q.QueryPreview
                            };

                            if (q.Tables != null && q.Tables.Count > 0)
                            {
                                qObj["tables"] = new JArray(q.Tables);
                            }

                            if (q.TempTables != null && q.TempTables.Count > 0)
                            {
                                qObj["tempTables"] = new JArray(q.TempTables);
                            }

                            queriesArray.Add(qObj);
                        }
                        batchObj["queries"] = queriesArray;
                    }

                    batchesArray.Add(batchObj);
                }

                var resultObj = new JObject
                {
                    ["fileSize"] = text?.Length ?? 0,
                    ["batchesCount"] = index.Batches.Count,
                    ["batches"] = batchesArray
                };

                return ToolResult.Success(resultObj.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                return ToolResult.Error($"Failed to execute get_active_document_index: {ex.Message}");
            }
        }
    }
}
