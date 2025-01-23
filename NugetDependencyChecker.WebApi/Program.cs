using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "API", Version = "v1" });

    // Map `FileContentResult` to a binary response
    c.MapType<FileContentResult>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });

    // Add response content type for file downloads
    c.OperationFilter<FileResponseOperationFilter>();
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseAuthorization();
app.UseAuthentication();


app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");

//app.UseHttpsRedirection();

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
