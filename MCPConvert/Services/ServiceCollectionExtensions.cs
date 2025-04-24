using Microsoft.Extensions.DependencyInjection;
using MCPConvert.Services.UrlDetection;
using MCPConvert.Services.Conversion;
using MCPConvert.Services.Utilities;

namespace MCPConvert.Services
{
    /// <summary>
    /// Extension methods for registering Swagger/OpenAPI conversion services
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Swagger/OpenAPI conversion services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddSwaggerConversionServices(this IServiceCollection services)
        {
            // Register utility services
            services.AddSingleton<SchemaTypeConverter>();
            
            // Register URL detectors
            services.AddScoped<ISwaggerUrlDetector, SwaggerUiUrlDetector>();
            services.AddScoped<ISwaggerUrlDetector, CommonEndpointsDetector>();
            
            // Register conversion services
            services.AddScoped<OpenApiToMcpConverter>();
            services.AddScoped<ISwaggerToMcpConverter, SwaggerToMcpConverter>();
            
            return services;
        }
    }
}
