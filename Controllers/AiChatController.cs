using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using sqlSense.Services.Ai;
using sqlSense.Services;
using Newtonsoft.Json.Linq;

namespace sqlSense.Controllers
{
    public class AiChatController
    {
        private CancellationTokenSource? _cts;
        public bool IsStreaming { get; private set; }

        public void StopStreaming()
        {
            _cts?.Cancel();
            IsStreaming = false;
        }

        public async Task SendMessageStreamAsync(string text, 
            Action onStart,
            Action<string> onThinkChunk, 
            Action<string> onTextChunk,
            Action<double> onThinkComplete,
            Action onComplete,
            Action<Exception> onError,
            bool isToolRecursion = false)
        {
            if (string.IsNullOrWhiteSpace(text) && !isToolRecursion) return;

            if (!string.IsNullOrWhiteSpace(text) && !isToolRecursion)
            {
                ChatSessionManager.AddMessage("user", text);
            }
            if (!isToolRecursion)
            {
                IsStreaming = true;
                _cts = new CancellationTokenSource();
                onStart();
            }

            string buffer = "";
            string thinkTextBuffer = "";
            string currentTextBuffer = "";
            string toolCallsJsonBuffer = "";

            try
            {
                bool isThinking = false;
                Stopwatch sw = new Stopwatch();

                await foreach (var chunk in AiService.SendMessageStreamAsync(text, _cts.Token))
                {
                    // Force yield to the UI message pump so it can render the updates
                    await Task.Yield();

                    string process = buffer + chunk;
                    buffer = "";

                    // Extract tool calls if present
                    while (process.Contains("<tool_calls>"))
                    {
                        int start = process.IndexOf("<tool_calls>");
                        int end = process.IndexOf("</tool_calls>", start);
                        if (end >= 0)
                        {
                            string json = process.Substring(start + 12, end - (start + 12));
                            toolCallsJsonBuffer += (toolCallsJsonBuffer.Length > 0 ? "," : "") + json.Trim('[', ']');
                            process = process.Remove(start, (end + 13) - start);
                        }
                        else break; // Wait for end tag
                    }

                    while (process.Length > 0)
                    {
                        if (!isThinking)
                        {
                            int idx = process.IndexOf("<think>");
                            if (idx >= 0)
                            {
                                isThinking = true;
                                if (idx > 0)
                                {
                                    string textPart = process.Substring(0, idx);
                                    currentTextBuffer += textPart;
                                    onTextChunk(textPart);
                                }
                                sw.Restart();
                                process = process.Substring(idx + 7).TrimStart('\r', '\n');
                            }
                            else
                            {
                                // Check for partial <think> at end of buffer
                                int pIdx = -1;
                                for (int i = 1; i <= 6 && i <= process.Length; i++) {
                                    if ("<think>".StartsWith(process.Substring(process.Length - i))) { pIdx = process.Length - i; break; }
                                }
                                if (pIdx >= 0) {
                                    if (pIdx > 0) {
                                        string textPart = process.Substring(0, pIdx);
                                        currentTextBuffer += textPart;
                                        onTextChunk(textPart);
                                    }
                                    buffer = process.Substring(pIdx);
                                    process = "";
                                } else {
                                    currentTextBuffer += process;
                                    onTextChunk(process);
                                    process = "";
                                }
                            }
                        }
                        else
                        {
                            int idx = process.IndexOf("</think>");
                            if (idx >= 0)
                            {
                                // End of thinking — yield remaining think text, then signal complete
                                isThinking = false;
                                sw.Stop();
                                string thinkPart = process.Substring(0, idx).TrimEnd('\r', '\n');
                                if (thinkPart.Length > 0)
                                {
                                    thinkTextBuffer += thinkPart;
                                    onThinkChunk(thinkPart);
                                }
                                onThinkComplete(sw.Elapsed.TotalSeconds);
                                process = process.Substring(idx + 8).TrimStart('\r', '\n');
                            }
                            else
                            {
                                // Check for partial </think> at end of buffer
                                int pIdx = -1;
                                for (int i = 1; i <= 7 && i <= process.Length; i++) {
                                    if ("</think>".StartsWith(process.Substring(process.Length - i))) { pIdx = process.Length - i; break; }
                                }
                                if (pIdx >= 0) {
                                    if (pIdx > 0) {
                                        // Stream thinking chunk immediately
                                        string thinkPart = process.Substring(0, pIdx);
                                        thinkTextBuffer += thinkPart;
                                        onThinkChunk(thinkPart);
                                    }
                                    buffer = process.Substring(pIdx);
                                    process = "";
                                } else {
                                    // Stream every thinking chunk as it arrives
                                    thinkTextBuffer += process;
                                    onThinkChunk(process);
                                    process = "";
                                }
                            }
                        }
                    }
                }

                Newtonsoft.Json.Linq.JArray toolCalls = null;
                if (!string.IsNullOrEmpty(toolCallsJsonBuffer))
                {
                    try { toolCalls = Newtonsoft.Json.Linq.JArray.Parse("[" + toolCallsJsonBuffer + "]"); } catch { }
                }

                if (toolCalls != null && toolCalls.Count > 0)
                {
                    ChatSessionManager.AddMessage("assistant", currentTextBuffer, thinkTextBuffer, toolCalls);
                    foreach (var tc in toolCalls)
                    {
                        var funcName = tc["function"]?["name"]?.ToString();
                        var callId = tc["id"]?.ToString();
                        var argsToken = tc["function"]?["arguments"];
                        
                        // Emit LOADING status
                        string friendlyName = AiToolStatusManager.GetFriendlyName(funcName);
                        onTextChunk(AiToolStatusManager.GetStatusTag(callId ?? funcName, ToolStatusState.Loading, $"Checking {friendlyName}..."));
                        
                        JObject argsObj = null;
                        if (argsToken != null)
                        {
                            if (argsToken.Type == JTokenType.String)
                            {
                                try { argsObj = JObject.Parse(argsToken.ToString()); } catch { }
                            }
                            else if (argsToken.Type == JTokenType.Object)
                            {
                                argsObj = argsToken as JObject;
                            }
                        }
                        
                        try
                        {
                            string result = AiToolRegistry.ExecuteTool(funcName, argsObj);
                            
                            // Emit SUCCESS status
                            onTextChunk(AiToolStatusManager.GetStatusTag(callId ?? funcName, ToolStatusState.Success, $"{friendlyName} retrieved"));
                            
                            ChatSessionManager.AddMessage("tool", result, null, null, funcName, callId);
                        }
                        catch (Exception ex)
                        {
                            // Emit ERROR status
                            onTextChunk(AiToolStatusManager.GetStatusTag(callId ?? funcName, ToolStatusState.Error, $"{friendlyName} failed"));
                            
                            ChatSessionManager.AddMessage("tool", $"Error executing tool {funcName}: {ex.Message}", null, null, funcName, callId);
                        }
                    }
                    await SendMessageStreamAsync(null, onStart, onThinkChunk, onTextChunk, onThinkComplete, onComplete, onError, true);
                    return; // Important: let the recursive call trigger onComplete
                }
                
                if (!isToolRecursion || !string.IsNullOrEmpty(currentTextBuffer) || !string.IsNullOrEmpty(thinkTextBuffer))
                {
                    ChatSessionManager.AddMessage("assistant", currentTextBuffer, thinkTextBuffer, toolCalls);
                }
                
                onComplete();
            }
            catch (OperationCanceledException) 
            {
                // When cancelled by user, still save the partial message and signal completion to reset UI
                Newtonsoft.Json.Linq.JArray toolCalls = null;
                if (!string.IsNullOrEmpty(toolCallsJsonBuffer))
                {
                    try { toolCalls = Newtonsoft.Json.Linq.JArray.Parse("[" + toolCallsJsonBuffer + "]"); } catch { }
                }

                string cancelText = currentTextBuffer.Length > 0 ? "\n\n*[User canceled the request]*" : "*[User canceled the request]*";
                currentTextBuffer += cancelText;
                onTextChunk(cancelText);

                ChatSessionManager.AddMessage("assistant", currentTextBuffer, thinkTextBuffer, toolCalls);
                onComplete();
            }
            catch (Exception ex)
            {
                onError(ex);
            }
            finally
            {
                if (!isToolRecursion)
                {
                    IsStreaming = false;
                }
            }
        }
    }
}
