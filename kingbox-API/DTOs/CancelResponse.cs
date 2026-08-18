namespace KingBox.Api.DTOs;

/// <summary>
/// Response payload for job cancellation requests.
/// </summary>
public class CancelResponse
{
    /// <summary>
    /// Indicates whether cancellation was accepted.
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Job identifier.
    /// </summary>
    public Guid ConversionId { get; set; }

    /// <summary>
    /// Updated status (Cancelled).
    /// </summary>
    public string Status { get; set; } = "Cancelled";

    /// <summary>
    /// Informational message.
    /// </summary>
    public string Message { get; set; } = "Conversion cancelled successfully.";
}
