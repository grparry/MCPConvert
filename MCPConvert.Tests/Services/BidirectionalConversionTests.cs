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

namespace MCPConvert.Tests.Services
{
    /// <summary>
    /// Tests for bidirectional conversion between OpenAPI and MCP formats with emphasis on round-trip fidelity
    /// </summary>
    public class BidirectionalConversionTests
    {
        private readonly Mock<ILogger<OpenApiToMcpConverter>> _converterLoggerMock;
        private readonly Mock<ILogger<SwaggerToMcpConverter>> _loggerMock;
        private readonly SchemaTypeConverter _schemaTypeConverter;
        private readonly OpenApiToMcpConverter _openApiConverter;
        private readonly MockHttpMessageHandler _mockHttp;
        private readonly IHttpClientFactory _httpClientFactory;

        public BidirectionalConversionTests()
        {
            _converterLoggerMock = new Mock<ILogger<OpenApiToMcpConverter>>();
            _loggerMock = new Mock<ILogger<SwaggerToMcpConverter>>();
            _schemaTypeConverter = new SchemaTypeConverter(NullLogger<SchemaTypeConverter>.Instance);
            _openApiConverter = new OpenApiToMcpConverter(_converterLoggerMock.Object, _schemaTypeConverter);
            
            // Setup mock HTTP client for testing URL fetch
            _mockHttp = new MockHttpMessageHandler();
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(() => _mockHttp.ToHttpClient());
            _httpClientFactory = httpClientFactoryMock.Object;
        }

        /// <summary>
        /// Creates an OpenAPI document for testing
        /// </summary>
        private OpenApiDocument CreateTestOpenApiDocument(bool useOpenApi31 = false)
        {
            var document = new OpenApiDocument
            {
                Info = new OpenApiInfo
                {
                    Title = "Test API",
                    Version = "1.0.0",
                    Description = "API for testing bidirectional conversion"
                },
                Paths = new OpenApiPaths(),
                Components = new OpenApiComponents
                {
                    Schemas = new Dictionary<string, OpenApiSchema>()
                }
            };

            // Add path item with GET operation
            document.Paths.Add("/test", new OpenApiPathItem
            {
                Operations = new Dictionary<OperationType, OpenApiOperation>
                {
                    [OperationType.Get] = new OpenApiOperation
                    {
                        OperationId = "getTest",
                        Description = "Get test data",
                        Parameters = new List<OpenApiParameter>
                        {
                            new OpenApiParameter
                            {
                                Name = "id",
                                In = ParameterLocation.Query,
                                Description = "ID of the test data",
                                Required = true,
                                Schema = new OpenApiSchema { Type = "string" }
                            }
                        },
                        Responses = new OpenApiResponses
                        {
                            ["200"] = new OpenApiResponse
                            {
                                Description = "Successful response",
                                Content = new Dictionary<string, OpenApiMediaType>
                                {
                                    ["application/json"] = new OpenApiMediaType
                                    {
                                        Schema = new OpenApiSchema
                                        {
                                            Type = "object",
                                            Properties = new Dictionary<string, OpenApiSchema>
                                            {
                                                ["id"] = new OpenApiSchema { Type = "string" },
                                                ["name"] = new OpenApiSchema { Type = "string" }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            });

            // Add POST operation to the same path
            if (!document.Paths["/test"].Operations.ContainsKey(OperationType.Post))
            {
                document.Paths["/test"].Operations.Add(OperationType.Post, new OpenApiOperation
                {
                    OperationId = "createTest",
                    Description = "Create test data",
                    RequestBody = new OpenApiRequestBody
                    {
                        Required = true,
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Schema = new OpenApiSchema
                                {
                                    Type = "object",
                                    Properties = new Dictionary<string, OpenApiSchema>
                                    {
                                        ["name"] = new OpenApiSchema { Type = "string" },
                                        ["type"] = new OpenApiSchema
                                        {
                                            OneOf = new List<OpenApiSchema>
                                            {
                                                new OpenApiSchema
                                                {
                                                    Type = "string",
                                                    Enum = new List<IOpenApiAny>
                                                    {
                                                        new OpenApiString("type1")
                                                    }
                                                },
                                                new OpenApiSchema
                                                {
                                                    Type = "string",
                                                    Enum = new List<IOpenApiAny>
                                                    {
                                                        new OpenApiString("type2")
                                                    }
                                                }
                                            },
                                            Discriminator = new OpenApiDiscriminator
                                            {
                                                PropertyName = "type",
                                                Mapping = new Dictionary<string, string>
                                                {
                                                    ["type1"] = "#/components/schemas/Type1",
                                                    ["type2"] = "#/components/schemas/Type2"
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    Responses = new OpenApiResponses
                    {
                        ["201"] = new OpenApiResponse
                        {
                            Description = "Created successfully"
                        }
                    }
                });
            }

            // If using OpenAPI 3.1.0, add 3.1-specific features to the document
            if (useOpenApi31)
            {
                // Add a schema with type array (3.1.0 feature)
                document.Components.Schemas.Add("NullableString", new OpenApiSchema
                {
                    Type = null, // In 3.1.0, type can be null when using type array
                    OneOf = new List<OpenApiSchema>
                    {
                        new OpenApiSchema { Type = "string" },
                        new OpenApiSchema { Type = "null" }
                    }
                });
            }

            return document;
        }

        [Fact]
        public async Task RoundTrip_OpenApiToMcpAndBack_PreservesData_OpenApi30()
        {
            // Arrange - Create a test OpenAPI 3.0 document
            var openApiDocument = CreateTestOpenApiDocument(useOpenApi31: false);

            // Configure mock HTTP service for our test
            var urlDetectors = new List<ISwaggerUrlDetector>();
            var converter = new SwaggerToMcpConverter(
                _loggerMock.Object,
                _httpClientFactory,
                urlDetectors,
                _openApiConverter);

            // Step 1: Convert OpenAPI to MCP
            // First serialize the OpenAPI document to JSON
            string openApiJson;
            using (var memoryStream = new MemoryStream())
            {
                using (var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true))
                {
                    var jsonWriter = new OpenApiJsonWriter(streamWriter);
                    openApiDocument.SerializeAsV3(jsonWriter);
                    streamWriter.Flush();
                    memoryStream.Position = 0;
                    using (var reader = new StreamReader(memoryStream, Encoding.UTF8))
                    {
                        openApiJson = reader.ReadToEnd();
                    }
                }
            }

            // Setup mock HTTP response with our OpenAPI JSON
            var testUrl = "https://api.example.com/swagger.json";
            _mockHttp.When(testUrl)
                     .Respond("application/json", openApiJson);

            // Act - Convert OpenAPI to MCP
            var mcpConversionResult = await converter.ConvertFromUrlAsync(testUrl);
            
            // Assert - Verify initial conversion
            Assert.True(mcpConversionResult.Success);
            Assert.NotNull(mcpConversionResult.McpJson);

            // Parse MCP JSON to validate structure
            var mcpContext = JObject.Parse(mcpConversionResult.McpJson);
            Assert.Equal("mcp", mcpContext["schema"]?.ToString());
            Assert.Equal("0.1.0", mcpContext["version"]?.ToString());
            
            // Verify tools were created
            var tools = mcpContext["tools"] as JArray;
            Assert.NotNull(tools);
            Assert.Equal(2, tools.Count);  // We had 2 operations in our test OpenAPI doc
            
            // Verify tool names match operation IDs
            var toolNames = tools.Select(t => t["name"].ToString()).ToList();
            Assert.Contains("getTest", toolNames);
            Assert.Contains("createTest", toolNames);

            // TODO: Add the MCP to OpenAPI conversion here when it's implemented
            // This will complete the bidirectional testing
        }

        [Fact]
        public async Task RoundTrip_OpenApiToMcpAndBack_PreservesData_OpenApi31()
        {
            // Arrange - Create a test OpenAPI 3.1 document with 3.1.0 specific features
            var openApiDocument = CreateTestOpenApiDocument(useOpenApi31: true);

            // Configure mock HTTP service for our test
            var urlDetectors = new List<ISwaggerUrlDetector>();
            var converter = new SwaggerToMcpConverter(
                _loggerMock.Object,
                _httpClientFactory,
                urlDetectors,
                _openApiConverter);

            // Step 1: Convert OpenAPI to MCP
            // First serialize the OpenAPI document to JSON with version patched to 3.1.0
            string openApiJson;
            using (var memoryStream = new MemoryStream())
            {
                using (var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true))
                {
                    var jsonWriter = new OpenApiJsonWriter(streamWriter);
                    openApiDocument.SerializeAsV3(jsonWriter);
                    streamWriter.Flush();
                    memoryStream.Position = 0;
                    using (var reader = new StreamReader(memoryStream, Encoding.UTF8))
                    {
                        var json = reader.ReadToEnd();
                        // Patch the version to 3.1.0
                        var jsonObj = JObject.Parse(json);
                        jsonObj["openapi"] = "3.1.0";
                        openApiJson = jsonObj.ToString();
                    }
                }
            }

            // Setup mock HTTP response with our OpenAPI JSON
            var testUrl = "https://api.example.com/swagger31.json";
            _mockHttp.When(testUrl)
                     .Respond("application/json", openApiJson);

            // Act - Convert OpenAPI to MCP
            var mcpConversionResult = await converter.ConvertFromUrlAsync(testUrl);
            
            // Assert - Verify initial conversion
            Assert.True(mcpConversionResult.Success);
            Assert.NotNull(mcpConversionResult.McpJson);

            // Parse MCP JSON to validate structure
            var mcpContext = JObject.Parse(mcpConversionResult.McpJson);
            Assert.Equal("mcp", mcpContext["schema"]?.ToString());
            Assert.Equal("0.1.0", mcpContext["version"]?.ToString());
            
            // Verify tools were created
            var tools = mcpContext["tools"] as JArray;
            Assert.NotNull(tools);
            Assert.Equal(2, tools.Count);  // We had 2 operations in our test OpenAPI doc
            
            // Verify tool names match operation IDs
            var toolNames = tools.Select(t => t["name"].ToString()).ToList();
            Assert.Contains("getTest", toolNames);
            Assert.Contains("createTest", toolNames);

            // Verify OpenAPI 3.1.0 features were properly converted
            var createTool = tools.FirstOrDefault(t => t["name"].ToString() == "createTest") as JObject;
            var parameters = createTool["parameters"] as JObject;
            var properties = parameters["properties"] as JObject;
            var body = properties["body"] as JObject;
            var bodyProperties = body["properties"] as JObject;
            var typeProperty = bodyProperties["type"] as JObject;
            
            // Check oneOf and discriminator from OpenAPI 3.1.0
            Assert.NotNull(typeProperty["oneOf"]);
            Assert.NotNull(typeProperty["discriminator"]);
            Assert.Equal("type", typeProperty["discriminator"]["propertyName"].ToString());

            // TODO: Add the MCP to OpenAPI conversion here when it's implemented
            // This will complete the bidirectional testing
        }
    }
}
