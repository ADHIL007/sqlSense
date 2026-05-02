using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace sqlSense.Services.Ai
{
    public class ModelUsageEntry
    {
        public string Model { get; set; }
        public string Provider { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ModelUsageSummary
    {
        public string Model { get; set; }
        public string Provider { get; set; }
        public int Requests { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens => PromptTokens + CompletionTokens;
        public DateTime LastUsed { get; set; }
    }

    public static class ModelUsageTracker
    {
        private static readonly string UsageDir = Path.Combine(AppConstants.AppDataFolder, "usage");

        private static readonly string UsageFile;

        static ModelUsageTracker()
        {
            if (!Directory.Exists(UsageDir))
                Directory.CreateDirectory(UsageDir);
            UsageFile = Path.Combine(UsageDir, "model_usage.jsonl");
        }

        /// <summary>
        /// Record a single AI request's token usage.
        /// </summary>
        public static void RecordUsage(string model, string provider, int promptTokens, int completionTokens)
        {
            try
            {
                var entry = new ModelUsageEntry
                {
                    Model = model,
                    Provider = provider,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    Timestamp = DateTime.UtcNow
                };
                var line = JsonConvert.SerializeObject(entry);
                File.AppendAllText(UsageFile, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelUsageTracker] Error recording usage: {ex.Message}");
            }
        }

        /// <summary>
        /// Load all usage entries from the log file.
        /// </summary>
        public static List<ModelUsageEntry> LoadAllEntries()
        {
            var entries = new List<ModelUsageEntry>();
            try
            {
                if (!File.Exists(UsageFile)) return entries;
                var lines = File.ReadAllLines(UsageFile);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var entry = JsonConvert.DeserializeObject<ModelUsageEntry>(line);
                        if (entry != null) entries.Add(entry);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelUsageTracker] Error loading entries: {ex.Message}");
            }
            return entries;
        }

        /// <summary>
        /// Get aggregated usage summaries grouped by model, optionally filtered by time range.
        /// </summary>
        public static List<ModelUsageSummary> GetSummaries(DateTime? since = null)
        {
            var entries = LoadAllEntries();
            if (since.HasValue)
                entries = entries.Where(e => e.Timestamp >= since.Value).ToList();

            return entries
                .GroupBy(e => new { e.Model, e.Provider })
                .Select(g => new ModelUsageSummary
                {
                    Model = g.Key.Model,
                    Provider = g.Key.Provider,
                    Requests = g.Count(),
                    PromptTokens = g.Sum(e => e.PromptTokens),
                    CompletionTokens = g.Sum(e => e.CompletionTokens),
                    LastUsed = g.Max(e => e.Timestamp)
                })
                .OrderByDescending(s => s.Requests)
                .ToList();
        }

        /// <summary>
        /// Export usage data as CSV string.
        /// </summary>
        public static string ExportCsv(DateTime? since = null)
        {
            var summaries = GetSummaries(since);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Model,Provider,Requests,Prompt Tokens,Completion Tokens,Total Tokens,Last Used");
            foreach (var s in summaries)
            {
                sb.AppendLine($"\"{s.Model}\",\"{s.Provider}\",{s.Requests},{s.PromptTokens},{s.CompletionTokens},{s.TotalTokens},{s.LastUsed:yyyy-MM-dd HH:mm:ss}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Clear all usage data.
        /// </summary>
        public static void ClearAll()
        {
            try
            {
                if (File.Exists(UsageFile)) File.Delete(UsageFile);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelUsageTracker] Error clearing usage: {ex.Message}");
            }
        }
    }
}
