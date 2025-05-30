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
using Newtonsoft.Json.Linq;

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

                // Download the Swagger JSON from the URL or use the provided content
                string content = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(content))
                {
                    return new ConversionResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to fetch Swagger content",
                        Diagnostics = diagnostics,
                        Timestamp = DateTime.UtcNow
                    };
                }
                
                // Preprocess OpenAPI 3.1.0 documents to work around library limitations
                string originalContent = content;
                bool isOpenApi31 = false;
                try
                {
                    // Check if this is an OpenAPI 3.1.0 document
                    var jsonObj = JObject.Parse(content);
                    var openApiVersion = jsonObj["openapi"]?.ToString();
                    isOpenApi31 = openApiVersion == "3.1.0";
                    
                    if (isOpenApi31 && diagnosticMode && diagnostics != null)
                    {
                        diagnostics.ProcessingSteps.Add("Detected OpenAPI 3.1.0 document, preprocessing for compatibility");
                    }
                    
                    // Temporarily change the version to 3.0.0 for parsing
                    if (isOpenApi31)
                    {
                        jsonObj["openapi"] = "3.0.0";
                        ConvertTypeArraysToNullable(jsonObj);
                        content = jsonObj.ToString();
                    }
                }
                catch (Exception ex)
                {
                    if (diagnosticMode && diagnostics != null)
                    {
                        diagnostics.Warnings.Add($"Error preprocessing OpenAPI document: {ex.Message}");
                    }
                    // Continue with original content if preprocessing fails
                    content = originalContent;
                }

                // Parse the Swagger document
                using var swaggerStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
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
                
                // Preprocess for OpenAPI 3.1.0 support
                bool isOpenApi31 = false;
                string originalContent = string.Empty;
                try
                {
                    // First check if this is an OpenAPI 3.1.0 document
                    memoryStream.Position = 0;
                    using var streamReader = new StreamReader(memoryStream, leaveOpen: true);
                    var jsonContent = await streamReader.ReadToEndAsync();
                    originalContent = jsonContent; // Store the original content
                    memoryStream.Position = 0; // Reset for later use
                    
                    var jsonObj = JObject.Parse(jsonContent);
                    var openApiVersion = jsonObj["openapi"]?.ToString();
                    isOpenApi31 = openApiVersion == "3.1.0";
                    
                    if (isOpenApi31)
                    {
                        if (diagnosticMode && diagnostics != null)
                        {
                            diagnostics.ProcessingSteps.Add("Detected OpenAPI 3.1.0 document, preprocessing for compatibility");
                        }
                        
                        // Temporarily change version to 3.0.0 for parsing
                        jsonObj["openapi"] = "3.0.0";
                        
                        // Convert OpenAPI 3.1.0 type arrays with 'null' to OpenAPI 3.0 nullable:true
                        ConvertTypeArraysToNullable(jsonObj);
                        
                        // Pre-process any external references that might cause issues
                        PreProcessReferences(jsonObj);
                        
                        var modifiedContent = jsonObj.ToString();
                        
                        // Replace the stream with modified content
                        memoryStream.SetLength(0);
                        using var writer = new StreamWriter(memoryStream, leaveOpen: true);
                        await writer.WriteAsync(modifiedContent);
                        await writer.FlushAsync();
                        memoryStream.Position = 0;
                    }
                }
                catch (Exception ex)
                {
                    if (diagnosticMode && diagnostics != null)
                    {
                        diagnostics.Warnings.Add($"Error preprocessing OpenAPI document: {ex.Message}");
                    }
                    // Continue with original content if available, otherwise reset stream
                    if (!string.IsNullOrEmpty(originalContent))
                    {
                        memoryStream.SetLength(0);
                        using var writer = new StreamWriter(memoryStream, leaveOpen: true);
                        await writer.WriteAsync(originalContent);
                        await writer.FlushAsync();
                    }
                    memoryStream.Position = 0;
                }

                try
                {
                    // Use OpenAPI reader to parse the document with enhanced settings for OpenAPI 3.1.0 support
                    // Create a default BaseUrl for reference resolution
                    Uri baseUrl = new Uri("https://example.com/specs/");
                    
                    var openApiReaderSettings = new OpenApiReaderSettings
                    {
                        // Set to ResolveLocalReferences to avoid external reference failures
                        ReferenceResolution = ReferenceResolutionSetting.ResolveLocalReferences,
                        BaseUrl = baseUrl // Set the BaseUrl for reference resolution
                    };
                        
                    // Create a reader for parsing the document with enhanced settings
                    var reader = new OpenApiStreamReader(openApiReaderSettings);
                    var readResult = await reader.ReadAsync(memoryStream);
                    
                    // Store any errors or warnings for diagnostics
                    if (diagnosticMode && diagnostics != null && readResult.OpenApiDiagnostic != null)
                    {
                        // Add parsing errors to warnings in diagnostics (since there's no separate Errors collection)
                        foreach (var error in readResult.OpenApiDiagnostic.Errors)
                        {
                            diagnostics.Warnings.Add($"OpenAPI parsing error: {error.Message}");
                        }
                        
                        // Add parsing warnings to warnings in diagnostics
                        foreach (var warning in readResult.OpenApiDiagnostic.Warnings)
                        {
                            diagnostics.Warnings.Add($"OpenAPI parsing warning: {warning}");
                        }
                        
                        var elapsedMs = Stopwatch.GetElapsedTime(parseStartTime).TotalMilliseconds;
                        diagnostics.PerformanceMetrics["ParseTime"] = elapsedMs;
                        diagnostics.ProcessingSteps.Add($"Swagger document parsed successfully (format: {(readResult.OpenApiDiagnostic.SpecificationVersion)}, {readResult.OpenApiDocument.Paths.Count} paths)");
                    }
                    
                    return readResult.OpenApiDocument;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("external references") || ex.Message.Contains("reference"))
                    {
                        Console.Error.WriteLine($"Error parsing Swagger: External reference issue. {ex.Message}");
                        throw; // Re-throw the original exception
                    }
                    else
                    {
                        Console.Error.WriteLine($"Unexpected error parsing Swagger document: {ex.ToString()}");
                        if (diagnosticMode && diagnostics != null)
                        {
                            diagnostics.ProcessingSteps.Add($"Unexpected error during parsing: {ex.Message}");
                            var elapsedMs = Stopwatch.GetElapsedTime(parseStartTime).TotalMilliseconds;
                            diagnostics.PerformanceMetrics["ParseTime"] = elapsedMs;
                        }
                        throw; // Re-throw
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unexpected error parsing Swagger document: {ex.ToString()}");
                if (diagnosticMode && diagnostics != null)
                {
                    diagnostics.ProcessingSteps.Add($"Unexpected error during parsing: {ex.Message}");
                }
                throw; // Re-throw
            }
        }
        
        /// <summary>
        /// Recursively converts OpenAPI 3.1.0 type arrays with 'null' to OpenAPI 3.0 nullable:true format
        /// </summary>
        /// <param name="token">The JSON token to process</param>
        private void ConvertTypeArraysToNullable(JToken token)
        {
            if (token is JObject obj)
            {
                // Check if this object has a type property that is an array (OpenAPI 3.1 style)
                if (obj["type"] is JArray typeArray)
                {
                    // If it's a type array that includes null
                    bool hasNullType = false;
                    string primaryType = null;
                    
                    foreach (var item in typeArray)
                    {
                        string typeValue = item.ToString();
                        if (typeValue == "null")
                        {
                            hasNullType = true;
                        }
                        else
                        {
                            primaryType = typeValue; // Store the non-null type
                        }
                    }
                    
                    // Convert ["string", "null"] to {"type": "string", "nullable": true}
                    if (hasNullType && primaryType != null)
                    {
                        obj["type"] = primaryType;
                        obj["nullable"] = true; 
                    }
                }
                
                // INDEPENDENTLY check for existing nullable flag (OpenAPI 3.0 style or already converted 3.1 style)
                // This reinforces it even if a 'type' property exists (string or array).
                if (obj["nullable"]?.Value<bool>() == true)
                {
                    // Ensure the nullable flag remains true in the preprocessed JSON
                    obj["nullable"] = true; 
                }

                // Process all properties of this object
                foreach (var property in obj.Properties())
                {
                    ConvertTypeArraysToNullable(property.Value);
                }
            }
            else if (token is JArray array)
            {
                // Process all items in the array
                foreach (var item in array)
                {
                    ConvertTypeArraysToNullable(item);
                }
            }
        }
        
        /// <summary>
        /// Pre-processes references in the OpenAPI document to handle potential issues
        /// </summary>
        /// <param name="token">The JSON token to process</param>
        private void PreProcessReferences(JToken token)
        {
            if (token is JObject obj)
            {
                // Check if this object has both a $ref property and other properties (OpenAPI 3.1 feature)
                if (obj["$ref"] != null && obj.Properties().Count() > 1)
                {
                    // In OpenAPI 3.0, $ref cannot have siblings, so we need to handle this specially
                    // One approach is to convert to allOf with the reference as the first item
                    string refValue = obj["$ref"].ToString();
                    
                    // Skip if it appears to be an external reference that could cause issues
                    if (refValue.StartsWith("http") || refValue.Contains("#/components/examples/"))
                    {
                        // Remove the external reference to prevent parsing errors
                        obj.Remove("$ref");
                    }
                    else if (!refValue.StartsWith("#/components/schemas/"))
                    {
                        // For non-schema references with siblings, convert to allOf structure
                        var refObj = new JObject();
                        refObj["$ref"] = refValue;
                        
                        // Create a clone without the $ref property
                        var propertiesObj = new JObject();
                        foreach (var prop in obj.Properties().Where(p => p.Name != "$ref"))
                        {
                            propertiesObj[prop.Name] = prop.Value;
                        }
                        
                        // Clear all properties and set up allOf
                        obj.RemoveAll();
                        var allOfArray = new JArray();
                        allOfArray.Add(refObj);
                        allOfArray.Add(propertiesObj);
                        obj["allOf"] = allOfArray;
                    }
                }
                
                // Process all properties of this object
                foreach (var property in obj.Properties().ToList())
                {
                    PreProcessReferences(property.Value);
                }
            }
            else if (token is JArray array)
            {
                // Process all items in the array
                foreach (var item in array)
                {
                    PreProcessReferences(item);
                }
            }
        }
    }
}
