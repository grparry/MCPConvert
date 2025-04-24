namespace MCPConvert.Services.Utilities
{
    /// <summary>
    /// Utility class for converting OpenAPI schema types to JSON Schema types
    /// </summary>
    public class SchemaTypeConverter
    {
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
    }
}
