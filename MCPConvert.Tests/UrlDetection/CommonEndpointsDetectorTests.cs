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
    public class CommonEndpointsDetectorTests
    {
        private readonly Mock<ILogger<CommonEndpointsDetector>> _loggerMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly CommonEndpointsDetector _detector;

        public CommonEndpointsDetectorTests()
        {
            _loggerMock = new Mock<ILogger<CommonEndpointsDetector>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _detector = new CommonEndpointsDetector(_loggerMock.Object, _httpClientFactoryMock.Object);
        }

        [Theory]
        [InlineData("https://example.com/swagger.json")]
        [InlineData("https://example.com/openapi.json")]
        [InlineData("https://example.com/swagger/v1/swagger.json")]
        [InlineData("https://example.com/api/v2/swagger.json")]
        [InlineData("https://example.com/api-docs")]
        [InlineData("https://example.com/v1/api-docs")]
        [InlineData("https://example.com/api-docs/v2")]
        [InlineData("https://example.com/docs/swagger.json")]
        [InlineData("https://example.com/swagger-ui/")]
        public void CanHandle_SwaggerRelatedUrls_ReturnsTrue(string url)
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
        public void CanHandle_NonSwaggerUrls_ReturnsFalse(string url)
        {
            // Act
            var result = _detector.CanHandle(url);
            
            // Assert
            Assert.False(result);
        }
        
        [Theory]
        [InlineData("https://example.com/swagger/v1/swagger.json", "v1")]
        [InlineData("https://example.com/api/v2/openapi.json", "v2")]
        [InlineData("https://example.com/v3/api-docs", "v3")]
        [InlineData("https://example.com/api-docs/v4", "v4")]
        [InlineData("https://example.com/api/products", "v1")] // Default when no version found
        public void ExtractApiVersion_ReturnsCorrectVersion(string url, string expectedVersion)
        {
            // Act
            var result = SwaggerUrlPatterns.ExtractApiVersion(url);
            
            // Assert
            Assert.Equal(expectedVersion, result);
        }
    }
}
