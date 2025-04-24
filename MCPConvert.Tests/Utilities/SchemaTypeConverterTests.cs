using Xunit;
using MCPConvert.Services.Utilities;

namespace MCPConvert.Tests.Utilities
{
    public class SchemaTypeConverterTests
    {
        private readonly SchemaTypeConverter _converter;

        public SchemaTypeConverterTests()
        {
            _converter = new SchemaTypeConverter();
        }

        [Theory]
        [InlineData("integer", "integer")]
        [InlineData("number", "number")]
        [InlineData("boolean", "boolean")]
        [InlineData("array", "array")]
        [InlineData("object", "object")]
        [InlineData("string", "string")]
        [InlineData(null, "string")]
        [InlineData("unknown", "string")]
        public void GetJsonSchemaType_ReturnsCorrectType(string openApiType, string expectedJsonType)
        {
            // Act
            var result = _converter.GetJsonSchemaType(openApiType);
            
            // Assert
            Assert.Equal(expectedJsonType, result);
        }
    }
}
