using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using sqlSense.Services.Ai;
using sqlSense.Services;

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
            Action<Exception> onError)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            ChatSessionManager.AddMessage("user", text);
            IsStreaming = true;
            _cts = new CancellationTokenSource();
            onStart();

            try
            {
                string buffer = "";
                string thinkTextBuffer = "";
                string currentTextBuffer = "";
                bool isThinking = false;
                Stopwatch sw = new Stopwatch();

                await foreach (var chunk in AiService.SendMessageStreamAsync(text, _cts.Token))
                {
                    string process = buffer + chunk;
                    buffer = "";

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
                                isThinking = false;
                                sw.Stop();
                                string thinkPart = process.Substring(0, idx).TrimEnd('\r', '\n');
                                thinkTextBuffer += thinkPart;
                                onThinkChunk(thinkPart);
                                onThinkComplete(sw.Elapsed.TotalSeconds);
                                process = process.Substring(idx + 8).TrimStart('\r', '\n');
                            }
                            else
                            {
                                int pIdx = -1;
                                for (int i = 1; i <= 7 && i <= process.Length; i++) {
                                    if ("</think>".StartsWith(process.Substring(process.Length - i))) { pIdx = process.Length - i; break; }
                                }
                                if (pIdx >= 0) {
                                    if (pIdx > 0) {
                                        string thinkPart = process.Substring(0, pIdx);
                                        thinkTextBuffer += thinkPart;
                                        onThinkChunk(thinkPart);
                                    }
                                    buffer = process.Substring(pIdx);
                                    process = "";
                                } else {
                                    thinkTextBuffer += process;
                                    onThinkChunk(process);
                                    process = "";
                                }
                            }
                        }
                    }
                }

                string finalContent = "";
                if (!string.IsNullOrEmpty(thinkTextBuffer)) finalContent += $"<think>\n{thinkTextBuffer}\n</think>\n\n";
                finalContent += currentTextBuffer;
                ChatSessionManager.AddMessage("assistant", finalContent);
                onComplete();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                onError(ex);
            }
            finally
            {
                IsStreaming = false;
            }
        }
    }
}
