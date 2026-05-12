using System;

namespace sqlSense.Models
{
    public class ContextStats
    {
        public int TotalUsedTokens { get; set; }
        public int MaxTokens { get; set; } = 10000;
        public int ReservedTokens { get; set; } = 4096;
        
        // Layer 1: System (static, always sent)
        public int SystemInstructionsTokens { get; set; }
        public int ToolDefinitionsTokens { get; set; }

        // Layer 2: User Context (summary + user/assistant text messages)
        public int UserContextTokens { get; set; }

        // Layer 3: Tool Usage (assistant tool calls + tool result messages)
        public int ToolUsageTokens { get; set; }

        /// <summary>Fraction of MaxTokens that is used (0.0 – 1.0+)</summary>
        public double PercentageUsed => MaxTokens > 0 ? (double)TotalUsedTokens / MaxTokens : 0;

        /// <summary>Fraction of MaxTokens reserved for response (0.0 – 1.0)</summary>
        public double ReservedPercentage => MaxTokens > 0 ? (double)ReservedTokens / MaxTokens : 0;
        
        // All breakdown percentages are relative to MaxTokens (the full context window)
        // so they visually sum to PercentageUsed in the UI.
        public double SystemInstructionsPercentage => MaxTokens > 0 ? (double)SystemInstructionsTokens / MaxTokens : 0;
        public double ToolDefinitionsPercentage => MaxTokens > 0 ? (double)ToolDefinitionsTokens / MaxTokens : 0;
        public double UserContextPercentage => MaxTokens > 0 ? (double)UserContextTokens / MaxTokens : 0;
        public double ToolUsagePercentage => MaxTokens > 0 ? (double)ToolUsageTokens / MaxTokens : 0;
    }
}
