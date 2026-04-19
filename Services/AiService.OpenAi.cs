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
        private static async IAsyncEnumerable<string> CallOpenAiStreamAsync(string prompt, string apiKey, [EnumeratorCancellation] System.Threading.CancellationToken ct)
        {
            var modelName = string.IsNullOrWhiteSpace(SettingsManager.Current.AiModelName) ? "gpt-3.5-turbo" : SettingsManager.Current.AiModelName;
            var payload = new
            {
                model = modelName,
                messages = new[] { new { role = "user", content = prompt } },
                stream = true
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var baseUrl = string.IsNullOrWhiteSpace(SettingsManager.Current.AiBaseUrl) ? "https://api.openai.com/v1" : SettingsManager.Current.AiBaseUrl;
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions") { Content = content };
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

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
                if (line.StartsWith("data: ") && !line.Contains("[DONE]"))
                {
                    string jsonStr = line.Substring(6);
                    string deltaReasoning = null;
                    string deltaContentText = null;
                    try {
                        var json = JObject.Parse(jsonStr);
                        deltaReasoning = json["choices"]?[0]?["delta"]?["reasoning_content"]?.ToString();
                        deltaContentText = json["choices"]?[0]?["delta"]?["content"]?.ToString();
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
