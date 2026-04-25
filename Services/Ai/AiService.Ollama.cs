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
            using var response = await HttpService.SendStreamAsync(request, "AI_Chat_Ollama", ct);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream, Encoding.UTF8);

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

