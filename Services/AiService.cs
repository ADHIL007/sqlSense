using System;
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
            
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
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
            
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
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
            var response = await _httpClient.PostAsync("http://localhost:11434/api/chat", content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseString);
            return json["message"]?["content"]?.ToString() ?? "No content returned.";
        }
    }
}
