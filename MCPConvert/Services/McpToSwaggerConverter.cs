using System.Threading.Tasks;
using MCPConvert.Services;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using MCPToSwagger;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using System.Text;

namespace MCPConvert.Services
{
    /// <summary>
    /// Service for converting MCP JSON to Swagger/OpenAPI JSON using MCPToSwagger logic.
    /// </summary>
    public class McpToSwaggerConverter : IMcpToSwaggerConverter
    {
        private readonly ILogger<McpToSwaggerConverter> _logger;

        public McpToSwaggerConverter(ILogger<McpToSwaggerConverter> logger)
        {
            _logger = logger;
        }

        public async Task<string> ConvertMcpToSwaggerAsync(string mcpJson)
        {
            if (string.IsNullOrWhiteSpace(mcpJson))
                throw new ArgumentException("Input MCP JSON is empty.", nameof(mcpJson));

            try
            {
                // Use the MCPToSwagger converter from the other project
                var converter = new MCPToSwagger.McpToSwaggerConverter();
                OpenApiDocument openApiDoc = converter.ConvertFromJson(mcpJson);
                // Serialize OpenApiDocument to JSON
                var sb = new StringBuilder();
                using (var writer = new StringWriter(sb))
                {
                    var jsonWriter = new OpenApiJsonWriter(writer);
                    openApiDoc.SerializeAsV3(jsonWriter);
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting MCP to Swagger");
                throw;
            }
        }
    }
}
