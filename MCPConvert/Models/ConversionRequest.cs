using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MCPConvert.Models
{
    /// <summary>
    /// Represents a request to convert Swagger/OpenAPI to MCP JSON
    /// </summary>
    public class ConversionRequest
    {
        /// <summary>
        /// URL to a Swagger/OpenAPI JSON or YAML file
        /// </summary>
        [Display(Name = "Swagger URL")]
        public string? SwaggerUrl { get; set; }

        /// <summary>
        /// Uploaded Swagger/OpenAPI file
        /// </summary>
        [Display(Name = "Swagger File")]
        public IFormFile? SwaggerFile { get; set; }

        /// <summary>
        /// Whether to include source mapping in the output
        /// </summary>
        [Display(Name = "Include Source Mapping")]
        public bool IncludeSourceMapping { get; set; } = true;

        /// <summary>
        /// Whether to run in diagnostic mode
        /// </summary>
        [Display(Name = "Diagnostic Mode")]
        public bool DiagnosticMode { get; set; } = false;

        /// <summary>
        /// Validates that either a URL or file is provided
        /// </summary>
        /// <returns>True if the request is valid, false otherwise</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(SwaggerUrl) || SwaggerFile != null;
        }

        /// <summary>
        /// Gets validation error message if the request is invalid
        /// </summary>
        /// <returns>Error message or null if valid</returns>
        public string? GetValidationError()
        {
            if (!IsValid())
            {
                return "Please provide either a Swagger URL or upload a Swagger file.";
            }

            return null;
        }
    }
}
