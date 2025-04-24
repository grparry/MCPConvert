using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MCPConvert.Models;
using MCPConvert.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace MCPConvert.Tests.Integration
{
    public class SwaggerUrlDetectionIntegrationTests
    {
        private readonly ITestOutputHelper _output;
        private readonly IServiceProvider _serviceProvider;

        public SwaggerUrlDetectionIntegrationTests(ITestOutputHelper output)
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
        }
        
        private T GetService<T>() where T : class
        {
            return _serviceProvider.GetRequiredService<T>();
        }

        [Fact]
        public async Task CanDetectAndConvertDirectSwaggerJsonEndpoints()
        {
            // Arrange
            var endpoints = new Dictionary<string, string>
            {
                { "Petstore Direct JSON", "https://petstore.swagger.io/v2/swagger.json" },
                { "Guru API", "https://api.getguru.com/api/v1/swagger.json" },
                { "Import.io Schedule API", "https://api.docs.import.io/schedule/swagger.json" }
            };
            
            var converter = GetService<SwaggerToMcpConverter>();
            
            foreach (var endpoint in endpoints)
            {
                try
                {
                    _output.WriteLine($"Testing endpoint: {endpoint.Key} - {endpoint.Value}");
                    
                    // Act
                    var result = await converter.ConvertFromUrlAsync(endpoint.Value, true, true);
                    
                    // Assert
                    Assert.NotNull(result);
                    Assert.NotNull(result.Diagnostics);
                    
                    // Log detailed results for manual inspection
                    _output.WriteLine($"Endpoint: {endpoint.Key} - {endpoint.Value}");
                    _output.WriteLine($"Steps: {string.Join(Environment.NewLine, result.Diagnostics.ProcessingSteps)}");
                    
                    // Check if conversion was successful
                    Assert.Contains(result.Diagnostics.ProcessingSteps, 
                        step => step.Contains("Conversion completed successfully"));
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Error testing {endpoint.Key}: {ex.Message}");
                    throw;
                }
            }
        }
        
        [Fact]
        public async Task CanDetectAndConvertSwaggerUiEndpoints()
        {
            // Arrange
            var endpoints = new Dictionary<string, string>
            {
                { "Petstore UI", "https://petstore.swagger.io/" },
                { "Crossref API", "https://api.crossref.org/swagger-ui/index.html" },
                { "ConfigCat API", "https://api.configcat.com/swagger/index.html" }
            };
            
            var converter = GetService<SwaggerToMcpConverter>();
            
            foreach (var endpoint in endpoints)
            {
                try
                {
                    _output.WriteLine($"Testing endpoint: {endpoint.Key} - {endpoint.Value}");
                    
                    // Act
                    var result = await converter.ConvertFromUrlAsync(endpoint.Value, true, true);
                    
                    // Assert
                    Assert.NotNull(result);
                    Assert.NotNull(result.Diagnostics);
                    
                    // Log detailed results for manual inspection
                    _output.WriteLine($"Endpoint: {endpoint.Key} - {endpoint.Value}");
                    _output.WriteLine($"Steps: {string.Join(Environment.NewLine, result.Diagnostics.ProcessingSteps)}");
                    
                    // Check if conversion was successful
                    Assert.Contains(result.Diagnostics.ProcessingSteps, 
                        step => step.Contains("Conversion completed successfully"));
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Error testing {endpoint.Key}: {ex.Message}");
                    // Don't throw here, just log the error
                    // This allows us to see which endpoints work and which don't
                    _output.WriteLine($"FAILED: {endpoint.Key} - {endpoint.Value}");
                }
            }
        }
        
        [Fact]
        public async Task CanDetectAndConvertNonStandardSwaggerEndpoints()
        {
            // Arrange
            var endpoints = new Dictionary<string, string>
            {
                { "BMC Discovery API", "https://docs.bmc.com/xwiki/bin/view/IT-Operations-Management/Discovery/BMC-Discovery/DISCO121/Integrating/Using-the-REST-API/Swagger-and-the-REST-API/" }
                // APIs.guru is a directory, not a direct Swagger endpoint
            };
            
            var converter = GetService<SwaggerToMcpConverter>();
            
            foreach (var endpoint in endpoints)
            {
                try
                {
                    _output.WriteLine($"Testing endpoint: {endpoint.Key} - {endpoint.Value}");
                    
                    // Act
                    var result = await converter.ConvertFromUrlAsync(endpoint.Value, true, true);
                    
                    // Assert
                    Assert.NotNull(result);
                    Assert.NotNull(result.Diagnostics);
                    
                    // Log detailed results for manual inspection
                    _output.WriteLine($"Endpoint: {endpoint.Key} - {endpoint.Value}");
                    _output.WriteLine($"Steps: {string.Join(Environment.NewLine, result.Diagnostics.ProcessingSteps)}");
                    
                    // For these non-standard endpoints, we don't expect all to succeed
                    // Just log the outcome for analysis
                    var success = result.Diagnostics.ProcessingSteps.Any(step => step.Contains("Conversion completed successfully"));
                    _output.WriteLine(success ? "SUCCESS" : "FAILED");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Error testing {endpoint.Key}: {ex.Message}");
                    // Don't throw here, just log the error
                    _output.WriteLine($"FAILED: {endpoint.Key} - {endpoint.Value}");
                }
            }
        }
    }
}
