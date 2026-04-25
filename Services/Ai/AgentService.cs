using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sqlSense.Services.Ai
{
    public static class AgentService
    {
        public static async IAsyncEnumerable<string> ProcessStreamAsync(IAsyncEnumerable<string> rawStream, bool isFastMode, [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default)
        {
            if (!isFastMode)
            {
                await foreach (var chunk in rawStream.WithCancellation(cancellationToken))
                {
                    yield return chunk;
                }
                yield break;
            }

            bool isThinking = false;
            string buffer = "";
            bool hasYieldedError = false;

            await using var enumerator = rawStream.GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                bool success = false;
                Exception loopEx = null;
                try
                {
                    success = await enumerator.MoveNextAsync();
                }
                catch (Exception e)
                {
                    loopEx = e;
                    System.Diagnostics.Debug.WriteLine($"[AgentService] Stream Exception: {e}");
                }

                if (loopEx != null)
                {
                    if (!hasYieldedError)
                    {
                        yield return $"\n[Stream Error: {loopEx.Message}]";
                        hasYieldedError = true;
                    }
                    break;
                }

                if (!success) break;

                var chunk = enumerator.Current;
                System.Diagnostics.Debug.WriteLine($"[AgentService] Chunk received: {chunk?.Replace("\n", "\\n").Replace("\r", "")}");

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
                            if (idx > 0) yield return process.Substring(0, idx);
                            process = process.Substring(idx + 7);
                        }
                        else
                        {
                            int pIdx = -1;
                            for (int i = 1; i <= 6 && i <= process.Length; i++)
                            {
                                if ("<think>".StartsWith(process.Substring(process.Length - i)))
                                {
                                    pIdx = process.Length - i;
                                    break;
                                }
                            }
                            if (pIdx >= 0)
                            {
                                if (pIdx > 0) yield return process.Substring(0, pIdx);
                                buffer = process.Substring(pIdx);
                                process = "";
                            }
                            else
                            {
                                yield return process;
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
                            process = process.Substring(idx + 8);
                        }
                        else
                        {
                            int pIdx = -1;
                            for (int i = 1; i <= 7 && i <= process.Length; i++)
                            {
                                if ("</think>".StartsWith(process.Substring(process.Length - i)))
                                {
                                    pIdx = process.Length - i;
                                    break;
                                }
                            }
                            if (pIdx >= 0)
                            {
                                buffer = process.Substring(pIdx);
                            }
                            process = "";
                        }
                    }
                }
            }
            if (!isThinking && buffer.Length > 0 && buffer != "<think" && buffer != "</think") 
            {
                yield return buffer;
            }
        }
    }
}
