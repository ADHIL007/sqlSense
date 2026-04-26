using sqlSense.Services.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private static async IAsyncEnumerable<string> CallOllamaStreamAsync(List<ChatMessage> history, [EnumeratorCancellation] System.Threading.CancellationToken ct)
        {
            var settings = SettingsManager.Current;
            var modelName = string.IsNullOrWhiteSpace(settings.AiModelName) ? "llama3" : settings.AiModelName;
            var isFast = settings.AiFastMode;

            object thinkValue = !isFast;
            if (modelName.Contains("gpt-oss", StringComparison.OrdinalIgnoreCase))
            {
                thinkValue = isFast ? "low" : "high";
            }

            var payload = new
            {
                model = modelName, 
                messages = history.Select(m => new { 
                    role = m.Role, 
                    content = m.Content,
                    thinking = m.Thinking,
                    tool_calls = m.ToolCalls,
                    tool_name = m.ToolName
                }).ToArray(),
                stream = true,
                think = thinkValue
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var baseUrl = string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "http://localhost:11434" : settings.AiBaseUrl;
            
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/chat") { Content = content };
            using var response = await HttpService.SendStreamAsync(request, "AI_Chat_Ollama", ct);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream, Encoding.UTF8);

            bool reasoningStarted = false;
            bool reasoningEnded = false;

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                string deltaReasoning = null;
                string deltaContentText = null;
                JArray deltaToolCalls = null;

                try {
                    var json = JObject.Parse(line);
                    var message = json["message"];
                    if (message != null)
                    {
                        deltaReasoning = message["thinking"]?.ToString();
                        deltaContentText = message["content"]?.ToString();
                        deltaToolCalls = message["tool_calls"] as JArray;
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

                if (deltaToolCalls != null && deltaToolCalls.Count > 0)
                {
                    if (reasoningStarted && !reasoningEnded) { reasoningEnded = true; yield return "\n</think>\n"; }
                    // Yield tool calls as a specialized tag that the controller could eventually parse
                    yield return $"<tool_calls>{deltaToolCalls.ToString(Formatting.None)}</tool_calls>";
                }
            }
            if (reasoningStarted && !reasoningEnded) yield return "\n</think>\n";
        }
    }
}

