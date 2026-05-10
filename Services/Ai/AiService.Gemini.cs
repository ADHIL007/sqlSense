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
        private static async IAsyncEnumerable<string> CallGeminiStreamAsync(string systemInstruction, string prompt, string apiKey, [EnumeratorCancellation] System.Threading.CancellationToken ct)
        {
            var payloadObj = new JObject
            {
                ["systemInstruction"] = new JObject
                {
                    ["parts"] = new JArray { new JObject { ["text"] = systemInstruction } }
                },
                ["contents"] = JArray.FromObject(new[] { new { role = "user", parts = new[] { new { text = prompt } } } })
            };
            if (SettingsManager.Current.AiMaxTokens > 0)
            {
                payloadObj["generationConfig"] = new JObject
                {
                    ["maxOutputTokens"] = SettingsManager.Current.AiMaxTokens,
                    ["temperature"] = 0.5
                };
            }
            else
            {
                payloadObj["generationConfig"] = new JObject
                {
                    ["temperature"] = 0.5
                };
            }

            var availableTools = AiToolRegistry.GetAvailableTools();
            if (availableTools != null && availableTools.Count > 0)
            {
                var geminiTools = new JArray();
                var functionDeclarations = new JArray();
                foreach (var tool in availableTools)
                {
                    var func = tool["function"];
                    if (func != null)
                    {
                        var declaration = new JObject
                        {
                            ["name"] = func["name"],
                            ["description"] = func["description"],
                            ["parameters"] = func["parameters"]
                        };
                        functionDeclarations.Add(declaration);
                    }
                }
                geminiTools.Add(new JObject { ["functionDeclarations"] = functionDeclarations });
                payloadObj["tools"] = geminiTools;
            }

            var content = new StringContent(payloadObj.ToString(Formatting.None), Encoding.UTF8, "application/json");
            var modelName = string.IsNullOrWhiteSpace(SettingsManager.Current.AiModelName) ? "gemini-pro" : SettingsManager.Current.AiModelName;
            // Strip the thinking icon if present
            modelName = modelName.Replace(" 🧠", "");
            
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:streamGenerateContent?alt=sse&key={apiKey}";
            
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            using var response = await HttpService.SendStreamAsync(request, "AI_Chat_Gemini", ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                string errMsg = response.ReasonPhrase;
                try
                {
                    var errJson = JObject.Parse(errorBody);
                    if (errJson["error"] != null && errJson["error"]["message"] != null)
                    {
                        errMsg = errJson["error"]["message"].ToString();
                    }
                }
                catch { errMsg = string.IsNullOrWhiteSpace(errorBody) ? errMsg : errorBody; }

                if (errMsg != null && (errMsg.IndexOf("tool", StringComparison.OrdinalIgnoreCase) >= 0 || errMsg.IndexOf("function", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    throw new Exception($"The selected model does not support tools/function calling. Please select a different model. (Details: {errMsg})");
                }
                throw new Exception($"API Error {(int)response.StatusCode}: {errMsg}");
            }

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
                                    else if (part["functionCall"] != null)
                                    {
                                        if (reasoningStarted && !reasoningEnded) { reasoningEnded = true; tokensToYield.Add("\n</think>\n"); }
                                        string funcName = part["functionCall"]["name"]?.ToString();
                                        string argsStr = part["functionCall"]["args"]?.ToString(Formatting.None) ?? "{}";
                                        string callId = "call_" + Guid.NewGuid().ToString().Substring(0, 8);
                                        
                                        var toolCallObj = new JObject
                                        {
                                            ["id"] = callId,
                                            ["type"] = "function",
                                            ["function"] = new JObject
                                            {
                                                ["name"] = funcName,
                                                ["arguments"] = argsStr
                                            }
                                        };
                                        tokensToYield.Add($"<tool_calls>[{toolCallObj.ToString(Formatting.None)}]</tool_calls>");
                                    }
                                }
                            }
                            // Parse real usage from Gemini's usageMetadata
                            var usageMeta = json["usageMetadata"];
                            if (usageMeta != null)
                            {
                                var pt = usageMeta["promptTokenCount"]?.Value<int>();
                                var ct2 = usageMeta["candidatesTokenCount"]?.Value<int>();
                                if (pt.HasValue) _lastPromptTokens = pt.Value;
                                if (ct2.HasValue) _lastCompletionTokens = ct2.Value;
                                if (pt.HasValue || ct2.HasValue) _lastUsageAvailable = true;
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

