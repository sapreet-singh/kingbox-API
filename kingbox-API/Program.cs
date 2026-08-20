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

// 3. Register Core Services & Singletons
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
builder.Services.AddSingleton<IToolPathResolver, ToolPathResolver>();
builder.Services.AddSingleton<IToolValidationService, ToolValidationService>();
builder.Services.AddSingleton<ITemporaryFileService, TemporaryFileService>();
builder.Services.AddSingleton<IConversionJobStore, InMemoryConversionJobStore>();
builder.Services.AddSingleton<IConversionQueue, ConversionQueue>();

// 4. Register Media Engines & Application Services
builder.Services.AddScoped<IMediaDownloader, YtDlpMediaDownloader>();
builder.Services.AddScoped<IMediaConverter, FfmpegMediaConverter>();
builder.Services.AddScoped<IMediaService, MediaService>();

// 5. Register Hosted Background Processing Worker
builder.Services.AddHostedService<MediaProcessingWorker>();

// 6. Configure Controllers & JSON serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 7. Configure Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KingBox API",
        Version = "v1",
        Description = "KingBox Media Downloader and Converter Backend API (Phase 2: Media Processing Engine)"
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

// 8. Global Exception Handling Middleware (First in pipeline)
app.UseMiddleware<GlobalExceptionMiddleware>();

// 9. Swagger Documentation UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "KingBox API v1");
        c.RoutePrefix = "swagger";
    });
}

// 10. CORS & Routing Middleware
app.UseCors(corsPolicyName);

app.MapControllers();

app.Run();
