using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MCPConvert.Services.UrlDetection
{
    /// <summary>
    /// Provides shared URL patterns and matching logic for Swagger/OpenAPI URL detection
    /// </summary>
    public static class SwaggerUrlPatterns
    {
        // Quick check keywords for fast rejection
        private static readonly string[] KeywordsForQuickCheck = new[] { 
            "swagger", 
            "openapi", 
            "api-docs", 
            "docs", 
            "documentation", 
            "api-explorer"
        };
        
        // Common Swagger UI URL patterns
        private static readonly string[] SwaggerUiPatterns = new[]
        {
            @"/swagger-ui\.html",
            @"/swagger-ui/?",
            @"/swagger-ui/index\.html",
            @"/swagger/?$",
            @"/swagger/index\.html",
            @"/api-docs/?$",
            @"/api-explorer/?$",
            @"/docs/?$",
            @"/documentation/?$"
        };
        
        // Common Swagger/OpenAPI JSON endpoint patterns
        private static readonly string[] SwaggerJsonPatterns = new[]
        {
            @"/swagger/v\d+/swagger\.json",
            @"/swagger/swagger\.json",
            @"/swagger\.json",
            @"/openapi\.json",
            @"/openapi\.yaml",
            @"/v\d+/swagger\.json",
            @"/v\d+/api-docs",
            @"/api-docs",
            @"/api-docs/v\d+",
            @"/api/v\d+/swagger\.json",
            @"/api/v\d+/openapi\.json",
            @"/api/swagger\.json",
            @"/api/openapi\.json",
            @"/api/docs/swagger\.json",
            @"/docs/swagger\.json"
        };
        
        // Common paths to try when looking for Swagger/OpenAPI JSON endpoints
        public static readonly string[] CommonEndpointPaths = new[]
        {
            "swagger/{0}/swagger.json",  // {0} will be replaced with API version
            "swagger/v1/swagger.json",
            "api/v3/openapi.json",
            "v2/swagger.json",
            "api-docs",
            "api-docs/swagger.json",
            "swagger/swagger.json",
            "openapi.json",
            "swagger.json",
            "api/swagger.json",
            "api/openapi.json",
            "api/docs/swagger.json",
            "docs/swagger.json",
            "docs/openapi.json"
        };
        
        /// <summary>
        /// Performs a quick check to see if a URL might be Swagger/OpenAPI related
        /// </summary>
        /// <param name="url">The URL to check</param>
        /// <returns>True if the URL contains any Swagger/OpenAPI related keywords</returns>
        public static bool QuickCheck(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;
                
            return KeywordsForQuickCheck.Any(keyword => 
                url.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        
        /// <summary>
        /// Checks if a URL matches any of the Swagger UI patterns
        /// </summary>
        /// <param name="url">The URL to check</param>
        /// <returns>True if the URL matches any Swagger UI pattern</returns>
        public static bool IsSwaggerUiUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;
                
            try
            {
                var uri = new Uri(url);
                
                // Special case for root path which might be a Swagger UI
                if (uri.AbsolutePath == "/" || uri.AbsolutePath.Equals(""))
                    return true;
                    
                // Direct string checks for common patterns
                if (url.EndsWith("/swagger") || url.EndsWith("/swagger/") ||
                    url.EndsWith("/api-docs") || url.EndsWith("/api-docs/") ||
                    url.EndsWith("/docs") || url.EndsWith("/docs/") ||
                    url.EndsWith("/documentation") || url.EndsWith("/documentation/") ||
                    url.Contains("/swagger-ui") || url.Contains("/api-explorer"))
                    return true;
                    
                // Regex pattern matching for more complex cases
                foreach (var pattern in SwaggerUiPatterns)
                {
                    if (Regex.IsMatch(uri.AbsolutePath, pattern, RegexOptions.IgnoreCase))
                        return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Checks if a URL is likely a direct Swagger/OpenAPI JSON endpoint
        /// </summary>
        /// <param name="url">The URL to check</param>
        /// <returns>True if the URL matches any Swagger JSON pattern</returns>
        public static bool IsLikelySwaggerJsonUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;
                
            try
            {
                var uri = new Uri(url);
                
                // Direct string checks for common patterns
                if (url.EndsWith(".json") && (
                    url.Contains("/swagger") || 
                    url.Contains("/openapi") || 
                    url.Contains("/api-docs")))
                    return true;
                    
                // Check if the URL matches any of the Swagger JSON patterns
                foreach (var pattern in SwaggerJsonPatterns)
                {
                    if (Regex.IsMatch(uri.AbsolutePath, pattern, RegexOptions.IgnoreCase))
                        return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Extracts the API version from a URL path
        /// </summary>
        /// <param name="url">The URL to extract the version from</param>
        /// <returns>The API version (e.g., "v1", "v2") or "v1" if not found</returns>
        public static string ExtractApiVersion(string url)
        {
            try
            {
                var uri = new Uri(url);
                var pathParts = uri.AbsolutePath.Split('/');
                
                foreach (var part in pathParts)
                {
                    if (part.StartsWith("v", StringComparison.OrdinalIgnoreCase) && 
                        part.Length > 1 && 
                        char.IsDigit(part[1]))
                    {
                        return part.ToLowerInvariant();
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            
            return "v1"; // Default version
        }
        
        /// <summary>
        /// Generates a list of common Swagger/OpenAPI endpoints to try for a given base URL
        /// </summary>
        /// <param name="baseUrl">The base URL (scheme + authority)</param>
        /// <param name="apiVersion">The API version to use in the templates</param>
        /// <returns>A list of full URLs to try</returns>
        public static List<string> GenerateCommonEndpoints(string baseUrl, string apiVersion)
        {
            var endpoints = new List<string>();
            
            foreach (var path in CommonEndpointPaths)
            {
                var formattedPath = string.Format(path, apiVersion);
                endpoints.Add($"{baseUrl.TrimEnd('/')}/{formattedPath.TrimStart('/')}");
            }
            
            return endpoints;
        }
    }
}
