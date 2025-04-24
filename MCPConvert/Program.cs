using MCPBuckle.Extensions;
using MCPConvert.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

// Register MCPConvert services
builder.Services.AddSingleton<ISwaggerToMcpConverter, SwaggerToMcpConverter>();
builder.Services.AddSingleton<ConversionCacheService>();
builder.Services.AddSingleton<UsageTrackingService>();

// Configure MCPBuckle
builder.Services.AddMcpBuckle(options =>
{
    options.RoutePrefix = "/.well-known/mcp-context";
    options.Metadata.Add("title", "MCPConvert");
    options.Metadata.Add("description", "Convert Swagger/OpenAPI documents to MCP JSON");
    options.Metadata.Add("version", "1.0.0");
    options.Metadata.Add("contact", new Dictionary<string, string>
    {
        { "name", "MCPConvert Team" },
        { "url", "https://github.com/grparry/MCPConvert" }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

// Use MCPBuckle middleware
app.UseMcpBuckle();

app.UseStaticFiles();
app.MapRazorPages();

app.Run();
