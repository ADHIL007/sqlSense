using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using sqlSense.Models;
using sqlSense.Services.Ai;
using sqlSense.Services.Ai.ContextBuilders;
using System;
using System.Windows.Media;

namespace sqlSense.ViewModels
{
    public partial class ContextWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private ContextStats stats = new ContextStats();

        [ObservableProperty]
        private Brush barColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007ACC"));

        [ObservableProperty]
        private string formattedTokenUsage = "0 / 0 tokens";

        [ObservableProperty]
        private string formattedPercentage = "0%";

        [ObservableProperty]
        private string systemInstructionsPctStr = "0.0%";

        [ObservableProperty]
        private string toolDefinitionsPctStr = "0.0%";

        [ObservableProperty]
        private string messagesPctStr = "0.0%";

        [ObservableProperty]
        private string toolResultsPctStr = "0.0%";

        public ContextWindowViewModel()
        {
            UpdateStats();
        }

        [RelayCommand]
        public void UpdateStats()
        {
            Stats = TokenAnalyzerService.AnalyzeCurrentContext();

            FormattedTokenUsage = $"{FormatTokens(Stats.TotalUsedTokens)} / {FormatTokens(Stats.MaxTokens)} tokens";

            double pctRaw = Stats.PercentageUsed * 100;
            FormattedPercentage = pctRaw >= 100 ? $"{pctRaw:0.0}%" : $"{(int)pctRaw}%";

            // Breakdown percentages (each is share of MaxTokens)
            SystemInstructionsPctStr = FormatPct(Stats.SystemInstructionsPercentage);
            ToolDefinitionsPctStr = FormatPct(Stats.ToolDefinitionsPercentage);
            MessagesPctStr = FormatPct(Stats.MessagesPercentage);
            ToolResultsPctStr = FormatPct(Stats.ToolResultsPercentage);

            // Color thresholds
            double clampedPct = Math.Min(Stats.PercentageUsed, 1.0);
            if (clampedPct < 0.70)
                BarColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007ACC")); // Blue
            else if (clampedPct < 0.90)
                BarColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D7BA7D")); // Yellow
            else
                BarColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")); // Red
        }

        private static string FormatPct(double fraction)
        {
            double pct = fraction * 100;
            if (pct < 0.05) return "0.0%";
            return $"{pct:0.0}%";
        }

        private static string FormatTokens(int tokens)
        {
            if (tokens >= 1000) return (tokens / 1000.0).ToString("0.#") + "K";
            return tokens.ToString();
        }

        [RelayCommand]
        public void CompactConversation()
        {
            if (ChatSessionManager.CurrentSession != null)
            {
                ContextHandler.CheckAndCompactSession(ChatSessionManager.CurrentSession, ChatSessionManager.RewriteSessionFile, force: true);
                UpdateStats();
            }
        }
    }
}
