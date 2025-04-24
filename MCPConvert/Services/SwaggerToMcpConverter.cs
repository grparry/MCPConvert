using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using MCPConvert.Models;
using MCPConvert.Services.Conversion;
using MCPConvert.Services.UrlDetection;
using Newtonsoft.Json;

namespace MCPConvert.Services
{
    /// <summary>
    /// Service for converting Swagger/OpenAPI documents to MCP JSON
    /// </summary>
    public class SwaggerToMcpConverter : ISwaggerToMcpConverter
    {
        private readonly ILogger<SwaggerToMcpConverter> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IEnumerable<ISwaggerUrlDetector> _urlDetectors;
        private readonly OpenApiToMcpConverter _openApiConverter;

        /// <summary>
        /// Initializes a new instance of the <see cref="SwaggerToMcpConverter"/> class
        /// </summary>
        /// <param name="logger">Logger</param>
        /// <param name="httpClientFactory">HTTP client factory</param>
        /// <param name="urlDetectors">Collection of URL detectors</param>
        /// <param name="openApiConverter">OpenAPI to MCP converter</param>
        public SwaggerToMcpConverter(
            ILogger<SwaggerToMcpConverter> logger, 
            IHttpClientFactory httpClientFactory,
            IEnumerable<ISwaggerUrlDetector> urlDetectors,
            OpenApiToMcpConverter openApiConverter)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _urlDetectors = urlDetectors;
            _openApiConverter = openApiConverter;
        }

        /// <inheritdoc />
        public async Task<ConversionResponse> ConvertFromUrlAsync(string swaggerUrl, bool includeSourceMapping = false, bool diagnosticMode = false)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var diagnostics = diagnosticMode ? new ConversionDiagnostics() : null;
            var sourceMap = includeSourceMapping ? new Dictionary<string, SourceMapEntry>() : null;

            try
            {
                if (diagnosticMode && diagnostics != null)
                {
                    diagnostics.ProcessingSteps.Add($"Starting conversion from URL: {swaggerUrl}");
                }

                // Detect the actual Swagger/OpenAPI JSON URL
                string actualSwaggerUrl = await DetectSwaggerJsonUrlAsync(swaggerUrl, diagnosticMode, diagnostics);
                
                // Fetch the Swagger document
                HttpResponseMessage response;
                try
                {
                    if (diagnosticMode && diagnostics != null) 
                        diagnostics.ProcessingSteps.Add($"Fetching Swagger JSON from: {actualSwaggerUrl}");
                    
                    var httpClient = _httpClientFactory.CreateClient();
                    response = await httpClient.GetAsync(actualSwaggerUrl);
                    response.EnsureSuccessStatusCode();

                    if (diagnosticMode && diagnostics != null)
                    {
                        diagnostics.PerformanceMetrics["FetchTime"] = stopwatch.Elapsed.TotalMilliseconds;
                        diagnostics.ProcessingSteps.Add($"Swagger document fetched successfully ({response.Content.Headers.ContentLength} bytes)");
                    }
                }
                catch (Exception ex)
                {
                    if (diagnosticMode && diagnostics != null)
                    {
                        diagnostics.ProcessingSteps.Add($"Error fetching Swagger document: {ex.Message}");
                    }
                    return new ConversionResponse
                    {
                        Success = false,
                        ErrorMessage = $"Failed to fetch Swagger document: {ex.Message}",
                        Diagnostics = diagnostics,
                        Timestamp = DateTime.UtcNow
                    };
                }

                // Parse the Swagger document
                var swaggerStream = await response.Content.ReadAsStreamAsync();
                var openApiDocument = await ParseSwaggerDocumentAsync(swaggerStream, diagnostics, diagnosticMode);

                if (openApiDocument == null)
                {
                    return new ConversionResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to parse Swagger document",
                        Diagnostics = diagnostics,
                        Timestamp = DateTime.UtcNow
                    };
                }

                // Convert to MCP JSON
                if (diagnosticMode && diagnostics != null) 
                    diagnostics.ProcessingSteps.Add("Converting Swagger to MCP JSON");
                
                var conversionStartTime = stopwatch.Elapsed.TotalMilliseconds;
                var mcpJson = _openApiConverter.ConvertToMcp(openApiDocument, sourceMap, diagnostics, diagnosticMode);

                if (diagnosticMode && diagnostics != null)
                {
                    diagnostics.PerformanceMetrics["ConversionTime"] = stopwatch.Elapsed.TotalMilliseconds - conversionStartTime;
                    diagnostics.ProcessingSteps.Add("Conversion completed successfully");
                }

                // Calculate content hash for idempotency
                string contentHash;
                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(mcpJson));
                    contentHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }

                stopwatch.Stop();
                if (diagnosticMode && diagnostics != null)
                {
                    diagnostics.PerformanceMetrics["TotalTime"] = stopwatch.Elapsed.TotalMilliseconds;
                }

                return new ConversionResponse
                {
                    Success = true,
                    McpJson = mcpJson,
                    ContentHash = contentHash,
                    SourceMap = sourceMap,
                    Diagnostics = diagnostics,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting Swagger from URL");

                if (diagnosticMode && diagnostics != null)
                {
                    diagnostics.ProcessingSteps.Add($"Unexpected error: {ex.Message}");
                    diagnostics.Warnings.Add($"Exception: {ex.GetType().Name}");
                }

                return new ConversionResponse
                {
                    Success = false,
                    ErrorMessage = $"Conversion failed: {ex.Message}",
                    Diagnostics = diagnostics,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <inheritdoc />
        public async Task<ConversionResponse> ConvertFromStreamAsync(Stream swaggerStream, bool includeSourceMapping = false, bool diagnosticMode = false)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var diagnostics = diagnosticMode ? new ConversionDiagnostics() : null;
            var sourceMap = includeSourceMapping ? new Dictionary<string, SourceMapEntry>() : null;
            
            try
            {
                if (diagnosticMode && diagnostics != null)
                {
                    diagnostics.ProcessingSteps.Add("Starting conversion from uploaded file");
                }
                
                // Parse the Swagger document
                var openApiDocument = await ParseSwaggerDocumentAsync(swaggerStream, diagnostics, diagnosticMode);
                
                if (openApiDocument == null)
                {
                    return new ConversionResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to parse Swagger document",
                        Diagnostics = diagnostics,
                        Timestamp = DateTime.UtcNow
                    };
                }
                
                // Convert to MCP JSON
                if (diagnosticMode && diagnostics != null) 
                    diagnostics.ProcessingSteps.Add("Converting Swagger to MCP JSON");
                
                var conversionStartTime = stopwatch.Elapsed.TotalMilliseconds;
                var mcpJson = _openApiConverter.ConvertToMcp(openApiDocument, sourceMap, diagnostics, diagnosticMode);
                
                if (diagnosticMode && diagnostics != null)
                {
                    diagnostics.PerformanceMetrics["ConversionTime"] = stopwatch.Elapsed.TotalMilliseconds - conversionStartTime;
                    diagnostics.ProcessingSteps.Add("Conversion completed successfully");
                }
                
                // Calculate content hash for idempotency
                string contentHash;
                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(mcpJson));
                    contentHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
                
                stopwatch.Stop();
                if (diagnosticMode && diagnostics != null)
                {
                    diagnostics.PerformanceMetrics["TotalTime"] = stopwatch.Elapsed.TotalMilliseconds;
                }
                
                return new ConversionResponse
                {
                    Success = true,
                    McpJson = mcpJson,
                    ContentHash = contentHash,
                    SourceMap = sourceMap,
                    Diagnostics = diagnostics,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting Swagger from stream");
                
                if (diagnosticMode && diagnostics != null)
                {
                    diagnostics.ProcessingSteps.Add($"Unexpected error: {ex.Message}");
                    diagnostics.Warnings.Add($"Exception: {ex.GetType().Name}");
                }
                
                return new ConversionResponse
                {
                    Success = false,
                    ErrorMessage = $"Conversion failed: {ex.Message}",
                    Diagnostics = diagnostics,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Detects the actual Swagger/OpenAPI JSON URL from a given URL
        /// </summary>
        /// <param name="url">URL to check, may be a Swagger UI URL or direct JSON URL</param>
        /// <param name="diagnosticMode">Whether to run in diagnostic mode</param>
        /// <param name="diagnostics">Diagnostics object to update if in diagnostic mode</param>
        /// <returns>The detected Swagger/OpenAPI JSON URL</returns>
        private async Task<string> DetectSwaggerJsonUrlAsync(string url, bool diagnosticMode, ConversionDiagnostics? diagnostics)
        {
            // Try each detector in sequence until one can handle the URL
            foreach (var detector in _urlDetectors)
            {
                if (detector.CanHandle(url))
                {
                    var detectedUrl = await detector.DetectSwaggerJsonUrlAsync(url, diagnosticMode, diagnostics);
                    if (detectedUrl != url) // If the detector found a different URL
                    {
                        _logger.LogInformation($"Detector {detector.GetType().Name} detected Swagger JSON URL: {detectedUrl}");
                        return detectedUrl;
                    }
                }
            }
            
            // If no detector found a different URL, return the original
            return url;
        }

        private async Task<OpenApiDocument?> ParseSwaggerDocumentAsync(Stream swaggerStream, ConversionDiagnostics? diagnostics, bool diagnosticMode)
        {
            var parseStartTime = Stopwatch.GetTimestamp();

            try
            {
                // Create a memory stream that we can seek
                using var memoryStream = new MemoryStream();
                await swaggerStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // Use OpenAPI reader to parse the document
                var openApiReaderSettings = new OpenApiReaderSettings
                {
                    ReferenceResolution = ReferenceResolutionSetting.ResolveLocalReferences
                };
                
                var reader = new OpenApiStreamReader(openApiReaderSettings);
                var result = await reader.ReadAsync(memoryStream);
                
                if (diagnosticMode && diagnostics != null)
                {
                    var elapsedMs = Stopwatch.GetElapsedTime(parseStartTime).TotalMilliseconds;
                    diagnostics.PerformanceMetrics["ParseTime"] = elapsedMs;
                    diagnostics.ProcessingSteps.Add($"Swagger document parsed successfully (format: {(result.OpenApiDiagnostic.SpecificationVersion)}, {result.OpenApiDocument.Paths.Count} paths)");
                    
                    // Add any OpenAPI diagnostic warnings
                    foreach (var error in result.OpenApiDiagnostic.Errors)
                    {
                        diagnostics.Warnings.Add($"OpenAPI Error: {error.Message}");
                    }
                    
                    foreach (var warning in result.OpenApiDiagnostic.Warnings)
                    {
                        diagnostics.Warnings.Add($"OpenAPI Warning: {warning.Message}");
                    }
                }
                
                return result.OpenApiDocument;
            }
            catch (Exception ex)
            {
                if (diagnosticMode && diagnostics != null)
                {
                    diagnostics.ProcessingSteps.Add($"Error parsing Swagger document: {ex.Message}");
                    diagnostics.Warnings.Add($"Parse exception: {ex.GetType().Name}");
                }
                
                _logger.LogError(ex, "Error parsing Swagger document");
                return null;
            }
        }
    }
}
