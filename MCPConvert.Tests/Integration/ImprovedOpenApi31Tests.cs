using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.OpenApi.Writers;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RichardSzalay.MockHttp;
using Xunit;
using MCPConvert.Models;
using MCPConvert.Services;
using MCPConvert.Services.Conversion;
using MCPConvert.Services.UrlDetection;
using MCPConvert.Services.Utilities;

namespace MCPConvert.Tests.Integration
{
    /// <summary>
    /// Comprehensive tests for OpenAPI 3.1.0 support in MCPConvert
    /// </summary>
    public class ImprovedOpenApi31Tests
    {
        private readonly Mock<ILogger<OpenApiToMcpConverter>> _converterLoggerMock;
        private readonly Mock<ILogger<SwaggerToMcpConverter>> _loggerMock;
        private readonly SchemaTypeConverter _schemaTypeConverter;
        private readonly OpenApiToMcpConverter _openApiConverter;
        private readonly MockHttpMessageHandler _mockHttp;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _testDataPath;
        private readonly ILoggerFactory _loggerFactory;

        public ImprovedOpenApi31Tests()
        {
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning) 
                    .AddFilter("System", LogLevel.Warning)     
                    .AddFilter("MCPConvert", LogLevel.Trace) 
                    .AddConsole(options =>
                    {
                        options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
                    });
            });
            _converterLoggerMock = new Mock<ILogger<OpenApiToMcpConverter>>();
            _loggerMock = new Mock<ILogger<SwaggerToMcpConverter>>();
            _schemaTypeConverter = new SchemaTypeConverter(_loggerFactory.CreateLogger<SchemaTypeConverter>());
            _openApiConverter = new OpenApiToMcpConverter(_converterLoggerMock.Object, _schemaTypeConverter);
            
            // Setup mock HTTP client for testing URL fetch
            _mockHttp = new MockHttpMessageHandler();
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(() => _mockHttp.ToHttpClient());
            _httpClientFactory = httpClientFactoryMock.Object;
            
            // Get path to test data files
            _testDataPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "TestData");
        }

        [Fact]
        public async Task OpenApi31TestFile_ConvertsToMcp_Successfully()
        {
            // Arrange - Load the test OpenAPI 3.1.0 file
            var filePath = Path.Combine(_testDataPath, "openapi31_features.json");
            Assert.True(File.Exists(filePath), $"Test file not found at {filePath}");
            
            var fileContent = await File.ReadAllTextAsync(filePath);
            
            // Configure mock HTTP response with our test file
            var testUrl = "https://api.example.com/openapi31.json";
            _mockHttp.When(testUrl)
                    .Respond("application/json", fileContent);
            
            // Create the converter with all dependencies
            var urlDetectors = new List<ISwaggerUrlDetector>();
            var converter = new SwaggerToMcpConverter(
                _loggerMock.Object,
                _httpClientFactory,
                urlDetectors,
                _openApiConverter);
            
            // Act - Convert the OpenAPI 3.1.0 document to MCP
            var mcpConversionResult = await converter.ConvertFromUrlAsync(testUrl);
            
            // Print diagnostic information for debugging
            Console.WriteLine($"Test file content (first 100 chars): {fileContent.Substring(0, Math.Min(100, fileContent.Length))}...");
            Console.WriteLine($"Conversion successful: {mcpConversionResult.Success}");
            if (!mcpConversionResult.Success) {
                Console.WriteLine($"Error message: {mcpConversionResult.ErrorMessage}");
                Console.WriteLine($"Diagnostics count: {mcpConversionResult.Diagnostics?.Warnings.Count ?? 0}");
                foreach (var warning in mcpConversionResult.Diagnostics?.Warnings ?? new List<string>()) {
                    Console.WriteLine($"Warning: {warning}");
                }
            }
            
            // TEMP: Print the full MCP JSON output for debugging
            Console.WriteLine("=== FULL MCP JSON OUTPUT ===");
            Console.WriteLine(mcpConversionResult.McpJson);
            Console.WriteLine("=== END MCP JSON OUTPUT ===");

            // Assert - Verify conversion was successful
            Assert.True(mcpConversionResult.Success, $"Conversion failed with error: {mcpConversionResult.ErrorMessage}");
            Assert.NotNull(mcpConversionResult.McpJson);
            
            // Parse and validate MCP structure
            var mcpContext = JObject.Parse(mcpConversionResult.McpJson);
            Assert.Equal("mcp", mcpContext["schema"]?.ToString());
            Assert.Equal("0.1.0", mcpContext["version"]?.ToString());
            
            // Verify tools were created for each operation
            var tools = mcpContext["tools"] as JArray;
            Assert.NotNull(tools);
            Assert.True(tools.Count > 0);

            // Verify schemas were created correctly
            var schemas = mcpContext["schemas"] as JObject;
            Assert.NotNull(schemas);
            
            // Verify the operations from our test file are present
            var operationIds = new[] { "testNullableTypes", "testSchemaComposition", 
                                     "testDiscriminator", "testReferenceExtensions" };
            // Note: In the current version of Microsoft.OpenApi.Readers, BaseUrl isn't supported in the constructor
            // so we'll remove those references from the test code
            
            foreach (var opId in operationIds)
            {
                var tool = tools.FirstOrDefault(t => t["name"]?.ToString() == opId);
                Assert.NotNull(tool);
            }
            
            // Verify OpenAPI 3.1.0 features were properly converted
            
            // 1. Nullable types (OpenAPI 3.1.0 style)
            var nullableTool = tools.FirstOrDefault(t => t["name"]?.ToString() == "testNullableTypes") as JObject;
            var parameters = nullableTool["parameters"] as JObject;
            var properties = parameters["properties"] as JObject;
            var body = properties["body"] as JObject;
            Assert.NotNull(body);
            var bodyProps = body?["properties"] as JObject; // Access properties nested within 'body'
            Assert.NotNull(bodyProps); // Ensure bodyProps itself is not null
            
            // Verify legacy nullable property
            var legacyNullableProp = bodyProps?["legacyNullable"] as JObject; // Corrected: Access via bodyProps
            Assert.NotNull(legacyNullableProp); // Check that the property itself exists
            
            // Logging (can be removed later)
            _loggerMock.Object.LogInformation("TEST LOG: Retrieved legacyNullableProp from properties: {PropJson}", legacyNullableProp.ToString(Newtonsoft.Json.Formatting.None));
            var nullableToken = legacyNullableProp?["nullable"]; 
            _loggerMock.Object.LogInformation("TEST LOG: Type of nullableToken: {TokenType}, Value: {TokenValue}", 
                nullableToken?.GetType()?.FullName ?? "null", 
                nullableToken?.ToString() ?? "null");

            Assert.True((bool?)nullableToken == true, "legacyNullable should have nullable property set to true");

            // Verify other nullable/non-nullable properties using similar robust checks
            Assert.True((bool?)bodyProps?["nullableString"]?["nullable"] == true, "nullableString should be nullable");
            Assert.False((bool?)bodyProps?["nonNullableString"]?["nullable"] == true, "nonNullableString should not be nullable (explicitly false or null)");
            
            // 2. Schema Composition
            var compositionTool = tools.FirstOrDefault(t => t["name"]?.ToString() == "testSchemaComposition") as JObject;
            var compParams = compositionTool["parameters"] as JObject;
            var compProperties = compParams["properties"] as JObject;
            var compBody = compProperties["body"] as JObject;
            var compBodyProps = compBody["properties"] as JObject;
            
            // Verify oneOf, anyOf and allOf were properly converted
            Assert.NotNull(compBodyProps["oneOfField"]["oneOf"]);
            Assert.NotNull(compBodyProps["anyOfField"]["anyOf"]);
            Assert.NotNull(compBodyProps["allOfField"]["allOf"]);
            
            // 3. Discriminator
            var discriminatorTool = tools.FirstOrDefault(t => t["name"]?.ToString() == "testDiscriminator") as JObject;
            var discParams = discriminatorTool["parameters"] as JObject;
            var discProperties = discParams["properties"] as JObject;
            var discBody = discProperties["body"] as JObject;
            
            // Verify discriminator was preserved
            Assert.NotNull(discBody["discriminator"]);
            Assert.Equal("petType", discBody["discriminator"]["propertyName"]?.ToString());
            
            // 4. References with additional properties
            var refTool = tools.FirstOrDefault(t => t["name"]?.ToString() == "testReferenceExtensions") as JObject;
            var refParams = refTool["parameters"] as JObject;
            var refProperties = refParams["properties"] as JObject;
            var refBody = refProperties["body"] as JObject;
            var refBodyProps = refBody["properties"] as JObject;
            var user = refBodyProps["user"] as JObject;
            
            // Verify ref with additional properties was handled correctly
            Assert.Contains("$ref", user.Properties().Select(p => p.Name));
            // Note: OpenAPI 3.1.0 allows $ref alongside other properties, but earlier
            // versions of the spec treated $ref as exclusive. The converter may need
            // special handling for this case.
        }

        [Fact]
        public async Task MCP_Roundtrip_PreservesFeatures()
        {
            // Arrange - Load the MCP test file
            var filePath = Path.Combine(_testDataPath, "mcp_sample.json");
            Assert.True(File.Exists(filePath), $"Test file not found at {filePath}");
            
            var fileContent = await File.ReadAllTextAsync(filePath);
            var originalMcp = JObject.Parse(fileContent);
            
            // This test case can be expanded when MCP to OpenAPI conversion is implemented
            // to test the complete round-trip conversion
            
            // For now, verify the MCP file loads correctly
            Assert.Equal("mcp", originalMcp["schema"]?.ToString());
            Assert.Equal("0.1.0", originalMcp["version"]?.ToString());
            
            var tools = originalMcp["tools"] as JArray;
            Assert.NotNull(tools);
            Assert.True(tools.Count > 0);
            
            // TODO: When MCP to OpenAPI conversion is implemented, add code to:
            // 1. Convert MCP to OpenAPI
            // 2. Convert OpenAPI back to MCP
            // 3. Compare the original MCP to the round-trip MCP
            // 4. Verify OpenAPI 3.1.0 features are preserved in round-trip
        }
    }
}
