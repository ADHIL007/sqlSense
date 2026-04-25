using sqlSense.Services.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;

namespace sqlSense.Services.Ai
{
    public static partial class AiService
    {
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
    }
}

