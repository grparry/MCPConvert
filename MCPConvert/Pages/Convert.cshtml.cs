using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using MCPConvert.Models;
using MCPConvert.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace MCPConvert.Pages
{
    public class ConvertModel : PageModel
    {
        private readonly ILogger<ConvertModel> _logger;
        private readonly ISwaggerToMcpConverter _converter;
        private readonly ConversionCacheService _cacheService;
        private readonly UsageTrackingService _usageTrackingService;

        [BindProperty]
        public new ConversionRequest Request { get; set; } = new();

        public new ConversionResponse? Response { get; private set; }
        
        public UsageStatistics? UsageStats { get; private set; }
        
        public string? ValidationError { get; private set; }
        
        public bool ShowResults { get; private set; }

        public ConvertModel(
            ILogger<ConvertModel> logger,
            ISwaggerToMcpConverter converter,
            ConversionCacheService cacheService,
            UsageTrackingService usageTrackingService)
        {
            _logger = logger;
            _converter = converter;
            _cacheService = cacheService;
            _usageTrackingService = usageTrackingService;
        }

        public void OnGet()
        {
            UsageStats = _usageTrackingService.GetStatistics();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            UsageStats = _usageTrackingService.GetStatistics();
            
            // Validate the request
            ValidationError = Request.GetValidationError();
            if (!string.IsNullOrEmpty(ValidationError))
            {
                return Page();
            }
            
            // Check if quota is exceeded
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (_usageTrackingService.IsClientQuotaExceeded(clientIp))
            {
                ValidationError = "Your daily quota has been exceeded. Please try again tomorrow.";
                return Page();
            }
            
            if (_usageTrackingService.IsDailyQuotaExceeded())
            {
                ValidationError = "The service's daily quota has been exceeded. Please try again tomorrow.";
                return Page();
            }

            // Process the request
            var startTime = DateTime.UtcNow;
            
            try
            {
                if (!string.IsNullOrEmpty(Request.SwaggerUrl))
                {
                    Response = await _converter.ConvertFromUrlAsync(
                        Request.SwaggerUrl,
                        Request.IncludeSourceMapping,
                        Request.DiagnosticMode);
                }
                else if (Request.SwaggerFile != null)
                {
                    using var stream = Request.SwaggerFile.OpenReadStream();
                    Response = await _converter.ConvertFromStreamAsync(
                        stream,
                        Request.IncludeSourceMapping,
                        Request.DiagnosticMode);
                }
                
                // Cache successful results
                if (Response?.Success == true && !string.IsNullOrEmpty(Response.ContentHash))
                {
                    _cacheService.CacheResult(Response.ContentHash, Response);
                }
                
                // Track usage
                var processingTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                _usageTrackingService.RecordConversion(clientIp, processingTime);
                
                ShowResults = true;
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing conversion request");
                ValidationError = $"An error occurred: {ex.Message}";
                return Page();
            }
        }
        
        public IActionResult OnPostDownload()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            
            // Get the MCP JSON from the form
            var mcpJson = HttpContext.Request.Form["mcpJson"].ToString();
            if (string.IsNullOrEmpty(mcpJson))
            {
                return Page();
            }
            
            // Return as a downloadable file
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(mcpJson);
            return File(bytes, "application/json", "mcp-context.json");
        }
    }
}
