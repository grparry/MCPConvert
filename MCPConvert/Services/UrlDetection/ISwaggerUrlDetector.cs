using System.Threading.Tasks;

namespace MCPConvert.Services.UrlDetection
{
    /// <summary>
    /// Interface for detecting Swagger/OpenAPI JSON URLs
    /// </summary>
    public interface ISwaggerUrlDetector
    {
        /// <summary>
        /// Determines if this detector can handle the given URL
        /// </summary>
        /// <param name="url">URL to check</param>
        /// <returns>True if this detector can handle the URL</returns>
        bool CanHandle(string url);
        
        /// <summary>
        /// Attempts to detect the actual Swagger/OpenAPI JSON URL from a given URL
        /// </summary>
        /// <param name="url">URL to check, may be a Swagger UI URL or direct JSON URL</param>
        /// <param name="diagnosticMode">Whether to run in diagnostic mode</param>
        /// <param name="diagnostics">Diagnostics object to update if in diagnostic mode</param>
        /// <returns>The detected Swagger/OpenAPI JSON URL, or the original URL if detection fails</returns>
        Task<string> DetectSwaggerJsonUrlAsync(string url, bool diagnosticMode, Models.ConversionDiagnostics? diagnostics);
    }
}
