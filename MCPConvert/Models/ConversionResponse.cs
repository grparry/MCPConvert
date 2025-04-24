using System;
using System.Collections.Generic;

namespace MCPConvert.Models
{
    /// <summary>
    /// Represents the result of a Swagger to MCP conversion
    /// </summary>
    public class ConversionResponse
    {
        /// <summary>
        /// Whether the conversion was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the conversion failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// The generated MCP JSON
        /// </summary>
        public string? McpJson { get; set; }

        /// <summary>
        /// Content hash for caching and idempotency
        /// </summary>
        public string? ContentHash { get; set; }

        /// <summary>
        /// Source mapping information linking MCP elements to Swagger source
        /// </summary>
        public Dictionary<string, SourceMapEntry>? SourceMap { get; set; }

        /// <summary>
        /// Diagnostic information (only populated in diagnostic mode)
        /// </summary>
        public ConversionDiagnostics? Diagnostics { get; set; }

        /// <summary>
        /// Timestamp when the conversion was performed
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;


    }

    /// <summary>
    /// Represents a source mapping entry linking MCP elements to Swagger source
    /// </summary>
    public class SourceMapEntry
    {
        /// <summary>
        /// Path to the element in the Swagger document
        /// </summary>
        public string? SwaggerPath { get; set; }

        /// <summary>
        /// Line number in the source document (if available)
        /// </summary>
        public int? LineNumber { get; set; }
    }

    /// <summary>
    /// Contains diagnostic information about the conversion process
    /// </summary>
    public class ConversionDiagnostics
    {
        /// <summary>
        /// Parsing warnings encountered during conversion
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Detailed processing steps for debugging
        /// </summary>
        public List<string> ProcessingSteps { get; set; } = new List<string>();

        /// <summary>
        /// Performance metrics for the conversion
        /// </summary>
        public Dictionary<string, double> PerformanceMetrics { get; set; } = new Dictionary<string, double>();
    }
}
