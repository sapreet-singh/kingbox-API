using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using KingBox.Api.Configuration;
using KingBox.Api.Services.Interfaces;

namespace KingBox.Api.Services;

/// <summary>
/// Thread-safe implementation of temporary file and isolated workspace directory management.
/// </summary>
public class TemporaryFileService : ITemporaryFileService
{
    private readonly string _tempRootPath;
    private readonly ILogger<TemporaryFileService> _logger;

    public TemporaryFileService(
        IOptions<MediaSettings> settings,
        IWebHostEnvironment environment,
        ILogger<TemporaryFileService> logger)
    {
        _logger = logger;

        var configuredPath = settings.Value.TempDirectory;
        _tempRootPath = Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));

        EnsureTempRootExists();
    }

    private void EnsureTempRootExists()
    {
        try
        {
            if (!Directory.Exists(_tempRootPath))
            {
                Directory.CreateDirectory(_tempRootPath);
                _logger.LogInformation("Initialized temporary root directory at: {RootPath}", _tempRootPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize temporary root directory at: {RootPath}", _tempRootPath);
        }
    }

    public string GetTempRootPath() => _tempRootPath;

    public string CreateJobDirectory(Guid jobId)
    {
        var jobDir = GetJobDirectory(jobId);
        if (!Directory.Exists(jobDir))
        {
            Directory.CreateDirectory(jobDir);
            _logger.LogDebug("Created job workspace directory: {JobDir}", jobDir);
        }
        return jobDir;
    }

    public string GetJobDirectory(Guid jobId)
    {
        var dir = Path.Combine(_tempRootPath, jobId.ToString("N"));
        return Path.GetFullPath(dir);
    }

    public string SanitizeFileName(string rawName, string fallbackExtension)
    {
        var ext = fallbackExtension.Trim().TrimStart('.').ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(rawName))
        {
            return $"media_{Guid.NewGuid():N}.{ext}";
        }

        // Strip path traversal attempts and directory separators
        var fileName = Path.GetFileName(rawName);

        // Replace invalid filesystem characters with underscores
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedChars = fileName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
        var sanitized = new string(sanitizedChars);

        // Remove redundant whitespaces/dots
        sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim('.', ' ');

        // Truncate to reasonable length (e.g. 120 chars)
        if (sanitized.Length > 120)
        {
            sanitized = sanitized[..120].Trim();
        }

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return $"media_{Guid.NewGuid():N}.{ext}";
        }

        // Ensure proper extension
        if (!sanitized.EndsWith($".{ext}", StringComparison.OrdinalIgnoreCase))
        {
            // If it ends with another extension, replace or append
            var currentExt = Path.GetExtension(sanitized);
            if (!string.IsNullOrEmpty(currentExt))
            {
                sanitized = Path.GetFileNameWithoutExtension(sanitized);
            }
            sanitized = $"{sanitized}.{ext}";
        }

        return sanitized;
    }

    public void CleanupJob(Guid jobId)
    {
        try
        {
            var jobDir = GetJobDirectory(jobId);
            if (Directory.Exists(jobDir))
            {
                Directory.Delete(jobDir, recursive: true);
                _logger.LogInformation("Cleaned up job temporary workspace for ID {JobId}", jobId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up job temporary directory for ID {JobId}", jobId);
        }
    }

    public void CleanupAllStaleJobs()
    {
        try
        {
            if (!Directory.Exists(_tempRootPath))
            {
                return;
            }

            var subDirs = Directory.GetDirectories(_tempRootPath);
            if (subDirs.Length > 0)
            {
                _logger.LogInformation("Found {Count} temporary job directories on startup. Purging stale workspaces...", subDirs.Length);
                foreach (var dir in subDirs)
                {
                    try
                    {
                        Directory.Delete(dir, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not delete stale workspace directory: {Dir}", dir);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error occurred during startup stale temporary directory cleanup.");
        }
    }

    public void CleanupOldJobs(TimeSpan maxAge)
    {
        try
        {
            if (!Directory.Exists(_tempRootPath))
            {
                return;
            }

            var cutoff = DateTime.UtcNow - maxAge;
            var subDirs = Directory.GetDirectories(_tempRootPath);

            foreach (var dir in subDirs)
            {
                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    if (dirInfo.CreationTimeUtc < cutoff && dirInfo.LastWriteTimeUtc < cutoff)
                    {
                        dirInfo.Delete(recursive: true);
                        _logger.LogInformation("Cleaned up expired temporary directory: {Directory}", dirInfo.Name);
                    }
                }
                catch (Exception dirEx)
                {
                    _logger.LogDebug(dirEx, "Could not delete old temp directory: {Dir}", dir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during cleanup of old temporary job directories.");
        }
    }
}
