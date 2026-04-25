using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

namespace sqlSense.Services.Http
{
    public static class HttpService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Sends an HTTP request and logs the process.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <param name="callKey">A key to distinguish this call in logs.</param>
        public static async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string callKey, CancellationToken cancellationToken = default)
        {
            string url = request.RequestUri?.ToString() ?? "Unknown URL";
            string method = request.Method.Method;
            
            Debug.WriteLine($"\n[HttpService] >>> [{callKey}] Sending {method} to {url}");
            
            if (request.Content != null)
            {
                // We don't log full content to avoid noise and privacy issues, just the type/length
                var headers = request.Content.Headers;
                Debug.WriteLine($"[HttpService] [{callKey}] Content-Type: {headers.ContentType}, Length: {headers.ContentLength ?? 0}");
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.SendAsync(request, cancellationToken);
                stopwatch.Stop();
                
                Debug.WriteLine($"[HttpService] <<< [{callKey}] Response: {(int)response.StatusCode} {response.StatusCode} (in {stopwatch.ElapsedMilliseconds}ms)");
                return response;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                Debug.WriteLine($"[HttpService] !!! [{callKey}] Request CANCELED (after {stopwatch.ElapsedMilliseconds}ms)");
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Debug.WriteLine($"[HttpService] !!! [{callKey}] Request ERROR (after {stopwatch.ElapsedMilliseconds}ms): {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Sends an HTTP request specifically for streaming responses.
        /// </summary>
        public static async Task<HttpResponseMessage> SendStreamAsync(HttpRequestMessage request, string callKey, CancellationToken cancellationToken = default)
        {
            string url = request.RequestUri?.ToString() ?? "Unknown URL";
            Debug.WriteLine($"\n[HttpService] >>> [{callKey}] Starting STREAMING request to {url}");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                stopwatch.Stop();
                
                Debug.WriteLine($"[HttpService] <<< [{callKey}] Stream Headers Received: {(int)response.StatusCode} (in {stopwatch.ElapsedMilliseconds}ms)");
                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Debug.WriteLine($"[HttpService] !!! [{callKey}] Stream Initialization ERROR: {ex.Message}");
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
