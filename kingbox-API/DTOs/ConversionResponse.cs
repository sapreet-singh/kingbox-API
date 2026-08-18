namespace KingBox.Api.DTOs;

/// <summary>
/// Response returned upon accepting a conversion request.
/// </summary>
public class ConversionResponse
{
    /// <summary>
    /// Indicates whether request initiation was successful.
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Unique identifier for the conversion job.
    /// </summary>
    public Guid ConversionId { get; set; }

    /// <summary>
    /// Current initial status of the job.
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Informational message regarding the job acceptance.
    /// </summary>
    public string Message { get; set; } = "Conversion request accepted.";
}
