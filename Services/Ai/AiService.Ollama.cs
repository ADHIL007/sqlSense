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

            // Build clean message objects — only include fields that have values.
            // Ollama does NOT expect thinking/tool_calls/tool_name on user messages.
            var messages = new JArray();
            foreach (var m in history)
            {
                var msg = new JObject
                {
                    ["role"] = m.Role,
                    ["content"] = m.Content ?? ""
                };
                if (!string.IsNullOrEmpty(m.Thinking))
                    msg["thinking"] = m.Thinking;
                if (m.ToolCalls != null && m.ToolCalls.Count > 0)
                    msg["tool_calls"] = m.ToolCalls;
                if (!string.IsNullOrEmpty(m.ToolName))
                    msg["tool_name"] = m.ToolName;
                if (!string.IsNullOrEmpty(m.ToolCallId))
                    msg["tool_call_id"] = m.ToolCallId;
                messages.Add(msg);
            }

            var payloadObj = new JObject
            {
                ["model"] = modelName,
                ["messages"] = messages,
                ["stream"] = true,
                ["think"] = JToken.FromObject(thinkValue),
                ["tools"] = AiToolRegistry.GetAvailableTools()
            };

            if (settings.AiMaxTokens > 0)
            {
                payloadObj["options"] = new JObject
                {
                    ["num_predict"] = settings.AiMaxTokens
                };
            }

            var jsonPayload = payloadObj.ToString(Formatting.None);
            System.Diagnostics.Debug.WriteLine($"[Ollama] Payload: {jsonPayload}");

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
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

                System.Diagnostics.Debug.WriteLine($"[Ollama] Chunk: {line}");
                
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
                    // Ollama's final chunk (done=true) contains real token counts
                    if (json["done"]?.Value<bool>() == true)
                    {
                        var promptEval = json["prompt_eval_count"]?.Value<int>();
                        var evalCount = json["eval_count"]?.Value<int>();
                        if (promptEval.HasValue) _lastPromptTokens = promptEval.Value;
                        if (evalCount.HasValue) _lastCompletionTokens = evalCount.Value;
                        if (promptEval.HasValue || evalCount.HasValue) _lastUsageAvailable = true;
                    }
                } catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"[Ollama] Parse error: {ex.Message}");
                }

                // Follow Ollama docs if/elif pattern: thinking and content
                // are mutually exclusive per chunk. Check non-empty to avoid
                // empty content="" during thinking from ending the trace.
                if (!string.IsNullOrEmpty(deltaReasoning))
                {
                    if (!reasoningStarted) 
                    { 
                        reasoningStarted = true; 
                        yield return "<think>\n"; 
                    }
                    yield return deltaReasoning;
                }
                else if (!string.IsNullOrEmpty(deltaContentText))
                {
                    if (reasoningStarted && !reasoningEnded) 
                    { 
                        reasoningEnded = true; 
                        yield return "\n</think>\n"; 
                    }
                    yield return deltaContentText;
                }
                else if (deltaToolCalls != null && deltaToolCalls.Count > 0)
                {
                    if (reasoningStarted && !reasoningEnded) { reasoningEnded = true; yield return "\n</think>\n"; }
                    yield return $"<tool_calls>{deltaToolCalls.ToString(Formatting.None)}</tool_calls>";
                }
            }
            if (reasoningStarted && !reasoningEnded) yield return "\n</think>\n";
        }
    }
}

