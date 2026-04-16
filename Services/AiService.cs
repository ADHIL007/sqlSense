using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;

namespace sqlSense.Services
{
    public static class AiService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<List<string>> FetchAvailableModelsAsync(string provider, string apiKey, string baseUrl)
        {
            var models = new List<string>();
            try
            {
                if (provider == "OpenAI")
                {
                    string url = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.openai.com/v1/models" : $"{baseUrl.TrimEnd('/')}/models";
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Authorization", $"Bearer {apiKey}");
                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                        if (json["data"] != null)
                        {
                            foreach (var item in json["data"]) models.Add(item["id"].ToString());
                        }
                    }
                }
                else if (provider == "Google Gemini")
                {
                    string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                        if (json["models"] != null)
                        {
                            foreach (var item in json["models"]) models.Add(item["name"].ToString().Replace("models/", ""));
                        }
                    }
                }
                else if (provider == "Microsoft Azure OpenAI")
                {
                    if (string.IsNullOrWhiteSpace(baseUrl)) throw new Exception("Base URL is required for Azure");
                    string url = $"{baseUrl.TrimEnd('/')}/openai/deployments?api-version=2023-05-15";
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("api-key", apiKey);
                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                        if (json["data"] != null)
                        {
                            foreach (var item in json["data"]) models.Add(item["id"].ToString());
                        }
                    }
                }
                else if (provider == "Local Model (Ollama)")
                {
                    string url = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:11434/api/tags" : $"{baseUrl.TrimEnd('/')}/api/tags";
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                        if (json["models"] != null)
                        {
                            foreach (var item in json["models"]) models.Add(item["name"].ToString());
                        }
                    }
                }
                else if (provider == "Anthropic Claude")
                {
                    models.AddRange(new[] { "claude-3-5-sonnet-20240620", "claude-3-opus-20240229", "claude-3-sonnet-20240229", "claude-3-haiku-20240307" });
                }
            }
            catch { }
            return models;
        }

        public static async IAsyncEnumerable<string> SendMessageStreamAsync(string message, [EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default)
        {
            var settings = SettingsManager.Current;
            if (string.IsNullOrWhiteSpace(settings.AiApiKey) && settings.AiProvider != "Local Model (Ollama)")
            {
                yield return "Error: API key is not configured. Please set your " + settings.AiProvider + " API Key in Settings.";
                yield break;
            }

            if (settings.AiFastMode)
            {
                message = "You are in fast mode. You must reply immediately without any chain of thought. Never output <think> or <thought> tags.\n\n" + message;
            }

            IAsyncEnumerable<string> stream = null;

            string setupError = null;
            try
            {
                switch (settings.AiProvider)
                {
                    case "OpenAI":
                        stream = CallOpenAiStreamAsync(message, settings.AiApiKey, cancellationToken);
                        break;
                    case "Microsoft Azure OpenAI":
                        stream = CallAzureStreamAsync(message, settings.AiApiKey, cancellationToken);
                        break;
                    case "Google Gemini":
                        stream = CallGeminiStreamAsync(message, settings.AiApiKey, cancellationToken);
                        break;
                    case "Anthropic Claude":
                        stream = CallAnthropicStreamAsync(message, settings.AiApiKey, cancellationToken);
                        break;
                    case "Local Model (Ollama)":
                        stream = CallOllamaStreamAsync(message, cancellationToken);
                        break;
                    default:
                        setupError = "Error: Unknown AI Provider selected.";
                        break;
                }
            }
            catch (Exception ex)
            {
                setupError = $"Error connecting to AI Provider ({settings.AiProvider}): {ex.Message}";
            }

            if (setupError != null)
            {
                yield return setupError;
                yield break;
            }

            if (stream == null) yield break;

            bool isThinking = false;
            string buffer = "";
            bool hasYieldedError = false;

            await using var enumerator = stream.GetAsyncEnumerator(cancellationToken);
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

                if (!settings.AiFastMode)
                {
                    yield return chunk;
                    continue;
                }

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

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("data: ") && !line.Contains("[DONE]"))
                {
                    string jsonStr = line.Substring(6);
                    string delta = null;
                    try {
                        var json = JObject.Parse(jsonStr);
                        delta = json["choices"]?[0]?["delta"]?["content"]?.ToString();
                    } catch { } 
                    if (!string.IsNullOrEmpty(delta)) yield return delta;
                }
            }
        }

        private static async IAsyncEnumerable<string> CallAzureStreamAsync(string prompt, string apiKey, [EnumeratorCancellation] System.Threading.CancellationToken ct)
        {
            var baseUrl = SettingsManager.Current.AiBaseUrl;
            var deployment = SettingsManager.Current.AiDeploymentName;
            var apiVersion = SettingsManager.Current.AiApiVersion;

            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(deployment) || string.IsNullOrWhiteSpace(apiVersion))
            {
                yield return "Error: Azure requires Base URL, Deployment Name, and API Version to be configured.";
                yield break;
            }

            var url = $"{baseUrl.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";

            var payload = new
            {
                messages = new[] { new { role = "user", content = prompt } },
                stream = true
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            request.Headers.Add("api-key", apiKey);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream);

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("data: ") && !line.Contains("[DONE]"))
                {
                    string delta = null;
                    try {
                        var json = JObject.Parse(line.Substring(6));
                        delta = json["choices"]?[0]?["delta"]?["content"]?.ToString();
                    } catch { }
                    if (!string.IsNullOrEmpty(delta)) yield return delta;
                }
            }
        }

        private static async IAsyncEnumerable<string> CallGeminiStreamAsync(string prompt, string apiKey, [EnumeratorCancellation] System.Threading.CancellationToken ct)
        {
            var payload = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var modelName = string.IsNullOrWhiteSpace(SettingsManager.Current.AiModelName) ? "gemini-pro" : SettingsManager.Current.AiModelName;
            
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:streamGenerateContent?alt=sse&key={apiKey}";
            
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream);

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("data: "))
                {
                    string delta = null;
                    try {
                        var jsonStr = line.Substring(6);
                        if (jsonStr != "[DONE]") {
                            var json = JObject.Parse(jsonStr);
                            delta = json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
                        }
                    } catch { }
                    if (!string.IsNullOrEmpty(delta)) yield return delta;
                }
            }
        }

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

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("data: "))
                {
                    string delta = null;
                    try {
                        var json = JObject.Parse(line.Substring(6));
                        if (json["type"]?.ToString() == "content_block_delta")
                        {
                            delta = json["delta"]?["text"]?.ToString();
                        }
                    } catch { }
                    if (!string.IsNullOrEmpty(delta)) yield return delta;
                }
            }
        }

        private static async IAsyncEnumerable<string> CallOllamaStreamAsync(string prompt, [EnumeratorCancellation] System.Threading.CancellationToken ct)
        {
            var modelName = string.IsNullOrWhiteSpace(SettingsManager.Current.AiModelName) ? "llama3" : SettingsManager.Current.AiModelName;
            var payload = new
            {
                model = modelName, 
                messages = new[] { new { role = "user", content = prompt } },
                stream = true
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var baseUrl = string.IsNullOrWhiteSpace(SettingsManager.Current.AiBaseUrl) ? "http://localhost:11434" : SettingsManager.Current.AiBaseUrl;
            
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/chat") { Content = content };
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream);

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                string chunk = null;
                try {
                    var json = JObject.Parse(line);
                    chunk = json["message"]?["content"]?.ToString();
                } catch { }
                if (!string.IsNullOrEmpty(chunk)) yield return chunk;
            }
        }
    }
}
