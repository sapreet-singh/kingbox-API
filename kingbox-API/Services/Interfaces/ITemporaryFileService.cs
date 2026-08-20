namespace KingBox.Api.Services.Interfaces;

/// <summary>
/// Manages isolated job temporary directories, filename sanitization, and disk cleanup.
/// </summary>
public interface ITemporaryFileService
{
    /// <summary>
    /// Creates and returns the absolute directory path for a conversion job.
    /// </summary>
    string CreateJobDirectory(Guid jobId);

    /// <summary>
    /// Gets the absolute directory path for a conversion job.
    /// </summary>
    string GetJobDirectory(Guid jobId);

    /// <summary>
    /// Sanitizes an external or untrusted filename to prevent path traversal and illegal filesystem characters.
    /// </summary>
    string SanitizeFileName(string rawName, string fallbackExtension);

    /// <summary>
    /// Safely deletes the temporary directory and all files associated with a job.
    /// </summary>
    void CleanupJob(Guid jobId);

    /// <summary>
    /// Cleans up any abandoned temporary directories older than the specified age.
    /// </summary>
    void CleanupOldJobs(TimeSpan maxAge);

    /// <summary>
    /// Cleans up all orphaned/stale temporary job directories on application startup.
    /// </summary>
    void CleanupAllStaleJobs();

    /// <summary>
    /// Gets the base temporary storage root directory path.
    /// </summary>
    string GetTempRootPath();
}
