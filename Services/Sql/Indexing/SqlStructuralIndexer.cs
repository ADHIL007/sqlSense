using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace sqlSense.Services.Sql.Indexing
{
    public class SqlStructuralIndexer
    {
        private readonly IXshdKeywordProvider _keywordProvider;

        // Compiled Regex Patterns
        private static readonly Regex SelectRegex = new(@"(?i)\bSELECT\b", RegexOptions.Compiled);
        private static readonly Regex UpdateRegex = new(@"(?i)\bUPDATE\b", RegexOptions.Compiled);
        private static readonly Regex InsertRegex = new(@"(?i)\bINSERT\s+INTO\b", RegexOptions.Compiled);
        private static readonly Regex DeleteRegex = new(@"(?i)\bDELETE\s+FROM\b", RegexOptions.Compiled);
        private static readonly Regex MergeRegex = new(@"(?i)\bMERGE\b", RegexOptions.Compiled);
        private static readonly Regex ProcedureRegex = new(@"(?i)\bCREATE\s+PROC(?:EDURE)?\b", RegexOptions.Compiled);
        private static readonly Regex FunctionRegex = new(@"(?i)\bCREATE\s+FUNCTION\b", RegexOptions.Compiled);
        private static readonly Regex TriggerRegex = new(@"(?i)\bCREATE\s+TRIGGER\b", RegexOptions.Compiled);
        private static readonly Regex CteRegex = new(@"(?i)\bWITH\b", RegexOptions.Compiled);
        private static readonly Regex TempTableRegex = new(@"#\w+", RegexOptions.Compiled);
        
        // Batch separators
        private static readonly Regex GoRegex = new(@"(?m)^\s*GO\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public SqlStructuralIndexer(IXshdKeywordProvider keywordProvider = null)
        {
            _keywordProvider = keywordProvider ?? new XshdKeywordProvider();
        }

        public async Task<SqlFileIndex> IndexFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var index = new SqlFileIndex { FilePath = filePath };
            
            if (!File.Exists(filePath)) return index;

            var fileInfo = new FileInfo(filePath);
            index.FileSize = fileInfo.Length;

            // In a massive file scenario, we would use memory mapped files or stream readers
            // For now, we'll read large chunks or lines
            string content;
            using (var reader = new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan)))
            {
                content = await reader.ReadToEndAsync(cancellationToken);
            }
            
            // Simple hash
            index.FileHash = content.GetHashCode().ToString();

            // STEP 2: Split batches
            var batches = SplitIntoBatches(content, cancellationToken);
            index.Batches = batches;

            return index;
        }

        private List<BatchIndex> SplitIntoBatches(string content, CancellationToken cancellationToken)
        {
            var batches = new List<BatchIndex>();
            var matches = GoRegex.Matches(content);
            int startOffset = 0;
            int startLine = 1;

            for (int i = 0; i <= matches.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int endOffset = (i < matches.Count) ? matches[i].Index : content.Length;
                if (endOffset > startOffset)
                {
                    var batchText = content.Substring(startOffset, endOffset - startOffset);
                    var endLine = startLine + CountLines(batchText) - 1;

                    var batch = new BatchIndex
                    {
                        StartOffset = startOffset,
                        EndOffset = endOffset,
                        StartLine = startLine,
                        EndLine = endLine,
                        NodeType = DetermineBatchType(batchText),
                        Queries = ExtractQueries(batchText, startOffset, startLine, cancellationToken)
                    };

                    batches.Add(batch);
                }

                if (i < matches.Count)
                {
                    startOffset = matches[i].Index + matches[i].Length;
                    startLine += CountLines(content.Substring(endOffset, matches[i].Length));
                }
            }

            return batches;
        }

        private SqlNodeType DetermineBatchType(string batchText)
        {
            if (ProcedureRegex.IsMatch(batchText)) return SqlNodeType.Procedure;
            if (FunctionRegex.IsMatch(batchText)) return SqlNodeType.Function;
            if (TriggerRegex.IsMatch(batchText)) return SqlNodeType.Trigger;
            return SqlNodeType.Batch;
        }

        private List<QueryIndex> ExtractQueries(string batchText, int batchStartOffset, int batchStartLine, CancellationToken cancellationToken)
        {
            var queries = new List<QueryIndex>();

            // Find statements based on keywords
            var regexes = new[]
            {
                (SelectRegex, SqlNodeType.Select),
                (UpdateRegex, SqlNodeType.Update),
                (InsertRegex, SqlNodeType.Insert),
                (DeleteRegex, SqlNodeType.Delete),
                (MergeRegex, SqlNodeType.Merge),
                (CteRegex, SqlNodeType.Cte)
            };

            foreach (var (regex, type) in regexes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var matches = regex.Matches(batchText);
                foreach (Match m in matches)
                {
                    // Approximation of query size: to end of batch or next semicolon
                    // For a lightweight index, we just want the starting point and span
                    int statementEnd = batchText.IndexOf(';', m.Index);
                    if (statementEnd == -1) statementEnd = batchText.Length;
                    
                    var stmtText = batchText.Substring(m.Index, statementEnd - m.Index);
                    
                    int localLineStart = CountLines(batchText.Substring(0, m.Index));
                    int localLineEnd = localLineStart + CountLines(stmtText) - 1;

                    var query = new QueryIndex
                    {
                        QueryType = type,
                        StartOffset = batchStartOffset + m.Index,
                        EndOffset = batchStartOffset + statementEnd,
                        StartLine = batchStartLine + localLineStart,
                        EndLine = batchStartLine + localLineEnd,
                        QueryPreview = stmtText.Substring(0, Math.Min(stmtText.Length, 50)).Replace("\r", "").Replace("\n", " ").Trim() + "..."
                    };

                    // Extract temp tables
                    var tempMatches = TempTableRegex.Matches(stmtText);
                    foreach (Match tm in tempMatches)
                    {
                        if (!query.TempTables.Contains(tm.Value))
                            query.TempTables.Add(tm.Value);
                    }

                    queries.Add(query);
                }
            }

            // Sort queries by offset
            queries.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));

            return queries;
        }

        private int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int count = 1;
            int index = -1;
            while ((index = text.IndexOf('\n', index + 1)) != -1)
            {
                count++;
            }
            return count;
        }
    }
}
