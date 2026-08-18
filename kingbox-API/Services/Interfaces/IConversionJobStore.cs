using KingBox.Api.Models;

namespace KingBox.Api.Services.Interfaces;

/// <summary>
/// Thread-safe in-memory store for active and completed media conversion jobs.
/// </summary>
public interface IConversionJobStore
{
    /// <summary>
    /// Adds a new conversion job to the store.
    /// </summary>
    bool TryAdd(ConversionJob job);

    /// <summary>
    /// Retrieves a conversion job by its unique identifier.
    /// </summary>
    bool TryGet(Guid id, out ConversionJob? job);

    /// <summary>
    /// Updates an existing conversion job in the store.
    /// </summary>
    bool TryUpdate(ConversionJob job);

    /// <summary>
    /// Removes a conversion job from the store.
    /// </summary>
    bool TryRemove(Guid id, out ConversionJob? job);

    /// <summary>
    /// Gets all current conversion jobs in memory.
    /// </summary>
    IEnumerable<ConversionJob> GetAll();

    /// <summary>
    /// Gets the count of currently running/active jobs.
    /// </summary>
    int GetActiveJobCount();
}
