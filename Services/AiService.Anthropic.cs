using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;

namespace sqlSense.Services
{
    public static partial class AiService
    {
        private static async IAsyncEnumerable<string> CallAnthropicStreamAsync(string prompt, string apiKey, [EnumeratorCancellation] System.Threading.CancellationToken ct)
        {
            var modelName = string.IsNullOrWhiteSpace(SettingsManager.Current.AiModelName) ? "claude-3-opus-20240229" : SettingsManager.Current.AiModelName;
            var payload = new
            {
                model = modelName,
                max_tokens = 1000,
                messages = new[] { new { role = "user", content = prompt } },
                stream = true
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var baseUrl = string.IsNullOrWhiteSpace(SettingsManager.Current.AiBaseUrl) ? "https://api.anthropic.com/v1" : SettingsManager.Current.AiBaseUrl;
            
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/messages") { Content = content };
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream);

            bool reasoningStarted = false;
            bool reasoningEnded = false;

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("data: "))
                {
                    string deltaReasoning = null;
                    string deltaContentText = null;
                    try {
                        var jsonStr = line.Substring(6);
                        var json = JObject.Parse(jsonStr);
                        if (json["type"]?.ToString() == "content_block_delta")
                        {
                            var deltaObj = json["delta"];
                            if (deltaObj?["type"]?.ToString() == "thinking_delta")
                            {
                                deltaReasoning = deltaObj["thinking"]?.ToString();
                            }
                            else if (deltaObj?["type"]?.ToString() == "text_delta")
                            {
                                deltaContentText = deltaObj["text"]?.ToString();
                            }
                        }
                    } catch { }
                    
                    if (!string.IsNullOrEmpty(deltaReasoning))
                    {
                        if (!reasoningStarted) { reasoningStarted = true; yield return "<think>\n"; }
                        yield return deltaReasoning;
                    }
                    if (!string.IsNullOrEmpty(deltaContentText))
                    {
                        if (reasoningStarted && !reasoningEnded) { reasoningEnded = true; yield return "\n</think>\n"; }
                        yield return deltaContentText;
                    }
                }
            }
        }
    }
}
