using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using MCPConvert.Models;
using MCPConvert.Services.Utilities;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace MCPConvert.Services.Conversion
{
    /// <summary>
    /// Converts OpenAPI documents to MCP JSON format
    /// </summary>
    /// <remarks>
    /// Supports OpenAPI 2.0, 3.0, and 3.1.0 specifications with full feature support for all versions.
    /// OpenAPI 3.1.0 features such as type arrays, schema composition, discriminators, and extended references
    /// are properly handled and converted to appropriate MCP schema representations.
    /// </remarks>
    public class OpenApiToMcpConverter
    {
        private readonly ILogger<OpenApiToMcpConverter> _logger;
        private readonly SchemaTypeConverter _schemaTypeConverter;

        public OpenApiToMcpConverter(ILogger<OpenApiToMcpConverter> logger, SchemaTypeConverter schemaTypeConverter)
        {
            _logger = logger;
            _schemaTypeConverter = schemaTypeConverter;
        }

        /// <summary>
        /// Converts an OpenAPI document to MCP JSON
        /// </summary>
        /// <param name="openApiDocument">The OpenAPI document to convert</param>
        /// <param name="sourceMap">Optional source mapping dictionary</param>
        /// <param name="diagnostics">Optional diagnostics object</param>
        /// <param name="diagnosticMode">Whether to run in diagnostic mode</param>
        /// <returns>MCP JSON string</returns>
        public virtual string ConvertToMcp(OpenApiDocument openApiDocument, Dictionary<string, SourceMapEntry>? sourceMap, ConversionDiagnostics? diagnostics, bool diagnosticMode)
        {
            try
            {
                // Log the detected OpenAPI version
                string openApiVersion = DetectOpenApiVersion(openApiDocument);
                _logger.LogInformation($"Converting OpenAPI {openApiVersion} document to MCP format");
                
                if (diagnosticMode && diagnostics != null)
                {
                    diagnostics.ProcessingSteps.Add($"Detected OpenAPI version: {openApiVersion}");
                }
                
                // Create the MCP context structure
                var mcpContext = new JObject
                {
                    ["schema"] = "mcp",
                    ["version"] = "0.1.0",
                    ["metadata"] = new JObject
                    {
                        ["title"] = openApiDocument.Info.Title ?? "API",
                        ["description"] = openApiDocument.Info.Description ?? "",
                        ["version"] = openApiDocument.Info.Version ?? "1.0.0"
                    },
                    ["tools"] = new JArray(),
                    ["schemas"] = new JObject() // Add schemas object to ensure it's not null
                };
                
                // Process each path and operation in the OpenAPI document
                foreach (var pathItem in openApiDocument.Paths)
                {
                    string path = pathItem.Key;
                    
                    foreach (var operation in pathItem.Value.Operations)
                    {
                        string method = operation.Key.ToString().ToLowerInvariant();
                        var operationValue = operation.Value;
                        
                        // Create a tool for each operation
                        var tool = new JObject
                        {
                            ["name"] = GetToolName(operationValue, path, method),
                            ["description"] = operationValue.Description ?? operationValue.Summary ?? $"{method.ToUpperInvariant()} {path}",
                            ["parameters"] = new JObject
                            {
                                ["type"] = "object",
                                ["properties"] = new JObject()
                            }
                        };
                        
                        // Add parameters
                        var properties = (JObject)tool["parameters"]["properties"];
                        var requiredParams = new JArray();
                        
                        // Path parameters
                        foreach (var parameter in operationValue.Parameters.Where(p => p.In == ParameterLocation.Path))
                        {
                            AddParameterToTool(properties, parameter, requiredParams, sourceMap, $"{path}.{method}.parameters.{parameter.Name}", openApiDocument);
                        }
                        
                        // Query parameters
                        foreach (var parameter in operationValue.Parameters.Where(p => p.In == ParameterLocation.Query))
                        {
                            AddParameterToTool(properties, parameter, requiredParams, sourceMap, $"{path}.{method}.parameters.{parameter.Name}", openApiDocument);
                        }
                        
                        // Request body
                        if (operationValue.RequestBody != null)
                        {
                            // Find JSON content type
                            var jsonContent = operationValue.RequestBody.Content.FirstOrDefault(c => 
                                c.Key.Contains("json", StringComparison.OrdinalIgnoreCase));
                                
                            if (jsonContent.Value != null && jsonContent.Value.Schema != null)
                            {
                                // Add body parameter
                                var bodyParam = new JObject
                                {
                                    ["description"] = operationValue.RequestBody.Description ?? "Request body",
                                    ["type"] = _schemaTypeConverter.GetJsonSchemaType(jsonContent.Value.Schema.Type)
                                };
                                
                                // Use the public Convert method
                                var processedSchema = _schemaTypeConverter.Convert(jsonContent.Value.Schema, openApiDocument);
                                
                                // Copy all properties from the processed schema to the body parameter
                                foreach (var prop in processedSchema.Properties())
                                {
                                    bodyParam[prop.Name] = prop.Value;
                                }
                                
                                // Add source mapping for body schema properties if needed
                                if (sourceMap != null && jsonContent.Value.Schema.Properties?.Count > 0)
                                {
                                    foreach (var prop in jsonContent.Value.Schema.Properties)
                                    {
                                        sourceMap[$"tools[{((JArray)mcpContext["tools"]).Count}].parameters.properties.body.properties.{prop.Key}"] = 
                                            new SourceMapEntry { SwaggerPath = $"{path}.{method}.requestBody.content.{jsonContent.Key}.schema.properties.{prop.Key}", LineNumber = 0 };
                                    }
                                }
                                
                                properties["body"] = bodyParam;
                                
                                if (operationValue.RequestBody.Required)
                                {
                                    requiredParams.Add("body");
                                }
                                
                                if (sourceMap != null)
                                {
                                    sourceMap[$"tools[{((JArray)mcpContext["tools"]).Count}].parameters.properties.body"] = 
                                        new SourceMapEntry { SwaggerPath = $"{path}.{method}.requestBody", LineNumber = 0 };
                                }
                            }
                        }
                        
                        // Add required parameters if any
                        if (requiredParams.Count > 0)
                        {
                            tool["parameters"]["required"] = requiredParams;
                        }
                        
                        // Add source mapping for the tool
                        if (sourceMap != null)
                        {
                            sourceMap[$"tools[{((JArray)mcpContext["tools"]).Count}]"] = 
                                new SourceMapEntry { SwaggerPath = $"{path}.{method}", LineNumber = 0 };
                        }
                        
                        // Add the tool to the tools array
                        ((JArray)mcpContext["tools"]).Add(tool);
                    }
                }
                
                // Process schemas from the OpenAPI document
                if (openApiDocument.Components?.Schemas != null && openApiDocument.Components.Schemas.Count > 0)
                {
                    var schemasObj = (JObject)mcpContext["schemas"];
                    
                    foreach (var schema in openApiDocument.Components.Schemas)
                    {
                        var schemaName = schema.Key;
                        var schemaValue = schema.Value;
                        
                        // Use the public Convert method
                        var mcpSchema = _schemaTypeConverter.Convert(schemaValue, openApiDocument);
                        
                        schemasObj[schemaName] = mcpSchema;
                        
                        // Add source mapping if available
                        if (sourceMap != null)
                        {
                            sourceMap[$"schemas.{schemaName}"] = 
                                new SourceMapEntry { SwaggerPath = $"components.schemas.{schemaName}", LineNumber = 0 };
                        }
                    }
                }
                
                return mcpContext.ToString();
            }
            catch (Exception ex)
            {
                if (diagnosticMode && diagnostics != null)
                {
                    diagnostics.ProcessingSteps.Add($"Error during MCP conversion: {ex.Message}");
                    diagnostics.Warnings.Add($"Conversion exception: {ex.GetType().Name}");
                }
                
                _logger.LogError(ex, "Error converting OpenAPI to MCP");
                throw;
            }
        }
        
        /// <summary>
        /// Detects the OpenAPI version from the document
        /// </summary>
        /// <param name="document">The OpenAPI document</param>
        /// <returns>The detected OpenAPI version as a string</returns>
        private string DetectOpenApiVersion(OpenApiDocument document)
        {
            if (document == null)
            {
                return "unknown";
            }
            
            // In the current version, we need to manually check the document info
            // Since there's no direct version property, we can use the Info.Version as a fallback
            // or we could parse the JSON directly to find the openapi version field
            return document.Info?.Version ?? "unknown";
        }

        private string GetToolName(OpenApiOperation operation, string path, string method)
        {
            // First try to use operationId if available
            if (operation != null && !string.IsNullOrEmpty(operation.OperationId))
            {
                return operation.OperationId;
            }
            
            // Otherwise, generate a name from the path and method
            var pathSegments = path.Split('/')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => s.StartsWith("{") && s.EndsWith("}") ? $"By{s.Substring(1, s.Length - 2)}" : s)
                .ToList();
                
            if (pathSegments.Count == 0)
            {
                return $"{method}Root";
            }
            
            // Convert to camelCase
            var result = method + string.Join("", pathSegments.Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1)));
            
            // Remove non-alphanumeric characters
            return Regex.Replace(result, "[^a-zA-Z0-9]", "");
        }
        
        private void AddParameterToTool(JObject properties, OpenApiParameter parameter, JArray requiredParams, Dictionary<string, SourceMapEntry>? sourceMap, string swaggerPath, OpenApiDocument openApiDocument)
        {
            if (properties == null || parameter == null || parameter.Schema == null) return;

            // Use the public Convert method
            var processedSchema = _schemaTypeConverter.Convert(parameter.Schema, openApiDocument);

            // Add a description if not already present in the processed schema
            if (!processedSchema.ContainsKey("description"))
            {
                processedSchema["description"] = parameter.Description ?? parameter.Name;
            }

            _logger.LogDebug("Assigning final processed schema for parameter '{ParamName}': {Json}", 
                parameter.Name, 
                processedSchema.ToString(Newtonsoft.Json.Formatting.None));

            properties[parameter.Name] = processedSchema;

            if (parameter.Required)
            {
                requiredParams.Add(parameter.Name);
            }

            // Add source mapping
            if (sourceMap != null)
            {
                int toolIndex = 0; // This would need to be passed in from the calling context
                sourceMap[$"tools[{toolIndex}].parameters.properties.{parameter.Name}"] =
                    new SourceMapEntry { SwaggerPath = swaggerPath, LineNumber = 0 };
            }
        }
    }
}
