using sqlSense.Services.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using sqlSense.Services.Http;


namespace sqlSense.Services.Ai
{
    public static partial class AiService
    {
        private static async IAsyncEnumerable<string> CallOpenAiStreamAsync(string prompt, string apiKey, [EnumeratorCancellation] System.Threading.CancellationToken ct)
        {
            var modelName = string.IsNullOrWhiteSpace(SettingsManager.Current.AiModelName) ? "gpt-3.5-turbo" : SettingsManager.Current.AiModelName;
            var settings = SettingsManager.Current;
            var isFast = settings.AiFastMode;
            
            var payload = new JObject
            {
                ["model"] = modelName,
                ["messages"] = JArray.FromObject(new[] { new { role = "user", content = prompt } }),
                ["stream"] = true,
                ["temperature"] = isFast ? 0.7 : 1.0,
                ["max_tokens"] = isFast ? 300 : 2000
            };

            // Dynamic thinking/reasoning parameters based on Fast Mode
            payload["extra_body"] = new JObject
            {
                ["chat_template_kwargs"] = new JObject
                {
                    ["thinking"] = !isFast,
                    ["reasoning_effort"] = isFast ? "low" : "high"
                }
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var baseUrl = string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "https://api.openai.com/v1" : settings.AiBaseUrl;
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions") { Content = content };
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            using var response = await HttpService.SendStreamAsync(request, "AI_Chat_OpenAI", ct);
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
                        var delta = json["choices"]?[0]?["delta"];
                        if (delta != null)
                        {
                            // Support both "reasoning_content" and "reasoning"
                            deltaReasoning = delta["reasoning_content"]?.ToString() ?? delta["reasoning"]?.ToString();
                            deltaContentText = delta["content"]?.ToString();
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
            if (reasoningStarted && !reasoningEnded) yield return "\n</think>\n";
        }
    }
}

