using System.Collections.Concurrent;
using KingBox.Api.Models;
using KingBox.Api.Services.Interfaces;

namespace KingBox.Api.Services;

/// <summary>
/// Thread-safe in-memory implementation of the conversion job store using ConcurrentDictionary.
/// </summary>
public class InMemoryConversionJobStore : IConversionJobStore
{
    private readonly ConcurrentDictionary<Guid, ConversionJob> _jobs = new();

    public bool TryAdd(ConversionJob job)
    {
        return _jobs.TryAdd(job.Id, job);
    }

    public bool TryGet(Guid id, out ConversionJob? job)
    {
        return _jobs.TryGetValue(id, out job);
    }

    public bool TryUpdate(ConversionJob job)
    {
        if (_jobs.TryGetValue(job.Id, out var existing))
        {
            return _jobs.TryUpdate(job.Id, job, existing);
        }
        return false;
    }

    public bool TryRemove(Guid id, out ConversionJob? job)
    {
        return _jobs.TryRemove(id, out job);
    }

    public IEnumerable<ConversionJob> GetAll()
    {
        return _jobs.Values;
    }

    public int GetActiveJobCount()
    {
        return _jobs.Values.Count(j => j.Status is ConversionStatus.Pending
                                                or ConversionStatus.Downloading
                                                or ConversionStatus.Converting
                                                or ConversionStatus.Finalizing);
    }
}
