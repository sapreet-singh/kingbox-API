namespace KingBox.Api.Models;

/// <summary>
/// In-memory representation of an active or recent media conversion job.
/// </summary>
public class ConversionJob
{
    /// <summary>
    /// Unique identifier for the conversion job.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Source media URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Target output format (e.g. mp3, mp4).
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Target output quality (e.g. 128, 320).
    /// </summary>
    public string Quality { get; set; } = string.Empty;

    /// <summary>
    /// Current execution status.
    /// </summary>
    public ConversionStatus Status { get; set; } = ConversionStatus.Pending;

    /// <summary>
    /// Accurate progress percentage (0 to 100) or null if indeterminate.
    /// </summary>
    public double? Progress { get; set; } = 0;

    /// <summary>
    /// Human-readable current stage (e.g. "Waiting", "Downloading", "Converting", "Finalizing", "Completed").
    /// </summary>
    public string Stage { get; set; } = "Waiting";

    /// <summary>
    /// Final sanitized generated file name (without server directory paths).
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Server-side temporary file path (kept internal to backend).
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Server-side isolated temporary directory for this specific job.
    /// </summary>
    public string? TempDirectory { get; set; }

    /// <summary>
    /// User-friendly error message if conversion failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Timestamp when job was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when job completed, failed, or was cancelled.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Cancellation token source for aborting execution.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public CancellationTokenSource? CancellationTokenSource { get; set; }
}
