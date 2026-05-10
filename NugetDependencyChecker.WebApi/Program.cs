using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using NugetDependencyChecker.BusinessLogic;
using NugetDependencyChecker.Implementation;
using NugetDependencyChecker.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Register services with dependency injection
builder.Services.AddScoped<IPackageDetailsGetter>(provider =>
    new ProjectAssetsJsonParser(Path.GetTempFileName()));
builder.Services.AddScoped<IDependencyMatrixCreator>(provider =>
    new ExcelDependencyMatrixCreator());
builder.Services.AddScoped<IDependencyDiagramCreator>(provider =>
    new DotDependencyDiagramCreator());
builder.Services.AddScoped<IDependencyAnalysisService, DependencyAnalysisService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "NuGet Dependency Checker API", 
        Version = "v1",
        Description = "API for analyzing NuGet package dependencies from project.assets.json files",
        Contact = new OpenApiContact
        {
            Name = "NuGet Dependency Checker"
        }
    });

    // Map `FileContentResult` to a binary response
    c.MapType<FileContentResult>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });

    // Add response content type for file downloads
    c.OperationFilter<FileResponseOperationFilter>();
    
    // Include XML comments if available
    var xmlFile = Path.Combine(AppContext.BaseDirectory, "NugetDependencyChecker.WebApi.xml");
    if (File.Exists(xmlFile))
    {
        c.IncludeXmlComments(xmlFile);
    }
});

// Configure CORS with restricted origins for production
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", builder =>
    {
        var allowedOrigins = new[] 
        {
            "http://localhost:3000",
            "http://localhost:5000",
            "http://localhost:5173"
        };

        builder.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });

    // Also keep a development policy for local testing
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("AllowAll", builder =>
        {
            builder.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    }
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NuGet Dependency Checker API v1");
});

var corsPolicy = app.Environment.IsDevelopment() ? "AllowAll" : "AllowSpecificOrigins";
app.UseCors(corsPolicy);

app.UseAuthorization();
app.UseAuthentication();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapGet("/", () =>
    Results.Content(
        """
        <!doctype html>
        <html lang=\"en\">
        <head>
            <meta charset=\"utf-8\" />
            <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />
            <title>NuGet Dependency Checker API</title>
            <style>
                body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; margin: 2rem; color: #111827; }
                h1 { margin-bottom: 0.5rem; }
                .ok { color: #16a34a; font-weight: 600; }
                a { color: #2563eb; text-decoration: none; }
                a:hover { text-decoration: underline; }
            </style>
        </head>
        <body>
            <h1>NuGet Dependency Checker API</h1>
            <p class=\"ok\">Deployment is working.</p>
            <p>Open <a href=\"/swagger\">Swagger UI</a> to use the API endpoints.</p>
        </body>
        </html>
        """,
        "text/html"));

app.MapControllers();

app.Run();


public class FileResponseOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Check if the response is a file
        var fileResponse = context.ApiDescription.SupportedResponseTypes
            .Any(r => r.Type == typeof(FileContentResult));

        if (fileResponse)
        {
            operation.Responses["200"].Content["application/octet-stream"] = new OpenApiMediaType
            {
                Schema = new OpenApiSchema
                {
                    Type = "string",
                    Format = "binary"
                }
            };
        }
    }
}
