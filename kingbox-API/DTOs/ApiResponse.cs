namespace KingBox.Api.DTOs;

/// <summary>
/// Generic standardized API response envelope.
/// </summary>
/// <typeparam name="T">Payload data type.</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Indicates whether operation was successful.
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Response or status message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Response payload data.
    /// </summary>
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T? data, string message = "Success")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }
}
