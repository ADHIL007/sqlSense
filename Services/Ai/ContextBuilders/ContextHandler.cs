using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace sqlSense.Services.Ai.ContextBuilders
{
    public static class ContextHandler
    {
        public static bool CheckAndCompactSession(ChatSession session, Action<ChatSession> rewriteSessionFileAction, bool force = false)
        {
            int thresholdTokens = sqlSense.Services.Configuration.SettingsManager.Current.AiAutoCompactTokens;

            // Use the shared estimation function (sqlSense formula: chars/4 + message_count)
            int estTokens = TokenAnalyzerService.EstimateTotalSessionTokens(session);

            if (estTokens > thresholdTokens || force)
            {
                int preserveRecent = 4;
                if (session.Messages.Count <= preserveRecent) return false;

                int splitPoint = session.Messages.Count - preserveRecent;
                
                // ── Tool Pair Protection (bidirectional) ──
                // Ensures we never orphan a ToolUse from its ToolResult or vice versa.
                var splitMsg = session.Messages[splitPoint];
                if (splitMsg.Role == "tool")
                {
                    // Walk backwards: include the assistant tool call in KEPT messages
                    while (splitPoint > 0 && session.Messages[splitPoint].Role == "tool")
                    {
                        splitPoint--;
                    }
                }
                else if (splitMsg.Role == "assistant" && splitMsg.ToolCalls != null && splitMsg.ToolCalls.Count > 0)
                {
                    // Walk forwards: include the tool results in SUMMARIZED messages
                    if (splitPoint + 1 < session.Messages.Count && session.Messages[splitPoint + 1].Role == "tool")
                    {
                        splitPoint++;
                        while (splitPoint < session.Messages.Count && session.Messages[splitPoint].Role == "tool")
                        {
                            splitPoint++;
                        }
                    }
                }

                if (splitPoint <= 0) return false;

                // ── Summarize messages [0..splitPoint-1] ──
                var toCompact = session.Messages.Take(splitPoint).ToList();
                int msgCount = toCompact.Count(m => m.Role != "system");

                // Extract metadata (rule-based, NOT LLM-based — per sqlSense spec)
                var toolsUsed = toCompact.Where(m => m.Role == "assistant" && m.ToolCalls != null)
                                         .SelectMany(m => m.ToolCalls.Select(tc => tc["function"]?["name"]?.ToString()))
                                         .Where(n => !string.IsNullOrEmpty(n))
                                         .Distinct().ToList();

                // Recent user requests (last 3 from the compacted window)
                var recentRequests = toCompact.Where(m => m.Role == "user").TakeLast(3).ToList();

                // Build timeline: user request → assistant action pairs
                var timelineEntries = new List<string>();
                foreach (var m in toCompact)
                {
                    if (m.Role == "user")
                    {
                        string snippet = m.Content?.Length > 80 ? m.Content.Substring(0, 80) + "..." : m.Content;
                        timelineEntries.Add($"User asked: \"{snippet?.Replace("\r\n", " ").Replace("\n", " ")}\"");
                    }
                    else if (m.Role == "assistant" && m.ToolCalls != null && m.ToolCalls.Count > 0)
                    {
                        var names = m.ToolCalls.Select(tc => tc["function"]?["name"]?.ToString()).Where(n => n != null);
                        timelineEntries.Add($"Assistant called: {string.Join(", ", names)}");
                    }
                }

                // Pending work detection: check if last assistant had an unfinished tool call
                var lastAssistant = toCompact.LastOrDefault(m => m.Role == "assistant");
                bool hasPendingWork = lastAssistant != null 
                    && lastAssistant.ToolCalls != null 
                    && lastAssistant.ToolCalls.Count > 0
                    && !toCompact.Any(m => m.Role == "tool" && m.Timestamp > lastAssistant.Timestamp);

                // ── Build structured summary ──
                var sb = new StringBuilder();
                sb.AppendLine("<summary>");
                sb.AppendLine("Conversation Summary:");
                sb.AppendLine($"- Scope: {msgCount} messages compacted");
                
                if (toolsUsed.Count > 0)
                    sb.AppendLine($"- Tools Used: {string.Join(", ", toolsUsed)}");

                sb.AppendLine("- Recent Requests:");
                int reqIdx = 1;
                foreach (var m in recentRequests)
                {
                    string reqTxt = m.Content?.Length > 200 ? m.Content.Substring(0, 200) + "..." : m.Content;
                    sb.AppendLine($"  {reqIdx++}. \"{reqTxt?.Replace("\r\n", " ").Replace("\n", " ")}\"");
                }

                if (hasPendingWork)
                {
                    sb.AppendLine("- Pending Work:");
                    sb.AppendLine("  - Waiting for tool results from last assistant action");
                }

                // Timeline (last 6 entries to keep summary compact)
                if (timelineEntries.Count > 0)
                {
                    sb.AppendLine("- Timeline:");
                    foreach (var entry in timelineEntries.TakeLast(6))
                    {
                        sb.AppendLine($"  - {entry}");
                    }
                }

                // Cumulative summarization: merge previous summary
                string previousSummary = session.Compaction?.LastSummary;
                if (!string.IsNullOrEmpty(previousSummary))
                {
                    sb.AppendLine("- Previous Summary:");
                    var match = Regex.Match(previousSummary, @"<summary>\s*(.*?)\s*</summary>", RegexOptions.Singleline);
                    if (match.Success)
                    {
                        string prevInner = match.Groups[1].Value;
                        sb.AppendLine("  " + prevInner.Trim().Replace("\r\n", "\n").Replace("\n", "\n  "));
                    }
                    else
                    {
                        sb.AppendLine("  " + previousSummary.Trim().Replace("\r\n", "\n").Replace("\n", "\n  "));
                    }
                }
                
                sb.AppendLine("</summary>");
                
                string newSummary = sb.ToString();
                
                if (session.Compaction == null) session.Compaction = new CompactionMeta();
                session.Compaction.Count++;
                session.Compaction.LastSummary = newSummary;

                // ── The Swap Operation ──
                // Delete [0..splitPoint-1], Insert summary at [0], Keep [splitPoint..end]
                var newMessages = new List<ChatMessage>
                {
                    new ChatMessage { Role = "system", Content = newSummary, Timestamp = DateTime.UtcNow }
                };
                newMessages.AddRange(session.Messages.Skip(splitPoint));

                session.Messages = newMessages;

                // Full file rewrite (not append) after compaction — per sqlSense spec
                rewriteSessionFileAction?.Invoke(session);
                return true;
            }
            return false;
        }
    }
}
