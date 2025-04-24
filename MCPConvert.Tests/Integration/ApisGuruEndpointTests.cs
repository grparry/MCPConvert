using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MCPConvert.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace MCPConvert.Tests.Integration
{
    /// <summary>
    /// Tests for converting Swagger/OpenAPI endpoints from the APIs.guru directory
    /// </summary>
    /// <remarks>
    /// TODO: Add support for OpenAPI 3.1.0 specifications. Currently, the parser only supports
    /// OpenAPI 2.0 and 3.0 specifications. Several APIs in the APIs.guru directory use 3.1.0.
    /// </remarks>
    public class ApisGuruEndpointTests
    {
        private readonly ITestOutputHelper _output;
        private readonly IServiceProvider _serviceProvider;
        private readonly HttpClient _httpClient;

        public ApisGuruEndpointTests(ITestOutputHelper output)
        {
            _output = output;
            
            // Setup DI container with all required services
            var services = new ServiceCollection();
            
            // Register HTTP client factory
            services.AddHttpClient();
            
            // Register logging
            services.AddLogging();
            
            // Register URL detectors
            services.AddTransient<MCPConvert.Services.UrlDetection.ISwaggerUrlDetector, MCPConvert.Services.UrlDetection.SwaggerUiUrlDetector>();
            services.AddTransient<MCPConvert.Services.UrlDetection.ISwaggerUrlDetector, MCPConvert.Services.UrlDetection.CommonEndpointsDetector>();
            
            // Register converters
            services.AddTransient<MCPConvert.Services.Conversion.OpenApiToMcpConverter>();
            services.AddTransient<MCPConvert.Services.Utilities.SchemaTypeConverter>();
            
            // Register main converter
            services.AddTransient<MCPConvert.Services.SwaggerToMcpConverter>();
            
            _serviceProvider = services.BuildServiceProvider();
            
            // Create HttpClient for fetching APIs.guru directory
            _httpClient = new HttpClient();
        }
        
        private T GetService<T>() where T : class
        {
            return _serviceProvider.GetRequiredService<T>();
        }
        
        [Fact]
        public async Task CanConvertSampleEndpointsFromApisGuru()
        {
            // Fetch the APIs.guru directory
            var response = await _httpClient.GetAsync("https://api.apis.guru/v2/list.json");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var directory = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
            
            Assert.NotNull(directory);
            Assert.True(directory.Count > 0, "APIs.guru directory should contain entries");
            
            _output.WriteLine($"Found {directory.Count} APIs in the directory");
            
            // Sample APIs to test (limit to 30 for broader coverage)
            var sampleApis = new List<(string Name, string Url)>();
            var counter = 0;
            
            foreach (var api in directory)
            {
                if (counter >= 30) break;
                
                try
                {
                    var versions = api.Value.GetProperty("versions");
                    var versionProps = versions.EnumerateObject().FirstOrDefault();
                    
                    if (versionProps.Value.TryGetProperty("swaggerUrl", out var swaggerUrlElement))
                    {
                        var swaggerUrl = swaggerUrlElement.GetString();
                        if (!string.IsNullOrEmpty(swaggerUrl))
                        {
                            sampleApis.Add((api.Key, swaggerUrl));
                            counter++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Error processing API {api.Key}: {ex.Message}");
                }
            }
            
            _output.WriteLine($"Selected {sampleApis.Count} APIs for testing");
            
            // Test each selected API
            var converter = GetService<SwaggerToMcpConverter>();
            var successCount = 0;
            
            foreach (var (name, url) in sampleApis)
            {
                try
                {
                    _output.WriteLine($"Testing API: {name} - {url}");
                    
                    var result = await converter.ConvertFromUrlAsync(url, true, true);
                    
                    Assert.NotNull(result);
                    Assert.NotNull(result.Diagnostics);
                    
                    var success = result.Diagnostics.ProcessingSteps.Any(step => 
                        step.Contains("Conversion completed successfully"));
                    
                    if (success)
                    {
                        successCount++;
                        _output.WriteLine($"✅ SUCCESS: {name}");
                    }
                    else
                    {
                        _output.WriteLine($"❌ FAILED: {name}");
                    }
                    
                    _output.WriteLine($"Steps: {string.Join(Environment.NewLine, result.Diagnostics.ProcessingSteps)}");
                    _output.WriteLine(new string('-', 80));
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"❌ ERROR testing {name}: {ex.Message}");
                }
            }
            
            _output.WriteLine($"Successfully converted {successCount} out of {sampleApis.Count} APIs");
        }
    }
}
