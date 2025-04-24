using System.IO;
using System.Threading.Tasks;
using MCPConvert.Models;

namespace MCPConvert.Services
{
    /// <summary>
    /// Interface for converting Swagger/OpenAPI documents to MCP JSON
    /// </summary>
    public interface ISwaggerToMcpConverter
    {
        /// <summary>
        /// Converts a Swagger/OpenAPI document from a URL to MCP JSON
        /// </summary>
        /// <param name="swaggerUrl">URL to the Swagger/OpenAPI document</param>
        /// <param name="includeSourceMapping">Whether to include source mapping in the output</param>
        /// <param name="diagnosticMode">Whether to run in diagnostic mode</param>
        /// <returns>Conversion response with MCP JSON or error</returns>
        Task<ConversionResponse> ConvertFromUrlAsync(string swaggerUrl, bool includeSourceMapping = true, bool diagnosticMode = false);

        /// <summary>
        /// Converts a Swagger/OpenAPI document from a stream to MCP JSON
        /// </summary>
        /// <param name="swaggerStream">Stream containing the Swagger/OpenAPI document</param>
        /// <param name="includeSourceMapping">Whether to include source mapping in the output</param>
        /// <param name="diagnosticMode">Whether to run in diagnostic mode</param>
        /// <returns>Conversion response with MCP JSON or error</returns>
        Task<ConversionResponse> ConvertFromStreamAsync(Stream swaggerStream, bool includeSourceMapping = true, bool diagnosticMode = false);
    }
}
