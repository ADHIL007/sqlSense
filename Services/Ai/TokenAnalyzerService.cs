using System;
using System.Linq;
using sqlSense.Models;

namespace sqlSense.Services.Ai
{
    public static class TokenAnalyzerService
    {
        private const int CharsPerToken = 4;

        public static ContextStats AnalyzeCurrentContext(int maxTokens = 10000, int reservedResponseTokens = 4096)
        {
            var session = ChatSessionManager.CurrentSession;
            if (session == null || session.Messages == null || session.Messages.Count == 0)
            {
                // Even with no conversation, system + tools still consume tokens
                var sysInstEmpty = new SystemInstruction();
                int sysT = sysInstEmpty.GetSystemInstruction().Length / CharsPerToken;
                int toolT = AiToolRegistry.GetAvailableTools().ToString(Newtonsoft.Json.Formatting.None).Length / CharsPerToken;
                return new ContextStats
                {
                    TotalUsedTokens = sysT + toolT,
                    MaxTokens = maxTokens,
                    ReservedTokens = reservedResponseTokens,
                    SystemInstructionsTokens = sysT,
                    ToolDefinitionsTokens = toolT,
                    MessagesTokens = 0,
                    ToolResultsTokens = 0
                };
            }

            // Real system prompt and tool schema token counts
            var sysInst = new SystemInstruction();
            int systemInstructionTokens = sysInst.GetSystemInstruction().Length / CharsPerToken;
            int toolDefinitionTokens = AiToolRegistry.GetAvailableTools().ToString(Newtonsoft.Json.Formatting.None).Length / CharsPerToken;

            int messagesTokens = 0;
            int toolResultTokens = 0;

            foreach (var m in session.Messages)
            {
                int contentTokens = (m.Content?.Length ?? 0) / CharsPerToken;
                int thinkingTokens = (m.Thinking?.Length ?? 0) / CharsPerToken;
                int toolCallTokens = (m.ToolCalls?.ToString().Length ?? 0) / CharsPerToken;
                int overhead = 4; // Per-message JSON framing overhead (role, separators, etc.)

                int msgTotal = contentTokens + thinkingTokens + toolCallTokens + overhead;

                if (m.Role == "tool")
                {
                    toolResultTokens += msgTotal;
                }
                else
                {
                    messagesTokens += msgTotal;
                }
            }

            int totalUsed = systemInstructionTokens + toolDefinitionTokens + messagesTokens + toolResultTokens;

            return new ContextStats
            {
                TotalUsedTokens = totalUsed,
                MaxTokens = maxTokens,
                ReservedTokens = reservedResponseTokens,
                SystemInstructionsTokens = systemInstructionTokens,
                ToolDefinitionsTokens = toolDefinitionTokens,
                MessagesTokens = messagesTokens,
                ToolResultsTokens = toolResultTokens
            };
        }
    }
}
