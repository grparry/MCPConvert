using Microsoft.OpenApi.Models;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;

namespace MCPConvert.Services.Utilities
{
    /// <summary>
    /// Utility class for converting OpenAPI schema types to JSON Schema types with OpenAPI 3.1.0 support
    /// </summary>
    public class SchemaTypeConverter
    {
        private readonly ILogger<SchemaTypeConverter> _logger;
        private OpenApiDocument? _openApiDocument; // Store the document reference

        public SchemaTypeConverter(ILogger<SchemaTypeConverter> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Converts an OpenAPI schema type to a JSON Schema type
        /// </summary>
        /// <param name="openApiType">The OpenAPI schema type</param>
        /// <returns>The corresponding JSON Schema type</returns>
        public string GetJsonSchemaType(string? openApiType)
        {
            return openApiType switch
            {
                "integer" => "integer",
                "number" => "number",
                "boolean" => "boolean",
                "array" => "array",
                "object" => "object",
                _ => "string"  // Default to string for null or unknown types
            };
        }

        /// <summary>
        /// Public entry point - initializes the process
        /// </summary>
        /// <param name="schema">The OpenAPI schema to process</param>
        /// <param name="openApiDocument">The containing OpenAPI document for resolving refs</param>
        /// <returns>A JObject representing the schema in MCP format</returns>
        public JObject Convert(OpenApiSchema schema, OpenApiDocument openApiDocument)
        {
            _openApiDocument = openApiDocument ?? throw new ArgumentNullException(nameof(openApiDocument));
            // Initialize the HashSet here for the top-level call
            return ProcessSchema(schema, openApiDocument, new HashSet<string>());
        }

        /// <summary>
        /// Processes a schema according to OpenAPI 3.1.0 rules and converts it to MCP schema format
        /// </summary>
        /// <param name="schema">The OpenAPI schema to process</param>
        /// <param name="openApiDocument">The containing OpenAPI document for resolving refs</param>
        /// <param name="processedRefs">A set of processed schema references to track circular references</param>
        /// <returns>A JObject representing the schema in MCP format</returns>
        private JObject ProcessSchema(OpenApiSchema schema, OpenApiDocument openApiDocument, HashSet<string> processedRefs)
        {
            JObject result = new JObject();
            
            // --- Start Revised $ref Handling ---
            // Handle schemas that ARE references first
            if (schema.Reference != null && !schema.Reference.IsExternal) // Check if it's an internal reference
            {
                string refId = schema.Reference.ReferenceV3; // e.g., "#/components/schemas/legacyNullable"
                // Extract the component name (e.g., "legacyNullable") for lookup and cycle tracking
                string refComponentId = refId.Contains('/') ? refId.Substring(refId.LastIndexOf('/') + 1) : refId;


                // Check for circular references BEFORE trying to resolve
                if (processedRefs.Contains(refComponentId))
                {
                    _logger.LogWarning($"Circular reference detected: {refComponentId}. Returning basic $ref.");
                    return new JObject { ["$ref"] = refId }; // Return just the ref to break the cycle
                }

                // Attempt to resolve the reference using the component ID
                if (openApiDocument?.Components?.Schemas != null &&
                    openApiDocument.Components.Schemas.TryGetValue(refComponentId, out var referencedSchema))
                {
                    // Mark this reference as being processed
                    processedRefs.Add(refComponentId);

                    try {
                        _logger.LogDebug($"Processing referenced schema: {refComponentId}");
                        // Recursively process the *referenced* schema
                        // The result of this call IS the schema we want, including its 'nullable' status etc.
                        // Pass the _openApiDocument instance down
                        var resolvedRefContent = ProcessSchema(referencedSchema, openApiDocument, processedRefs); // Pass component ID as context

                        _logger.LogInformation(">>>> DEBUG: Type of resolvedRefContent after call: {Type}, Value: {Json}",
                            resolvedRefContent?.GetType()?.FullName ?? "null",
                            resolvedRefContent?.ToString(Newtonsoft.Json.Formatting.None) ?? "null");

                        _logger.LogTrace("Resolved $ref: {RefId}. Initial result from recursive call: {ResultJson}", 
                            schema.Reference.Id, resolvedRefContent.ToString(Newtonsoft.Json.Formatting.None));

                        // If the resolvedRefContent is just a $ref (not a circular reference), try to expand it fully
                        while (resolvedRefContent.Count == 1 && resolvedRefContent["$ref"] != null)
                        {
                            var nextRef = resolvedRefContent["$ref"].ToString();
                            var nextComponentId = nextRef.Contains('/') ? nextRef.Substring(nextRef.LastIndexOf('/') + 1) : nextRef;
                            if (processedRefs.Contains(nextComponentId))
                            {
                                _logger.LogWarning($"Circular reference detected during deep expansion: {nextComponentId}. Returning basic $ref.");
                                break;
                            }
                            if (openApiDocument.Components.Schemas.TryGetValue(nextComponentId, out var nextSchema))
                            {
                                processedRefs.Add(nextComponentId);
                                resolvedRefContent = ProcessSchema(nextSchema, openApiDocument, processedRefs);
                                processedRefs.Remove(nextComponentId);
                            }
                            else
                            {
                                break;
                            }
                        }

                        // Check if the referencing schema has sibling properties that need merging
                        if (!string.IsNullOrEmpty(schema.Type) || schema.Properties?.Count > 0 ||
                            schema.Items != null || schema.AllOf?.Count > 0 ||
                            schema.AnyOf?.Count > 0 || schema.OneOf?.Count > 0 || 
                            !string.IsNullOrEmpty(schema.Description) || schema.Nullable || schema.Deprecated)
                        {
                            _logger.LogDebug("Ref '{RefId}' has sibling properties. Merging onto resolved content.", schema.Reference.Id);
                            _logger.LogDebug("Referencing Schema ('{RefId}') properties: Nullable={Nullable}, Description='{Description}', Deprecated={Deprecated}", 
                                schema.Reference.Id, schema.Nullable, schema.Description ?? "(null)", schema.Deprecated);
                            
                            // Start with a *copy* of the resolved content
                            JObject mergedResult = new JObject(resolvedRefContent);
                            _logger.LogDebug("Result BEFORE merge (resolved content): {ResultJson}", mergedResult.ToString(Newtonsoft.Json.Formatting.None));

                            // Merge sibling properties from the referencing schema onto the resolved content
                            // Avoid overwriting properties already present in the resolved schema unless necessary
                            if (schema.Description != null && mergedResult["description"] == null)
                            {
                                mergedResult["description"] = schema.Description;
                                _logger.LogDebug("Applied 'description' from referencing schema '{RefId}'.", schema.Reference.Id);
                            }
                            if (schema.Deprecated && mergedResult["deprecated"] == null)
                            {
                                mergedResult["deprecated"] = schema.Deprecated;
                                _logger.LogDebug("Applied 'deprecated: true' from referencing schema '{RefId}'.", schema.Reference.Id);
                            }
                            // TODO: Add merging for other relevant sibling properties (e.g., title, example, externalDocs) if needed

                            // Handle nullable: Explicit nullable=true on the referencing schema OVERRIDES resolved content
                            if (schema.Nullable) // Check legacy Nullable on the referencing schema itself
                            {
                                mergedResult["nullable"] = true; // Override/set nullable on the merged result
                                _logger.LogDebug("Applied/Overrode 'nullable: true' based on referencing schema '{RefId}'.", schema.Reference.Id);
                            }
                            else
                            {
                                 _logger.LogDebug("Referencing schema '{RefId}' does not set legacy nullable=true. Nullable status depends on resolved schema ({IsNullable}).", 
                                    schema.Reference.Id, mergedResult["nullable"] != null && mergedResult["nullable"].Value<bool>());
                            }
                            
                            result = mergedResult; // Use the merged object
                            _logger.LogDebug("Result AFTER merge: {ResultJson}", result.ToString(Newtonsoft.Json.Formatting.None));
                        }
                        else
                        {
                            // No sibling properties, just use the resolved content directly
                            result = resolvedRefContent;
                            _logger.LogTrace("Ref '{RefId}' has no sibling properties. Using resolved content directly.", schema.Reference.Id);
                        }
                    }
                    finally {
                        // Ensure we remove from tracking even if an error occurs during processing
                         processedRefs.Remove(refComponentId);
                    }

                    // Return the fully processed result (either the resolved schema or the merged one)
                    return result;
                }
                else
                {
                    _logger.LogWarning($"Could not resolve internal reference: {refId}. Returning error schema instead of $ref.");
                    // Couldn't resolve, output an explicit error schema for clarity
                    return new JObject {
                        ["error"] = $"Unresolvable $ref: {refId}",
                        ["originalRef"] = refId
                    };
                }
            }
            // --- End Revised $ref Handling ---
            // If we reach here, the schema was NOT primarily a reference (or was an unresolvable/external one)
            // Proceed with processing other properties.

            // Add log here to check if this block is entered for specific schemas
            _logger.LogTrace("Entered non-$ref processing block for schema. Type: {SchemaType}, Nullable: {IsNullable}", schema?.Type ?? "(null)", schema?.Nullable ?? false);

            _logger.LogTrace($"Processing non-ref schema. Schema Type: {schema.Type}, Format: {schema.Format}, Nullable: {schema.Nullable}"); // Log schema properties

            // TEMP DEBUG: Dump the schema object for 'nullableString' property to inspect OpenAPI.NET representation
            if (schema?.Title == "nullableString" || schema?.Description?.Contains("nullableString") == true)
            {
                _logger.LogInformation("DEBUG DUMP: OpenApiSchema for 'nullableString': {SchemaDump}", Newtonsoft.Json.JsonConvert.SerializeObject(schema));
            }

            // --- OpenAPI 3.1.0 type array nullability handling via AnyOf ---
            // If schema.AnyOf contains exactly two schemas, one with Type == "null" and one with a non-null type, treat as nullable
            if (schema.AnyOf != null && schema.AnyOf.Count == 2)
            {
                var nullSchema = schema.AnyOf.FirstOrDefault(s => s.Type == "null");
                var nonNullSchema = schema.AnyOf.FirstOrDefault(s => s.Type != "null");
                if (nullSchema != null && nonNullSchema != null && !string.IsNullOrEmpty(nonNullSchema.Type))
                {
                    result["type"] = GetJsonSchemaType(nonNullSchema.Type);
                    result["nullable"] = true;
                    _logger.LogTrace("Detected OpenAPI 3.1.0 anyOf with null: setting type='{Type}' and nullable=true", nonNullSchema.Type);
                }
                else if (!string.IsNullOrEmpty(schema.Type))
                {
                    result["type"] = GetJsonSchemaType(schema.Type);
                }
            }
            if (schema.Type is string typeStr && !string.IsNullOrEmpty(typeStr))
            {
                result["type"] = GetJsonSchemaType(typeStr);
            }

            if (schema.Nullable)
            {
                _logger.LogTrace("Setting 'nullable: true' based on schema.Nullable");
                result["nullable"] = true;
            }
            else if (schema.Type == null && schema.AnyOf?.Any(s => s.Type == "null") == true)
            {
                // Handle OpenAPI 3.1 style nullability where type is an array including 'null'
                // We might need a more robust way to handle the 'type' array later
                _logger.LogTrace("Setting 'nullable: true' based on AnyOf containing type 'null'");
                result["nullable"] = true;
                // Attempt to determine the primary type if not 'null'
                var nonNullType = schema.AnyOf.FirstOrDefault(s => s.Type != "null")?.Type;
                if (!string.IsNullOrEmpty(nonNullType))
                {
                    result["type"] = GetJsonSchemaType(nonNullType);
                }
            }
            
            _logger.LogTrace($"Result object after initial nullable/type processing: {result.ToString(Newtonsoft.Json.Formatting.None)}"); // Log result state

            // Handle basic properties (description is handled carefully below)
            if (!string.IsNullOrEmpty(schema.Description))
            {
                 // Add description ONLY if the result doesn't already have one (e.g., from a resolved ref)
                 if (!result.ContainsKey("description"))
                 {
                     result["description"] = schema.Description;
                 }
            }

            // Handle format
            if (!string.IsNullOrEmpty(schema.Format))
            {
                result["format"] = schema.Format;
            }

            // Handle enum values
            if (schema.Enum?.Count > 0)
            {
                result["enum"] = new JArray(schema.Enum.Select(e => JToken.FromObject(e)));
            }

            // Handle object properties
            if (schema.Type == "object" && schema.Properties?.Count > 0)
            {
                result["properties"] = new JObject();
                foreach (var prop in schema.Properties)
                {
                    var propertySchema = ProcessSchema(prop.Value, openApiDocument, processedRefs);
                    result["properties"][prop.Key] = propertySchema;
                    _logger.LogTrace("Assigned property '{PropertyName}' to result properties. Assigned Value: {PropertyValueJson}",
                        prop.Key,
                        propertySchema?.ToString(Newtonsoft.Json.Formatting.None) ?? "null");
                }
                // Add required properties
                if (schema.Required?.Count > 0)
                {
                    result["required"] = new JArray(schema.Required);
                }
            }

            // Handle array items
            if (schema.Type == "array" && schema.Items != null)
            {
                 // Pass the _openApiDocument instance down
                result["items"] = ProcessSchema(schema.Items, openApiDocument, processedRefs);
            }

            // Handle composition schemas (OpenAPI 3.1.0 feature)
            if (schema.OneOf?.Count > 0)
            {
                // Pass the correct processedRefs
                result["oneOf"] = ProcessSchemaComposition(schema.OneOf, openApiDocument, processedRefs);
                
                // Handle discriminator if present
                if (schema.Discriminator != null)
                {
                    result["discriminator"] = new JObject
                    {
                        ["propertyName"] = schema.Discriminator.PropertyName
                    };

                    if (schema.Discriminator.Mapping?.Count > 0)
                    {
                        var mapping = new JObject();
                        foreach (var item in schema.Discriminator.Mapping)
                        {
                            mapping[item.Key] = item.Value;
                        }
                        ((JObject)result["discriminator"])["mapping"] = mapping;
                    }
                }
            }
            if (schema.AnyOf?.Count > 0)
            {
                 // Pass the correct processedRefs
                result["anyOf"] = ProcessSchemaComposition(schema.AnyOf, openApiDocument, processedRefs);
            }
            if (schema.AllOf?.Count > 0)
            {
                 // Pass the correct processedRefs
                result["allOf"] = ProcessSchemaComposition(schema.AllOf, openApiDocument, processedRefs);
            }

            // Use reference ID if available, otherwise use schema type for logging
            string finalSchemaId = schema.Reference?.Id ?? $"Inline Type: {schema.Type ?? "(Unknown)"}";
            _logger.LogDebug("Final ProcessSchema Result for '{SchemaId}': {Json}", 
                finalSchemaId,
                result.ToString(Newtonsoft.Json.Formatting.None));

            return result;
        }

        // Helper for processing composition schemas
        private JArray ProcessSchemaComposition(IList<OpenApiSchema> schemas, OpenApiDocument openApiDocument, HashSet<string> processedRefs)
        {
            var array = new JArray();
            foreach (var schema in schemas)
            {
                // Recursively process each schema in the list, passing the HashSet
                array.Add(ProcessSchema(schema, openApiDocument, processedRefs));
            }
            return array;
        }
    }
}
