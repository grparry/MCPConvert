using Microsoft.AspNetCore.Mvc;
using MCPConvert.Models;
using MCPConvert.Services;
using System.Threading.Tasks;

namespace MCPConvert.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConvertController : ControllerBase
    {
        private readonly ISwaggerToMcpConverter _converter;
        private readonly ConversionCacheService _cacheService;
        private readonly UsageTrackingService _usageTrackingService;

        public ConvertController(
            ISwaggerToMcpConverter converter,
            ConversionCacheService cacheService,
            UsageTrackingService usageTrackingService)
        {
            _converter = converter;
            _cacheService = cacheService;
            _usageTrackingService = usageTrackingService;
        }

        [HttpPost("FromUrl")]
        public async Task<ActionResult<ConversionResponse>> ConvertFromUrl(
            [FromForm] string swaggerUrl,
            [FromForm] bool includeSourceMapping = false,
            [FromForm] bool diagnosticMode = false)
        {
            if (string.IsNullOrWhiteSpace(swaggerUrl))
            {
                return BadRequest(new ConversionResponse
                {
                    Success = false,
                    ErrorMessage = "Swagger URL is required"
                });
            }

            // Check usage quota
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!_usageTrackingService.CanPerformConversion(clientIp))
            {
                return BadRequest(new ConversionResponse
                {
                    Success = false,
                    ErrorMessage = "Usage quota exceeded. Please try again later."
                });
            }

            // Check cache
            var cacheKey = $"url:{swaggerUrl}:map:{includeSourceMapping}:diag:{diagnosticMode}";
            var cachedResponse = _cacheService.GetCachedResponse(cacheKey);
            if (cachedResponse != null)
            {
                return Ok(cachedResponse);
            }

            // Perform conversion
            try
            {
                var response = await _converter.ConvertFromUrlAsync(swaggerUrl, includeSourceMapping, diagnosticMode);
                
                // Track usage
                _usageTrackingService.TrackConversion(clientIp);
                
                // Cache response
                _cacheService.CacheResponse(cacheKey, response);
                
                return Ok(response);
            }
            catch (System.Exception ex)
            {
                return BadRequest(new ConversionResponse
                {
                    Success = false,
                    ErrorMessage = $"Error converting Swagger: {ex.Message}"
                });
            }
        }

        [HttpPost("FromFile")]
        public async Task<ActionResult<ConversionResponse>> ConvertFromFile(
            [FromForm] IFormFile swaggerFile,
            [FromForm] bool includeSourceMapping = false,
            [FromForm] bool diagnosticMode = false)
        {
            if (swaggerFile == null || swaggerFile.Length == 0)
            {
                return BadRequest(new ConversionResponse
                {
                    Success = false,
                    ErrorMessage = "Swagger file is required"
                });
            }

            // Check usage quota
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!_usageTrackingService.CanPerformConversion(clientIp))
            {
                return BadRequest(new ConversionResponse
                {
                    Success = false,
                    ErrorMessage = "Usage quota exceeded. Please try again later."
                });
            }

            // Perform conversion
            try
            {
                using var stream = swaggerFile.OpenReadStream();
                var response = await _converter.ConvertFromStreamAsync(stream, includeSourceMapping, diagnosticMode);
                
                // Track usage
                _usageTrackingService.TrackConversion(clientIp);
                
                // Cache response (using file hash as part of key)
                var fileName = swaggerFile.FileName;
                var fileSize = swaggerFile.Length;
                var cacheKey = $"file:{fileName}:{fileSize}:map:{includeSourceMapping}:diag:{diagnosticMode}";
                _cacheService.CacheResponse(cacheKey, response);
                
                return Ok(response);
            }
            catch (System.Exception ex)
            {
                return BadRequest(new ConversionResponse
                {
                    Success = false,
                    ErrorMessage = $"Error converting Swagger: {ex.Message}"
                });
            }
        }

        [HttpGet("Stats")]
        public ActionResult<object> GetStats()
        {
            var cacheStats = _cacheService.GetStatistics();
            var usageStats = _usageTrackingService.GetGlobalStatistics();
            
            return Ok(new
            {
                Cache = cacheStats,
                Usage = usageStats
            });
        }
    }
}
