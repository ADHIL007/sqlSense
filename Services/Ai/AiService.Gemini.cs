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
        private static async IAsyncEnumerable<string> CallGeminiStreamAsync(string prompt, string apiKey, [EnumeratorCancellation] System.Threading.CancellationToken ct)
        {
            var payload = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var modelName = string.IsNullOrWhiteSpace(SettingsManager.Current.AiModelName) ? "gemini-pro" : SettingsManager.Current.AiModelName;
            // Strip the thinking icon if present
            modelName = modelName.Replace(" 🧠", "");
            
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:streamGenerateContent?alt=sse&key={apiKey}";
            
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            using var response = await HttpService.SendStreamAsync(request, "AI_Chat_Gemini", ct);
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
                    List<string> tokensToYield = new List<string>();
                    try {
                        var jsonStr = line.Substring(6);
                        if (jsonStr != "[DONE]") {
                            var json = JObject.Parse(jsonStr);
                            var parts = json["candidates"]?[0]?["content"]?["parts"];
                            if (parts != null) {
                                foreach (var part in parts) {
                                    bool isThought = part["thought"] != null && (part["thought"].Type == JTokenType.Boolean ? (bool)part["thought"] : false);
                                    string text = part["text"]?.ToString();
                                    
                                    // Sometimes thinking is returned as a direct 'thought' string depending on exact model version
                                    string thoughtField = part["thought"] != null && part["thought"].Type == JTokenType.String ? part["thought"].ToString() : null;
                                    
                                    string reasoningToOutput = !string.IsNullOrEmpty(thoughtField) ? thoughtField : (isThought ? text : null);
                                    
                                    if (!string.IsNullOrEmpty(reasoningToOutput))
                                    {
                                        if (!reasoningStarted) { reasoningStarted = true; tokensToYield.Add("<think>\n"); }
                                        tokensToYield.Add(reasoningToOutput);
                                    }
                                    else if (!string.IsNullOrEmpty(text))
                                    {
                                        if (reasoningStarted && !reasoningEnded) { reasoningEnded = true; tokensToYield.Add("\n</think>\n"); }
                                        tokensToYield.Add(text);
                                    }
                                }
                            }
                        }
                    } catch { }
                    foreach(var token in tokensToYield) yield return token;
                }
            }
            if (reasoningStarted && !reasoningEnded) yield return "\n</think>\n";
        }
    }
}

