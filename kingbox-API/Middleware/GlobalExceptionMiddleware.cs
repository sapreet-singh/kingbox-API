using System.Net;
using System.Text.Json;
using KingBox.Api.DTOs;
using KingBox.Api.Exceptions;
using KingBox.Api.Services;

namespace KingBox.Api.Middleware;

/// <summary>
/// Global exception handling middleware providing consistent JSON error responses and safe logging.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = HttpStatusCode.InternalServerError;
        var errorCode = "INTERNAL_SERVER_ERROR";
        var message = "An unexpected error occurred.";
        IDictionary<string, string[]>? errors = null;

        switch (exception)
        {
            case ArgumentValidationException validationEx:
                statusCode = HttpStatusCode.BadRequest;
                errorCode = "VALIDATION_ERROR";
                message = validationEx.Message;
                errors = new Dictionary<string, string[]>
                {
                    { validationEx.FieldName, [validationEx.Message] }
                };
                _logger.LogWarning("Validation error: {Message}", validationEx.Message);
                break;

            case ArgumentException argEx:
                statusCode = HttpStatusCode.BadRequest;
                errorCode = "INVALID_ARGUMENT";
                message = argEx.Message;
                _logger.LogWarning("Invalid argument: {Message}", argEx.Message);
                break;

            case KeyNotFoundException notFoundEx:
                statusCode = HttpStatusCode.NotFound;
                errorCode = "NOT_FOUND";
                message = notFoundEx.Message;
                _logger.LogWarning("Resource not found: {Message}", notFoundEx.Message);
                break;

            case FileNotFoundException fileNotFoundEx:
                statusCode = HttpStatusCode.NotFound;
                errorCode = "FILE_NOT_FOUND";
                message = fileNotFoundEx.Message;
                _logger.LogWarning("File not found: {Message}", fileNotFoundEx.Message);
                break;

            case InvalidOperationException invalidOpEx:
                statusCode = HttpStatusCode.Conflict;
                errorCode = "INVALID_STATE";
                message = invalidOpEx.Message;
                _logger.LogWarning("Invalid operation state: {Message}", invalidOpEx.Message);
                break;

            case UnauthorizedAccessException authEx:
                statusCode = HttpStatusCode.Forbidden;
                errorCode = "ACCESS_DENIED";
                message = "Access to the requested resource is prohibited.";
                _logger.LogWarning(authEx, "Unauthorized access attempt.");
                break;

            default:
                _logger.LogError(exception, "Unhandled server exception occurred while processing request.");
                break;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new ApiErrorResponse
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            Errors = errors
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}
