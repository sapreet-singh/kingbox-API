namespace KingBox.Api.DTOs;

/// <summary>
/// Status and progress updates for an ongoing or completed conversion job.
/// </summary>
public class ConversionProgressResponse
{
    /// <summary>
    /// Job identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Current job status (Pending, Downloading, Converting, Finalizing, Completed, Failed, Cancelled).
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Accurate progress percentage (0 to 100) or null if indeterminate.
    /// </summary>
    public double? Progress { get; set; }

    /// <summary>
    /// Current execution stage description.
    /// </summary>
    public string Stage { get; set; } = "Waiting";

    /// <summary>
    /// Output file name if completed, otherwise null.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Failure error message if failed, otherwise null.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
