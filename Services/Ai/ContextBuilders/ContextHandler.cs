using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sqlSense.Services.Ai.ContextBuilders
{
    public static class ContextHandler
    {
        public static bool CheckAndCompactSession(ChatSession session, Action<ChatSession> rewriteSessionFileAction, bool force = false)
        {
            int thresholdTokens = 10000;
            var envThreshold = Environment.GetEnvironmentVariable("CLAUDE_CODE_AUTO_COMPACT_INPUT_TOKENS");
            if (int.TryParse(envThreshold, out int parsedThreshold)) thresholdTokens = parsedThreshold;

            // Better Token Estimation: includes actual system prompt, tool schemas, and JSON overhead
            var sysInst = new SystemInstruction();
            int systemPromptOffset = sysInst.GetSystemInstruction().Length / 4;
            systemPromptOffset += AiToolRegistry.GetAvailableTools().ToString(Newtonsoft.Json.Formatting.None).Length / 4;
            int estTokens = systemPromptOffset + (session.Messages.Count * 20);
            estTokens += session.Messages.Sum(m => 
                ((m.Content?.Length ?? 0) + 
                 (m.Thinking?.Length ?? 0) + 
                 (m.ToolCalls?.ToString().Length ?? 0) * 2 + // Weight JSON payload
                 (m.ToolName?.Length ?? 0)) / 4);

            if (estTokens > thresholdTokens || force)
            {
                int preserveRecent = 4;
                if (session.Messages.Count <= preserveRecent) return false;

                int splitPoint = session.Messages.Count - preserveRecent;
                
                // Protect pairs: ensure Assistant (ToolUse) and Tool Results are not orphaned
                var splitMsg = session.Messages[splitPoint];
                if (splitMsg.Role == "tool")
                {
                    // Walk backwards to include the assistant call in the KEPT messages
                    while (splitPoint > 0 && session.Messages[splitPoint].Role == "tool")
                    {
                        splitPoint--;
                    }
                }
                else if (splitMsg.Role == "assistant" && splitMsg.ToolCalls != null && splitMsg.ToolCalls.Count > 0)
                {
                    // Walk forwards to include the tool results in the SUMMARIZED messages
                    if (splitPoint + 1 < session.Messages.Count && session.Messages[splitPoint + 1].Role == "tool")
                    {
                        splitPoint++;
                        while (splitPoint < session.Messages.Count && session.Messages[splitPoint].Role == "tool")
                        {
                            splitPoint++;
                        }
                    }
                }

                if (splitPoint <= 0) return false; // Cannot compact without breaking pairs or preserving minimum

                // Summarize 0 to splitPoint-1
                var toCompact = session.Messages.Take(splitPoint).ToList();
                int msgCount = toCompact.Count(m => m.Role != "system"); // Exclude prior summary

                var toolsUsed = toCompact.Where(m => m.Role == "assistant" && m.ToolCalls != null)
                                         .SelectMany(m => m.ToolCalls.Select(tc => tc["function"]?["name"]?.ToString()))
                                         .Where(n => !string.IsNullOrEmpty(n))
                                         .Distinct().ToList();

                var filesReferenced = toCompact.Where(m => m.Role == "tool")
                                               .Select(m => m.ToolName) // Approximating files referenced via tools
                                               .Where(n => !string.IsNullOrEmpty(n))
                                               .Distinct().ToList();

                var summaryBuilder = new System.Text.StringBuilder();
                summaryBuilder.AppendLine("<summary>");
                summaryBuilder.AppendLine("Conversation Summary:");
                summaryBuilder.AppendLine($"- Scope: {msgCount} messages compacted");
                if (toolsUsed.Count > 0)
                    summaryBuilder.AppendLine($"- Tools Used: {string.Join(", ", toolsUsed)}");
                
                summaryBuilder.AppendLine("- Recent Requests:");
                int reqCount = 1;
                foreach (var m in toCompact.Where(m => m.Role == "user").TakeLast(2))
                {
                    string reqTxt = m.Content?.Length > 200 ? m.Content.Substring(0, 200) + "..." : m.Content;
                    summaryBuilder.AppendLine($"  {reqCount++}. \"{reqTxt?.Replace("\r\n", " ").Replace("\n", " ")}\"");
                }

                string previousSummary = session.Compaction?.LastSummary;
                if (!string.IsNullOrEmpty(previousSummary))
                {
                    summaryBuilder.AppendLine("- Previous Summary:");
                    var match = System.Text.RegularExpressions.Regex.Match(previousSummary, @"<summary>\s*(.*?)\s*</summary>", System.Text.RegularExpressions.RegexOptions.Singleline);
                    if (match.Success)
                    {
                        string prevInner = match.Groups[1].Value;
                        summaryBuilder.AppendLine("  " + prevInner.Trim().Replace("\r\n", "\n").Replace("\n", "\n  "));
                    }
                    else
                    {
                        summaryBuilder.AppendLine("  " + previousSummary.Trim().Replace("\r\n", "\n").Replace("\n", "\n  "));
                    }
                }
                
                summaryBuilder.AppendLine("</summary>");
                
                string newSummary = summaryBuilder.ToString();
                
                if (session.Compaction == null) session.Compaction = new CompactionMeta();
                session.Compaction.Count++;
                session.Compaction.LastSummary = newSummary;

                var newMessages = new List<ChatMessage>
                {
                    new ChatMessage { Role = "system", Content = newSummary, Timestamp = DateTime.UtcNow }
                };
                newMessages.AddRange(session.Messages.Skip(splitPoint));

                session.Messages = newMessages;

                rewriteSessionFileAction?.Invoke(session);
                return true;
            }
            return false;
        }
    }
}
