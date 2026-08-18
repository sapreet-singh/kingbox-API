using System.Reflection;
using System.Text.Json.Serialization;
using KingBox.Api.Configuration;
using KingBox.Api.Middleware;
using KingBox.Api.Services;
using KingBox.Api.Services.Interfaces;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Media Settings Options
builder.Services.Configure<MediaSettings>(
    builder.Configuration.GetSection(MediaSettings.SectionName));

// 2. Configure CORS Policy
var corsPolicyName = "KingBoxCorsPolicy";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 3. Register In-Memory State & Services
builder.Services.AddSingleton<IConversionJobStore, InMemoryConversionJobStore>();
builder.Services.AddScoped<IMediaService, MediaService>();

// 4. Configure Controllers & JSON serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 5. Configure Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KingBox API",
        Version = "v1",
        Description = "KingBox Media Downloader and Converter Backend API (Phase 1: Foundation & Architecture)"
    });

    // Include XML comments if generated
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// 6. Global Exception Handling Middleware (First in pipeline)
app.UseMiddleware<GlobalExceptionMiddleware>();

// 7. Swagger Documentation UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "KingBox API v1");
        c.RoutePrefix = "swagger";
    });
}

// 8. CORS & Routing Middleware
app.UseCors(corsPolicyName);

app.MapControllers();

app.Run();
