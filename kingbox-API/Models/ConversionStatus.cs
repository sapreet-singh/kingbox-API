namespace KingBox.Api.Models;

/// <summary>
/// Lifecycle status of a media conversion job.
/// </summary>
public enum ConversionStatus
{
    Pending,
    Downloading,
    Converting,
    Finalizing,
    Completed,
    Failed,
    Cancelled
}
