using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCPConvert.Services;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace MCPConvert.Pages
{
    public class ConvertMcpToSwaggerModel : PageModel
    {
        private readonly ILogger<ConvertMcpToSwaggerModel> _logger;
        private readonly IMcpToSwaggerConverter _converter;

        public ConvertMcpToSwaggerModel(ILogger<ConvertMcpToSwaggerModel> logger, IMcpToSwaggerConverter converter)
        {
            _logger = logger;
            _converter = converter;
        }

        [BindProperty]
        [Required]
        [Display(Name = "MCP JSON Input")]
        public string McpInput { get; set; }

        [BindProperty]
        public IFormFile McpFile { get; set; }

        public string SwaggerOutput { get; set; }
        public string ErrorMessage { get; set; }

        public void OnGet()
        {
            // Initial page load
        }

        public async Task<IActionResult> OnPostAsync()
        {
            string mcpJson = McpInput;

            // If a file is uploaded, use its contents
            if (McpFile != null && McpFile.Length > 0)
            {
                if (!McpFile.ContentType.Contains("json") && !McpFile.FileName.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage = "Uploaded file must be a JSON file.";
                    return Page();
                }
                using (var reader = new System.IO.StreamReader(McpFile.OpenReadStream()))
                {
                    mcpJson = await reader.ReadToEndAsync();
                }
            }

            if (string.IsNullOrWhiteSpace(mcpJson))
            {
                ErrorMessage = "Please provide MCP JSON input (paste or file).";
                return Page();
            }

            try
            {
                SwaggerOutput = await _converter.ConvertMcpToSwaggerAsync(mcpJson);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error converting MCP to Swagger");
                ErrorMessage = $"Conversion failed: {ex.Message}";
            }
            return Page();
        }
    }
}
