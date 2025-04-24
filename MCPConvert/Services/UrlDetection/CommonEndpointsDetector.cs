using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MCPConvert.Models;

namespace MCPConvert.Services.UrlDetection
{
    /// <summary>
    /// Detector for direct Swagger/OpenAPI JSON URLs
    /// </summary>
    public class CommonEndpointsDetector : ISwaggerUrlDetector
    {
        private readonly ILogger<CommonEndpointsDetector> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public CommonEndpointsDetector(ILogger<CommonEndpointsDetector> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        public bool CanHandle(string url)
        {
            // This detector can handle direct Swagger JSON URLs and fallback to common endpoints
            // We'll do a quick check first to avoid unnecessary processing
            return SwaggerUrlPatterns.QuickCheck(url);
        }

        /// <inheritdoc />
        public async Task<string> DetectSwaggerJsonUrlAsync(string url, bool diagnosticMode, ConversionDiagnostics? diagnostics)
        {
            var httpClient = _httpClientFactory.CreateClient();
            
            try
            {
                // First check if the URL is already a valid Swagger JSON endpoint
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    // Simple check for common Swagger/OpenAPI JSON properties
                    if (content.Contains("\"swagger\":") || content.Contains("\"openapi\":"))
                    {
                        if (diagnosticMode && diagnostics != null)
                        {
                            diagnostics.ProcessingSteps.Add($"URL is already a valid Swagger/OpenAPI JSON endpoint: {url}");
                        }
                        return url;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error checking if URL is a direct Swagger JSON endpoint: {ex.Message}");
            }
            
            // If the URL is not a direct Swagger JSON endpoint, try common patterns
            return await TryCommonEndpointsAsync(url, httpClient, diagnosticMode, diagnostics);
        }
        
        private async Task<string> TryCommonEndpointsAsync(string url, HttpClient httpClient, bool diagnosticMode, ConversionDiagnostics? diagnostics)
        {
            var uri = new Uri(url);
            var baseUrl = $"{uri.Scheme}://{uri.Authority}";
            
            // Extract API version from the URL path
            var apiVersion = SwaggerUrlPatterns.ExtractApiVersion(url);
            
            // Generate common endpoints to try
            var commonEndpoints = SwaggerUrlPatterns.GenerateCommonEndpoints(baseUrl, apiVersion);
            
            if (diagnosticMode && diagnostics != null)
            {
                diagnostics.ProcessingSteps.Add("Trying common Swagger/OpenAPI endpoints");
            }
            
            foreach (var endpoint in commonEndpoints)
            {
                try
                {
                    var testResponse = await httpClient.GetAsync(endpoint);
                    if (testResponse.IsSuccessStatusCode)
                    {
                        var content = await testResponse.Content.ReadAsStringAsync();
                        // Simple check for common Swagger/OpenAPI JSON properties
                        if (content.Contains("\"swagger\":") || content.Contains("\"openapi\":"))
                        {
                            if (diagnosticMode && diagnostics != null)
                            {
                                diagnostics.ProcessingSteps.Add($"Found working Swagger JSON endpoint: {endpoint}");
                            }
                            return endpoint;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Error checking endpoint {endpoint}: {ex.Message}");
                    // Continue trying other endpoints
                }
            }
            
            // If all else fails, return the original URL
            return url;
        }
    }
}
