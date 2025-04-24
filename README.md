# MCPConvert

MCPConvert is a hosted web service that converts Swagger/OpenAPI documents to MCP JSON format. It provides a simple, low-friction way to generate MCP contexts from existing API documentation.

## Features

- **Unified Conversion Interface**: Upload Swagger files or provide URLs to Swagger documents
- **Source Mapping**: Optional mapping between MCP elements and Swagger source
- **Diagnostic Mode**: Detailed information about the conversion process
- **Caching**: Efficient caching of conversion results for improved performance
- **Usage Tracking**: Monitor API usage and enforce quotas
- **MCP Context Endpoint**: Built-in `.well-known/mcp-context` endpoint using MCPBuckle
- **Resource Efficient**: Optimized for Azure Free Tier deployment

## Usage

### Web UI

1. Visit the conversion page
2. Enter a Swagger URL or upload a Swagger file
3. Optionally enable source mapping and diagnostic mode
4. Click "Convert" to generate the MCP JSON
5. View the results or download the MCP JSON file

### API Integration

MCPConvert can be integrated with other tools and services:

```csharp
// Example C# client code
using var httpClient = new HttpClient();
var content = new MultipartFormDataContent();

// Option 1: Convert from URL
content.Add(new StringContent(swaggerUrl), "SwaggerUrl");

// Option 2: Convert from file
content.Add(new StreamContent(fileStream), "SwaggerFile", "swagger.json");

// Optional flags
content.Add(new StringContent("true"), "IncludeSourceMapping");
content.Add(new StringContent("true"), "DiagnosticMode");

var response = await httpClient.PostAsync("https://mcpconvert.example.com/Convert", content);
var result = await response.Content.ReadAsStringAsync();
```

### Agent/IDE Integration

MCPConvert is designed to be easily integrated with AI agents and IDEs:

1. The `.well-known/mcp-context` endpoint provides the MCP context for the converter itself
2. Source mapping enables traceability between MCP elements and Swagger source
3. Content hashing ensures idempotency for caching and change detection
4. Diagnostic mode provides detailed information for troubleshooting

## CLI Equivalence

For command-line usage, you can use curl:

```bash
# Convert from URL
curl -X POST https://mcpconvert-h3bwarf4e3e7aabv.westus2-01.azurewebsites.net/Convert \
  -F "SwaggerUrl=https://petstore.swagger.io/v2/swagger.json" \
  -F "IncludeSourceMapping=true" \
  -F "DiagnosticMode=true"

# Convert from file
curl -X POST https://mcpconvert-h3bwarf4e3e7aabv.westus2-01.azurewebsites.net/Convert \
  -F "SwaggerFile=@/path/to/swagger.json" \
  -F "IncludeSourceMapping=true" \
  -F "DiagnosticMode=true"
```

## Development

### Prerequisites

- .NET 9.0 SDK
- Visual Studio 2022 or Visual Studio Code

### Building and Running

```bash
# Clone the repository
git clone https://github.com/grparry/MCPConvert.git
cd MCPConvert

# Build the project
dotnet build

# Run the application
dotnet run
```

### Dependencies

- MCPBuckle: MCP context generation for ASP.NET Core
- Microsoft.OpenApi: OpenAPI document parsing and manipulation
- Newtonsoft.Json: JSON serialization and manipulation

## License

This project is open source and available under the MIT License.
