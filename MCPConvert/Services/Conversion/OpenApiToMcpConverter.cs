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
    /// TODO: Add support for OpenAPI 3.1.0 specifications. Currently, only OpenAPI 2.0 and 3.0
    /// specifications are supported by the underlying parser library.
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
                    ["tools"] = new JArray()
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
                            AddParameterToTool(properties, parameter, requiredParams, sourceMap, $"{path}.{method}.parameters.{parameter.Name}");
                        }
                        
                        // Query parameters
                        foreach (var parameter in operationValue.Parameters.Where(p => p.In == ParameterLocation.Query))
                        {
                            AddParameterToTool(properties, parameter, requiredParams, sourceMap, $"{path}.{method}.parameters.{parameter.Name}");
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
                                
                                // Handle schema properties for objects
                                if (jsonContent.Value.Schema.Type == "object" && jsonContent.Value.Schema.Properties.Count > 0)
                                {
                                    bodyParam["properties"] = new JObject();
                                    
                                    foreach (var prop in jsonContent.Value.Schema.Properties)
                                    {
                                        ((JObject)bodyParam["properties"])[prop.Key] = new JObject
                                        {
                                            ["type"] = _schemaTypeConverter.GetJsonSchemaType(prop.Value.Type),
                                            ["description"] = prop.Value.Description ?? prop.Key
                                        };
                                        
                                        if (sourceMap != null)
                                        {
                                            sourceMap[$"tools[{((JArray)mcpContext["tools"]).Count}].parameters.properties.body.properties.{prop.Key}"] = 
                                                new SourceMapEntry { SwaggerPath = $"{path}.{method}.requestBody.content.{jsonContent.Key}.schema.properties.{prop.Key}", LineNumber = 0 };
                                        }
                                    }
                                    
                                    if (jsonContent.Value.Schema.Required.Count > 0)
                                    {
                                        bodyParam["required"] = new JArray(jsonContent.Value.Schema.Required);
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
        
        private void AddParameterToTool(JObject properties, OpenApiParameter parameter, JArray requiredParams, Dictionary<string, SourceMapEntry>? sourceMap, string swaggerPath)
        {
            if (properties == null || parameter == null || parameter.Schema == null) return;
            
            var paramObj = new JObject
            {
                ["description"] = parameter.Description ?? parameter.Name,
                ["type"] = _schemaTypeConverter.GetJsonSchemaType(parameter.Schema.Type)
            };
            
            // Add enum values if present
            if (parameter.Schema.Enum != null && parameter.Schema.Enum.Count > 0)
            {
                paramObj["enum"] = new JArray(parameter.Schema.Enum.Select(e => JToken.FromObject(e)));
            }
            
            properties[parameter.Name] = paramObj;
            
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
