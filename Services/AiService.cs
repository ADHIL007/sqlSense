using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
                    // No public live model list, push static commonly used values
                    models.AddRange(new[] { "claude-3-5-sonnet-20240620", "claude-3-opus-20240229", "claude-3-sonnet-20240229", "claude-3-haiku-20240307" });
                }
            }
            catch { }
            return models;
        }

        public static async Task<string> SendMessageAsync(string message)
        {
            var settings = SettingsManager.Current;
            if (string.IsNullOrWhiteSpace(settings.AiApiKey) && settings.AiProvider != "Local Model (Ollama)")
            {
                return "Error: API key is not configured. Please set your " + settings.AiProvider + " API Key in Settings.";
            }

            try
            {
                switch (settings.AiProvider)
                {
                    case "OpenAI":
                        return await CallOpenAiAsync(message, settings.AiApiKey);
                    case "Microsoft Azure OpenAI":
                        return await CallAzureAsync(message, settings.AiApiKey);
                    case "Google Gemini":
                        return await CallGeminiAsync(message, settings.AiApiKey);
                    case "Anthropic Claude":
                        return await CallAnthropicAsync(message, settings.AiApiKey);
                    case "Local Model (Ollama)":
                        return await CallOllamaAsync(message);
                    default:
                        return "Error: Unknown AI Provider selected.";
                }
            }
            catch (Exception ex)
            {
                return $"Error connecting to AI Provider ({settings.AiProvider}): {ex.Message}";
            }
        }

        private static async Task<string> CallOpenAiAsync(string prompt, string apiKey)
        {
            var modelName = string.IsNullOrWhiteSpace(SettingsManager.Current.AiModelName) ? "gpt-3.5-turbo" : SettingsManager.Current.AiModelName;
            var payload = new
            {
                model = modelName,
                messages = new[] { new { role = "user", content = prompt } }
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            
            var baseUrl = string.IsNullOrWhiteSpace(SettingsManager.Current.AiBaseUrl) ? "https://api.openai.com/v1" : SettingsManager.Current.AiBaseUrl;
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions")
            {
                Content = content
            };
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseString);
            return json["choices"]?[0]?["message"]?["content"]?.ToString() ?? "No content returned.";
        }

        private static async Task<string> CallAzureAsync(string prompt, string apiKey)
        {
            var baseUrl = SettingsManager.Current.AiBaseUrl;
            var deployment = SettingsManager.Current.AiDeploymentName;
            var apiVersion = SettingsManager.Current.AiApiVersion;

            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(deployment) || string.IsNullOrWhiteSpace(apiVersion))
            {
                return "Error: Azure requires Base URL, Deployment Name, and API Version to be configured.";
            }

            var url = $"{baseUrl.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";

            var payload = new
            {
                messages = new[] { new { role = "user", content = prompt } }
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            request.Headers.Add("api-key", apiKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseString);
            return json["choices"]?[0]?["message"]?["content"]?.ToString() ?? "No content returned.";
        }

        private static async Task<string> CallGeminiAsync(string prompt, string apiKey)
        {
            var payload = new
            {
                contents = new[] {
                    new {
                        parts = new[] {
                            new { text = prompt }
                        }
                    }
                }
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var modelName = string.IsNullOrWhiteSpace(SettingsManager.Current.AiModelName) ? "gemini-pro" : SettingsManager.Current.AiModelName;
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";
            
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseString);
            return json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString() ?? "No content returned.";
        }

        private static async Task<string> CallAnthropicAsync(string prompt, string apiKey)
        {
            var modelName = string.IsNullOrWhiteSpace(SettingsManager.Current.AiModelName) ? "claude-3-opus-20240229" : SettingsManager.Current.AiModelName;
            var payload = new
            {
                model = modelName,
                max_tokens = 1000,
                messages = new[] { new { role = "user", content = prompt } }
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            
            var baseUrl = string.IsNullOrWhiteSpace(SettingsManager.Current.AiBaseUrl) ? "https://api.anthropic.com/v1" : SettingsManager.Current.AiBaseUrl;
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/messages")
            {
                Content = content
            };
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseString);
            return json["content"]?[0]?["text"]?.ToString() ?? "No content returned.";
        }

        private static async Task<string> CallOllamaAsync(string prompt)
        {
            var modelName = string.IsNullOrWhiteSpace(SettingsManager.Current.AiModelName) ? "llama3" : SettingsManager.Current.AiModelName;
            var payload = new
            {
                model = modelName, 
                messages = new[] { new { role = "user", content = prompt } },
                stream = false
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var baseUrl = string.IsNullOrWhiteSpace(SettingsManager.Current.AiBaseUrl) ? "http://localhost:11434" : SettingsManager.Current.AiBaseUrl;
            var response = await _httpClient.PostAsync($"{baseUrl.TrimEnd('/')}/api/chat", content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseString);
            return json["message"]?["content"]?.ToString() ?? "No content returned.";
        }
    }
}
