using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.IO;
using sqlSense.Services.Configuration;

namespace sqlSense.Services.Http
{
    public static class HttpService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string LogDir = Path.Combine(AppConstants.LocalAppDataFolder, "logs");
        
        private static string GetLogPath() => Path.Combine(LogDir, $"http_{DateTime.Now:yyyyMMdd}.log");

        private static void Log(string message)
        {
            // Always log to Debug/Output for developers
            Debug.WriteLine(message);

            // Optionally log to file if enabled in settings
            if (SettingsManager.Current?.EnableHttpLogging == true)
            {
                // Offload file I/O to a background thread to avoid blocking network/UI
                Task.Run(() =>
                {
                    try
                    {
                        if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);
                        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                        lock (LogDir) // Simple lock to avoid concurrent write issues
                        {
                            File.AppendAllText(GetLogPath(), $"[{timestamp}] {message}{Environment.NewLine}");
                        }
                    }
                    catch { /* Ignore logging failures to prevent app crashes */ }
                });
            }
        }

        /// <summary>
        /// Sends an HTTP request and logs the process.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <param name="callKey">A key to distinguish this call in logs.</param>
        public static async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string callKey, CancellationToken cancellationToken = default)
        {
            string url = request.RequestUri?.ToString() ?? "Unknown URL";
            string method = request.Method.Method;
            
            Log($"\n[HttpService] >>> [{callKey}] Sending {method} to {url}");
            
            if (request.Content != null)
            {
                try
                {
                    var body = await request.Content.ReadAsStringAsync();
                    Log($"[HttpService] [{callKey}] Request Body: {body}");
                }
                catch { Log($"[HttpService] [{callKey}] Could not read request body."); }
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.SendAsync(request, cancellationToken);
                stopwatch.Stop();
                
                Log($"[HttpService] <<< [{callKey}] Response: {(int)response.StatusCode} {response.StatusCode} (in {stopwatch.ElapsedMilliseconds}ms)");
                
                if (response.Content != null)
                {
                    try
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        Log($"[HttpService] [{callKey}] Response Body: {body}");
                    }
                    catch { Log($"[HttpService] [{callKey}] Could not read response body."); }
                }
                
                return response;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                Log($"[HttpService] !!! [{callKey}] Request CANCELED (after {stopwatch.ElapsedMilliseconds}ms)");
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Log($"[HttpService] !!! [{callKey}] Request ERROR (after {stopwatch.ElapsedMilliseconds}ms): {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Sends an HTTP request specifically for streaming responses.
        /// </summary>
        public static async Task<HttpResponseMessage> SendStreamAsync(HttpRequestMessage request, string callKey, CancellationToken cancellationToken = default)
        {
            string url = request.RequestUri?.ToString() ?? "Unknown URL";
            Log($"\n[HttpService] >>> [{callKey}] Starting STREAMING request to {url}");

            if (request.Content != null)
            {
                try
                {
                    var body = await request.Content.ReadAsStringAsync();
                    Log($"[HttpService] [{callKey}] Request Body: {body}");
                }
                catch { Log($"[HttpService] [{callKey}] Could not read request body."); }
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                stopwatch.Stop();
                
                Log($"[HttpService] <<< [{callKey}] Stream Headers Received: {(int)response.StatusCode} (in {stopwatch.ElapsedMilliseconds}ms)");
                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Log($"[HttpService] !!! [{callKey}] Stream Initialization ERROR: {ex.Message}");
                throw;
            }
        }

        // Convenience methods
        public static async Task<HttpResponseMessage> GetAsync(string url, string callKey, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            return await SendAsync(request, callKey, cancellationToken);
        }
    }
}
