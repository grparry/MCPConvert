using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Newtonsoft.Json.Linq;
using MCPConvert.Models;
using MCPConvert.Services;
using MCPConvert.Services.Conversion;
using MCPConvert.Services.UrlDetection;
using MCPConvert.Services.Utilities;

namespace MCPConvert.Tests.Services
{
    public class SwaggerToMcpConverterTests
    {
        [Fact]
        public void Constructor_InitializesCorrectly()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<SwaggerToMcpConverter>>();
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            var detectors = new List<ISwaggerUrlDetector> { 
                new Mock<ISwaggerUrlDetector>().Object,
                new Mock<ISwaggerUrlDetector>().Object 
            };
            var converterLoggerMock = new Mock<ILogger<OpenApiToMcpConverter>>();
            var schemaTypeConverter = new SchemaTypeConverter(NullLogger<SchemaTypeConverter>.Instance);
            var openApiConverter = new OpenApiToMcpConverter(converterLoggerMock.Object, schemaTypeConverter);
            
            // Act - Create the converter with the dependencies
            var converter = new SwaggerToMcpConverter(
                loggerMock.Object,
                httpClientFactoryMock.Object,
                detectors,
                openApiConverter);
            
            // Assert
            Assert.NotNull(converter);
        }


    }
}
