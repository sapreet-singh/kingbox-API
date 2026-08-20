namespace KingBox.Api.Services.Interfaces;

/// <summary>
/// Thread-safe in-memory queue for scheduling media conversion jobs.
/// </summary>
public interface IConversionQueue
{
    /// <summary>
    /// Enqueues a job ID for background processing.
    /// </summary>
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues the next job ID for processing.
    /// </summary>
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default);
}
