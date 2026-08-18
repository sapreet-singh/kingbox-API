namespace KingBox.Api.DTOs;

/// <summary>
/// Consistent error structure returned on client/server exceptions and validation failures.
/// </summary>
public class ApiErrorResponse
{
    /// <summary>
    /// Always false for error payloads.
    /// </summary>
    public bool Success { get; set; } = false;

    /// <summary>
    /// Friendly error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Error classification code (e.g. VALIDATION_ERROR, NOT_FOUND, INVALID_STATE, INTERNAL_SERVER_ERROR).
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Optional dictionary of field validation error details.
    /// </summary>
    public IDictionary<string, string[]>? Errors { get; set; }
}
