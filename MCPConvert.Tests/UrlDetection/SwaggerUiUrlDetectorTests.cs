using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;
using MCPConvert.Models;
using MCPConvert.Services.UrlDetection;

namespace MCPConvert.Tests.UrlDetection
{
    public class SwaggerUiUrlDetectorTests
    {
        private readonly Mock<ILogger<SwaggerUiUrlDetector>> _loggerMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly SwaggerUiUrlDetector _detector;

        public SwaggerUiUrlDetectorTests()
        {
            _loggerMock = new Mock<ILogger<SwaggerUiUrlDetector>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _detector = new SwaggerUiUrlDetector(_loggerMock.Object, _httpClientFactoryMock.Object);
        }

        [Theory]
        [InlineData("https://petstore.swagger.io/")]
        [InlineData("https://example.com/swagger/")]
        [InlineData("https://example.com/api-docs")]
        [InlineData("https://example.com/swagger-ui")]
        [InlineData("https://example.com/swagger-ui.html")]
        [InlineData("https://example.com/swagger-ui/index.html")]
        [InlineData("https://example.com/swagger/index.html")]
        [InlineData("https://example.com/api-explorer")]
        [InlineData("https://example.com/docs")]
        [InlineData("https://example.com/documentation")]
        public void CanHandle_SwaggerUiUrls_ReturnsTrue(string url)
        {
            // Act
            var result = _detector.CanHandle(url);
            
            // Assert
            Assert.True(result);
        }
        
        [Theory]
        [InlineData("https://example.com/api/v1/users")]
        [InlineData("https://example.com/users")]
        [InlineData("https://example.com/api/products")]
        [InlineData("https://example.com/random-page")]
        public void CanHandle_NonSwaggerUiUrls_ReturnsFalse(string url)
        {
            // Act
            var result = _detector.CanHandle(url);
            
            // Assert
            Assert.False(result);
        }
        
        [Theory]
        [InlineData("https://example.com/swagger.json")]
        [InlineData("https://example.com/openapi.json")]
        [InlineData("https://example.com/swagger/v1/swagger.json")]
        [InlineData("https://example.com/api/v2/swagger.json")]
        public void CanHandle_DirectSwaggerJsonUrls_ReturnsFalse(string url)
        {
            // Act
            var result = _detector.CanHandle(url);
            
            // Assert
            Assert.False(result);
        }
    }
}
