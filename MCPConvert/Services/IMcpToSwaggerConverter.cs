using System.Threading.Tasks;

namespace MCPConvert.Services
{
    /// <summary>
    /// Interface for converting MCP JSON to Swagger/OpenAPI
    /// </summary>
    public interface IMcpToSwaggerConverter
    {
        /// <summary>
        /// Converts MCP JSON string to OpenAPI/Swagger JSON string.
        /// </summary>
        /// <param name="mcpJson">The MCP JSON input</param>
        /// <returns>Swagger/OpenAPI JSON output</returns>
        Task<string> ConvertMcpToSwaggerAsync(string mcpJson);
    }
}
