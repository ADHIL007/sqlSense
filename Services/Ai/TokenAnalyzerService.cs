using System;
using System.Linq;
using sqlSense.Models;

namespace sqlSense.Services.Ai
{
    public static class TokenAnalyzerService
    {
        private const int CharsPerToken = 4;

        /// <summary>
        /// Estimates token count for a single message using the sqlSense formula:
        ///   tokens ≈ (character_count / 4) + 1 (per-message structural overhead)
        /// Shared by both the analyzer and the compaction engine.
        /// </summary>
        public static int EstimateMessageTokens(ChatMessage m)
        {
            int chars = (m.Content?.Length ?? 0)
                      + (m.Thinking?.Length ?? 0)
                      + (m.ToolCalls?.ToString().Length ?? 0)
                      + (m.ToolName?.Length ?? 0)
                      + (m.ToolCallId?.Length ?? 0);
            return (chars / CharsPerToken) + 1; // +1 = per-message structural overhead (role, JSON framing)
        }

        /// <summary>
        /// Estimates the total token footprint of the system prompt + tool schemas.
        /// These are sent on every request regardless of conversation length.
        /// </summary>
        public static int EstimateSystemTokens()
        {
            var sysInst = new SystemInstruction();
            return sysInst.GetSystemInstruction().Length / CharsPerToken;
        }

        public static int EstimateToolDefinitionTokens()
        {
            return AiToolRegistry.GetAvailableTools().ToString(Newtonsoft.Json.Formatting.None).Length / CharsPerToken;
        }

        /// <summary>
        /// Estimates total context usage for the current session.
        /// Matches sqlSense formula: Estimated Tokens ≈ (Character Count / 4) + Message Count
        /// </summary>
        public static ContextStats AnalyzeCurrentContext(int maxTokens = 10000, int reservedResponseTokens = 4096)
        {
            int systemInstructionTokens = EstimateSystemTokens();
            int toolDefinitionTokens = EstimateToolDefinitionTokens();

            var session = ChatSessionManager.CurrentSession;
            if (session == null || session.Messages == null || session.Messages.Count == 0)
            {
                return new ContextStats
                {
                    TotalUsedTokens = systemInstructionTokens + toolDefinitionTokens,
                    MaxTokens = maxTokens,
                    ReservedTokens = reservedResponseTokens,
                    SystemInstructionsTokens = systemInstructionTokens,
                    ToolDefinitionsTokens = toolDefinitionTokens,
                    UserContextTokens = 0,
                    ToolUsageTokens = 0
                };
            }

            int userContextTokens = 0;  // Summary + user messages + assistant text responses
            int toolUsageTokens = 0;    // Assistant tool calls + tool result messages

            foreach (var m in session.Messages)
            {
                int msgTokens = EstimateMessageTokens(m);

                if (m.Role == "tool")
                {
                    // Tool Result message → Tool Usage bucket
                    toolUsageTokens += msgTokens;
                }
                else if (m.Role == "assistant" && m.ToolCalls != null && m.ToolCalls.Count > 0)
                {
                    // Assistant ToolUse message → split:
                    //   Tool call JSON + thinking → Tool Usage
                    //   Any plain-text content → User Context
                    int toolCallChars = (m.ToolCalls?.ToString().Length ?? 0) + (m.Thinking?.Length ?? 0);
                    int contentChars = (m.Content?.Length ?? 0);
                    toolUsageTokens += (toolCallChars / CharsPerToken) + 1;
                    if (contentChars > 0)
                        userContextTokens += contentChars / CharsPerToken;
                }
                else
                {
                    // user, assistant (text-only), system (compaction summary) → User Context
                    userContextTokens += msgTokens;
                }
            }

            int totalUsed = systemInstructionTokens + toolDefinitionTokens + userContextTokens + toolUsageTokens;

            return new ContextStats
            {
                TotalUsedTokens = totalUsed,
                MaxTokens = maxTokens,
                ReservedTokens = reservedResponseTokens,
                SystemInstructionsTokens = systemInstructionTokens,
                ToolDefinitionsTokens = toolDefinitionTokens,
                UserContextTokens = userContextTokens,
                ToolUsageTokens = toolUsageTokens
            };
        }

        /// <summary>
        /// Quick estimation used by the compaction engine.
        /// Matches sqlSense: (total chars / 4) + message count + system overhead.
        /// </summary>
        public static int EstimateTotalSessionTokens(ChatSession session)
        {
            int systemTokens = EstimateSystemTokens() + EstimateToolDefinitionTokens();
            int msgTokens = session.Messages.Sum(m => EstimateMessageTokens(m));
            return systemTokens + msgTokens;
        }
    }
}
