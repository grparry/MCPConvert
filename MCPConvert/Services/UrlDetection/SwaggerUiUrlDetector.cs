using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MCPConvert.Models;

namespace MCPConvert.Services.UrlDetection
{
    /// <summary>
    /// Detector for Swagger UI URLs that parses HTML and JavaScript to find the Swagger JSON URL
    /// </summary>
    public class SwaggerUiUrlDetector : ISwaggerUrlDetector
    {
        private readonly ILogger<SwaggerUiUrlDetector> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public SwaggerUiUrlDetector(ILogger<SwaggerUiUrlDetector> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        public bool CanHandle(string url)
        {
            return SwaggerUrlPatterns.IsSwaggerUiUrl(url);
        }

        /// <inheritdoc />
        public async Task<string> DetectSwaggerJsonUrlAsync(string url, bool diagnosticMode, ConversionDiagnostics? diagnostics)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var swaggerUri = new Uri(url);
            string actualSwaggerUrl = url;
            
            try
            {
                // Try to get the Swagger UI page
                if (diagnosticMode && diagnostics != null) 
                    diagnostics.ProcessingSteps.Add("Detected Swagger UI URL, attempting to find JSON endpoint");
                
                var uiResponse = await httpClient.GetAsync(url);
                uiResponse.EnsureSuccessStatusCode();
                var htmlContent = await uiResponse.Content.ReadAsStringAsync();
                
                // Check for swagger-initializer.js script tag in the HTML
                var initializerMatch = Regex.Match(htmlContent, @"<script\s+src=[""']([^""']*swagger-initializer\.js[^""']*)[""']");
                if (initializerMatch.Success)
                {
                    var detectedUrl = await TryExtractFromInitializerJsAsync(swaggerUri, httpClient, htmlContent, diagnosticMode, diagnostics);
                    if (detectedUrl != null)
                    {
                        return detectedUrl;
                    }
                }
                
                // Try to find URL in HTML content using enhanced patterns
                var detectedUrlFromHtml = TryExtractFromHtml(url, htmlContent, diagnosticMode, diagnostics);
                if (detectedUrlFromHtml != null)
                {
                    return detectedUrlFromHtml;
                }
                
                // If we still haven't found the URL, try common endpoints
                return await TryCommonEndpointsAsync(url, httpClient, diagnosticMode, diagnostics);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error detecting Swagger JSON URL: {ex.Message}");
                if (diagnosticMode && diagnostics != null)
                {
                    diagnostics.ProcessingSteps.Add($"Error detecting Swagger JSON URL: {ex.Message}");
                }
                
                return url; // Return original URL if detection fails
            }
        }
        
        private async Task<string?> TryExtractFromInitializerJsAsync(Uri swaggerUri, HttpClient httpClient, string htmlContent, bool diagnosticMode, ConversionDiagnostics? diagnostics)
        {
            try
            {
                // Check for swagger-initializer.js script tag in the HTML
                var initializerMatch = Regex.Match(htmlContent, @"<script\s+src=['""]([^'""]*swagger-initializer\.js[^'""]*)['""]");
                if (!initializerMatch.Success)
                {
                    return null;
                }
                
                _logger.LogInformation("Found reference to swagger-initializer.js, attempting to fetch it");
                
                // Extract the initializer.js URL and make it absolute
                var initializerUrl = initializerMatch.Groups[1].Value;
                var absoluteInitializerUrl = new Uri(swaggerUri, initializerUrl).ToString();
                
                if (diagnosticMode && diagnostics != null)
                {
                    diagnostics.ProcessingSteps.Add($"Found swagger-initializer.js: {absoluteInitializerUrl}");
                }
                
                // Fetch the initializer script
                var initializerResponse = await httpClient.GetAsync(absoluteInitializerUrl);
                
                if (initializerResponse.IsSuccessStatusCode)
                {
                    var initializerContent = await initializerResponse.Content.ReadAsStringAsync();
                    _logger.LogInformation($"Found swagger-initializer.js, content length: {initializerContent.Length}");
                    
                    // Look for ossServices pattern (common in swagger.io implementations)
                    var swaggerHost = swaggerUri.Host.ToLowerInvariant();
                    _logger.LogInformation($"Looking for host {swaggerHost} in initializer content");
                    var ossServicesMatch = Regex.Match(initializerContent, $"{swaggerHost}=([^,\"']+)");
                    
                    if (ossServicesMatch.Success)
                    {
                        var jsonPath = ossServicesMatch.Groups[1].Value;
                        if (jsonPath.StartsWith("http"))
                        {
                            _logger.LogInformation($"Found Swagger JSON URL from ossServices mapping: {jsonPath}");
                            
                            if (diagnosticMode && diagnostics != null)
                            {
                                diagnostics.ProcessingSteps.Add($"Found Swagger JSON URL from ossServices mapping: {jsonPath}");
                            }
                            
                            return jsonPath;
                        }
                    }
                    else
                    {
                        // Look for url or definitionURL variable assignments
                        var urlPatterns = new[] {
                            @"url\s*=\s*['""]([^'""]+\.json)['""]",
                            @"definitionURL\s*=\s*['""]([^'""]+\.json)['""]",
                            @"specUrl\s*=\s*['""]([^'""]+\.json)['""]"
                        };                        
                        foreach (var pattern in urlPatterns)
                        {
                            var match = Regex.Match(initializerContent, pattern);
                            if (match.Success)
                            {
                                var jsonPath = match.Groups[1].Value;
                                _logger.LogInformation($"Found Swagger JSON URL using pattern '{pattern}': {jsonPath}");
                                
                                // Handle relative or absolute URL
                                string actualUrl;
                                var baseUrlStr = $"{swaggerUri.Scheme}://{swaggerUri.Authority}";
                                if (jsonPath.StartsWith("/"))
                                {
                                    actualUrl = baseUrlStr + jsonPath;
                                }
                                else if (!jsonPath.StartsWith("http"))
                                {
                                    actualUrl = $"{baseUrlStr}/{jsonPath}";
                                }
                                else
                                {
                                    actualUrl = jsonPath;
                                }
                                
                                if (diagnosticMode && diagnostics != null)
                                {
                                    diagnostics.ProcessingSteps.Add($"Found Swagger JSON URL from JavaScript: {actualUrl}");
                                }
                                
                                return actualUrl;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error fetching swagger-initializer.js: {ex.Message}");
            }
            
            return null;
        }
        
        private string? TryExtractFromHtml(string url, string htmlContent, bool diagnosticMode, ConversionDiagnostics? diagnostics)
        {
            var baseUri = new Uri(url);
            
            // Check for configUrl in SwaggerUI constructor
            var configUrlMatch = Regex.Match(htmlContent, "SwaggerUIBundle\\(\\s*\\{[^\\}]*configUrl:\\s*[\"']([^\"']+)[\"']", RegexOptions.Singleline);
            if (configUrlMatch.Success)
            {
                var jsonUrl = configUrlMatch.Groups[1].Value;
                var absoluteJsonUrl = new Uri(baseUri, jsonUrl).ToString();
                
                if (diagnosticMode && diagnostics != null)
                {
                    diagnostics.ProcessingSteps.Add($"Found Swagger JSON URL from SwaggerUIBundle configUrl: {absoluteJsonUrl}");
                }
                
                return absoluteJsonUrl;
            }
            
            // Check for common SwaggerUI constructor patterns
            var uiPatterns = new[] {
                @"SwaggerUI(?:Bundle)?\(\s*\{[^}]*url:\s*['""]([^'""]+\.json)['""]",
                @"SwaggerUIBundle\(\s*\{[^}]*urls:\s*\[\s*\{\s*url:\s*['""]([^'""]+\.json)['""]",
                @"data-swagger-url=['""]([^'""]+\.json)['""]",
                @"var\s+url\s*=\s*['""]([^'""]+\.json)['""]",
                @"""url""\s*:\s*""([^""]+\.json)"""
            };            
            foreach (var pattern in uiPatterns)
            {
                var match = Regex.Match(htmlContent, pattern, RegexOptions.Singleline);
                if (match.Success)
                {
                    var jsonUrl = match.Groups[1].Value;
                    var absoluteJsonUrl = new Uri(baseUri, jsonUrl).ToString();
                    
                    if (diagnosticMode && diagnostics != null)
                    {
                        diagnostics.ProcessingSteps.Add($"Found Swagger JSON URL from HTML pattern '{pattern}': {absoluteJsonUrl}");
                    }
                    
                    return absoluteJsonUrl;
                }
            }
            
            // Look for script tags with inline JavaScript that might contain the URL
            var scriptTags = Regex.Matches(htmlContent, "<script[^>]*>([^<]*)</script>", RegexOptions.Singleline);
            foreach (Match scriptMatch in scriptTags)
            {
                var scriptContent = scriptMatch.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(scriptContent))
                {
                    // Check for various URL patterns in inline scripts
                    var scriptUrlPatterns = new[] {
                        @"url\s*=\s*['""]([^'""]+\.json)['""]",
                        @"url:\s*['""]([^'""]+\.json)['""]",
                        @"spec:\s*['""]([^'""]+\.json)['""]",
                        @"swaggerUrl\s*=\s*['""]([^'""]+\.json)['""]"
                    };                    
                    foreach (var pattern in scriptUrlPatterns)
                    {
                        var match = Regex.Match(scriptContent, pattern);
                        if (match.Success)
                        {
                            var jsonUrl = match.Groups[1].Value;
                            var absoluteJsonUrl = new Uri(baseUri, jsonUrl).ToString();
                            
                            if (diagnosticMode && diagnostics != null)
                            {
                                diagnostics.ProcessingSteps.Add($"Found Swagger JSON URL from inline script pattern '{pattern}': {absoluteJsonUrl}");
                            }
                            
                            return absoluteJsonUrl;
                        }
                    }
                }
            }
            
            return null;
        }
        
        private string ResolveRelativeUrl(string baseUrl, string relativePath)
        {
            var uri = new Uri(baseUrl);
            
            // If it's a relative URL, combine with the base URL
            if (relativePath.StartsWith("/"))
            {
                var baseUrlWithoutPath = $"{uri.Scheme}://{uri.Authority}";
                return baseUrlWithoutPath + relativePath;
            }
            else if (!relativePath.StartsWith("http"))
            {
                // Handle relative path without leading slash
                var baseUrlWithPath = $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}";
                // Remove the index.html or trailing slash if present
                baseUrlWithPath = Regex.Replace(baseUrlWithPath, @"(index\.html|/+)$", "");
                return baseUrlWithPath + "/" + relativePath;
            }
            else
            {
                return relativePath; // Already absolute
            }
        }
        
        private async Task<string> TryCommonEndpointsAsync(string url, HttpClient httpClient, bool diagnosticMode, ConversionDiagnostics? diagnostics)
        {
            var uri = new Uri(url);
            var baseUrl = $"{uri.Scheme}://{uri.Authority}";
            var pathParts = uri.AbsolutePath.Split('/');
            var apiVersion = "v1";
            
            // Try to extract API version from the path
            foreach (var part in pathParts)
            {
                if (part.StartsWith("v") && part.Length > 1 && char.IsDigit(part[1]))
                {
                    apiVersion = part;
                    break;
                }
            }
            
            // Define common Swagger/OpenAPI path patterns
            var commonPaths = new[]
            {
                $"swagger/{apiVersion}/swagger.json",
                $"swagger/v1/swagger.json",
                $"api/v3/openapi.json",  // Common for Swagger 3.0
                $"v2/swagger.json",       // Common for Swagger 2.0
                $"api-docs",
                $"api-docs/swagger.json",
                $"swagger/swagger.json",
                $"openapi.json",
                $"swagger.json",
                $"api/swagger.json",
                $"api/openapi.json",
                $"api/docs/swagger.json",
                $"docs/swagger.json"
            };
            
            // Create full URLs from the path patterns
            var commonEndpoints = commonPaths.Select(path => $"{baseUrl}/{path.TrimStart('/')}").ToList();
            
            if (diagnosticMode && diagnostics != null)
            {
                diagnostics.ProcessingSteps.Add("Could not find Swagger JSON URL in HTML, trying common endpoints");
            }
            
            foreach (var endpoint in commonEndpoints)
            {
                try
                {
                    var testResponse = await httpClient.GetAsync(endpoint);
                    if (testResponse.IsSuccessStatusCode)
                    {
                        if (diagnosticMode && diagnostics != null)
                        {
                            diagnostics.ProcessingSteps.Add($"Found working Swagger JSON endpoint: {endpoint}");
                        }
                        return endpoint;
                    }
                }
                catch
                {
                    // Continue trying other endpoints
                }
            }
            
            // If all else fails, return the original URL
            return url;
        }
    }
}
