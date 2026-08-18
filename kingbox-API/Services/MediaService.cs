using Microsoft.Extensions.Options;
using KingBox.Api.Configuration;
using KingBox.Api.DTOs;
using KingBox.Api.Models;
using KingBox.Api.Services.Interfaces;

namespace KingBox.Api.Services;

/// <summary>
/// Core media service managing validation, conversion lifecycle, and file download retrieval.
/// </summary>
public class MediaService : IMediaService
{
    private readonly IConversionJobStore _jobStore;
    private readonly MediaSettings _settings;
    private readonly ILogger<MediaService> _logger;
    private readonly string _tempDirectoryPath;

    public MediaService(
        IConversionJobStore jobStore,
        IOptions<MediaSettings> settings,
        IWebHostEnvironment environment,
        ILogger<MediaService> logger)
    {
        _jobStore = jobStore;
        _settings = settings.Value;
        _logger = logger;

        // Resolve absolute temp directory path safely
        _tempDirectoryPath = Path.IsPathRooted(_settings.TempDirectory)
            ? _settings.TempDirectory
            : Path.Combine(environment.ContentRootPath, _settings.TempDirectory);

        EnsureTempDirectoryExists();
    }

    private void EnsureTempDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(_tempDirectoryPath))
            {
                Directory.CreateDirectory(_tempDirectoryPath);
                _logger.LogInformation("Created temporary media storage directory: {TempDirectory}", _tempDirectoryPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize temporary storage directory at {TempDirectory}", _tempDirectoryPath);
        }
    }

    public Task<ApiResponse<MediaInfoResponse?>> GetMediaInfoAsync(MediaInfoRequest request, CancellationToken cancellationToken = default)
    {
        ValidateUrl(request.Url);

        _logger.LogInformation("Received media info request for URL: {Url}", request.Url);

        // Phase 1: Return placeholder ready response without invoking external downloaders
        var response = new ApiResponse<MediaInfoResponse?>
        {
            Success = true,
            Message = "Media information service is ready.",
            Data = null
        };

        return Task.FromResult(response);
    }

    public Task<ConversionResponse> StartConversionAsync(ConversionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateUrl(request.Url);
        ValidateFormatAndQuality(request.Format, request.Quality);

        var activeCount = _jobStore.GetActiveJobCount();
        if (activeCount >= _settings.MaxConcurrentConversions)
        {
            _logger.LogWarning("Concurrent conversion limit ({Limit}) reached. Current active: {Active}", _settings.MaxConcurrentConversions, activeCount);
            throw new InvalidOperationException($"Maximum concurrent conversions limit of {_settings.MaxConcurrentConversions} reached. Please try again shortly.");
        }

        var job = new ConversionJob
        {
            Id = Guid.NewGuid(),
            Url = request.Url.Trim(),
            Format = request.Format.Trim().ToLowerInvariant(),
            Quality = request.Quality.Trim().ToLowerInvariant(),
            Status = ConversionStatus.Pending,
            Progress = 0,
            Stage = "Waiting",
            CreatedAt = DateTime.UtcNow,
            CancellationTokenSource = new CancellationTokenSource()
        };

        if (!_jobStore.TryAdd(job))
        {
            _logger.LogError("Failed to add conversion job to in-memory store for ID {JobId}", job.Id);
            throw new InvalidOperationException("Failed to register conversion job.");
        }

        _logger.LogInformation("Accepted conversion request for ID {JobId}, Format: {Format}, Quality: {Quality}", job.Id, job.Format, job.Quality);

        var response = new ConversionResponse
        {
            Success = true,
            ConversionId = job.Id,
            Status = job.Status.ToString(),
            Message = "Conversion request accepted."
        };

        return Task.FromResult(response);
    }

    public Task<ConversionProgressResponse?> GetProgressAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_jobStore.TryGet(id, out var job) || job is null)
        {
            _logger.LogWarning("Conversion job with ID {JobId} not found.", id);
            return Task.FromResult<ConversionProgressResponse?>(null);
        }

        var progress = new ConversionProgressResponse
        {
            Id = job.Id,
            Status = job.Status.ToString(),
            Progress = job.Progress,
            Stage = job.Stage,
            FileName = job.FileName,
            ErrorMessage = job.ErrorMessage
        };

        return Task.FromResult<ConversionProgressResponse?>(progress);
    }

    public Task<CancelResponse?> CancelConversionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_jobStore.TryGet(id, out var job) || job is null)
        {
            _logger.LogWarning("Cannot cancel: conversion job with ID {JobId} not found.", id);
            return Task.FromResult<CancelResponse?>(null);
        }

        if (job.Status is ConversionStatus.Completed)
        {
            throw new InvalidOperationException("Cannot cancel a completed conversion job.");
        }

        if (job.Status is ConversionStatus.Cancelled)
        {
            return Task.FromResult<CancelResponse?>(new CancelResponse
            {
                Success = true,
                ConversionId = job.Id,
                Status = ConversionStatus.Cancelled.ToString(),
                Message = "Conversion was already cancelled."
            });
        }

        job.Status = ConversionStatus.Cancelled;
        job.Stage = "Cancelled";
        job.CompletedAt = DateTime.UtcNow;

        try
        {
            job.CancellationTokenSource?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Ignore disposed token source
        }

        _jobStore.TryUpdate(job);
        _logger.LogInformation("Conversion job {JobId} was successfully cancelled.", job.Id);

        var response = new CancelResponse
        {
            Success = true,
            ConversionId = job.Id,
            Status = ConversionStatus.Cancelled.ToString(),
            Message = "Conversion cancelled successfully."
        };

        return Task.FromResult<CancelResponse?>(response);
    }

    public Task<DownloadFileInfo?> GetDownloadFileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_jobStore.TryGet(id, out var job) || job is null)
        {
            _logger.LogWarning("Cannot download: conversion job with ID {JobId} not found.", id);
            return Task.FromResult<DownloadFileInfo?>(null);
        }

        if (job.Status != ConversionStatus.Completed)
        {
            _logger.LogWarning("Download requested for non-completed job {JobId}. Current status: {Status}", id, job.Status);
            throw new InvalidOperationException($"Conversion job {id} is not completed. Current status: {job.Status}.");
        }

        if (string.IsNullOrWhiteSpace(job.FilePath) || !File.Exists(job.FilePath))
        {
            _logger.LogWarning("Download requested for job {JobId}, but output file is missing on disk.", id);
            throw new FileNotFoundException("The processed media file was not found on temporary storage.");
        }

        // Security check: Prevent path traversal by verifying file is strictly within TempDirectory
        var fullPath = Path.GetFullPath(job.FilePath);
        var allowedRoot = Path.GetFullPath(_tempDirectoryPath);

        if (!fullPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("Security violation: Attempted access outside temporary directory for job {JobId}.", id);
            throw new UnauthorizedAccessException("Access to the specified file path is prohibited.");
        }

        var contentType = GetContentType(job.Format);
        var downloadName = !string.IsNullOrWhiteSpace(job.FileName)
            ? job.FileName
            : $"{job.Id}.{job.Format}";

        return Task.FromResult<DownloadFileInfo?>(new DownloadFileInfo(fullPath, contentType, downloadName));
    }

    private void ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw ArgumentValidationException.ForField("Url", "URL is required and cannot be empty.");
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw ArgumentValidationException.ForField("Url", "URL must be a valid absolute HTTP or HTTPS URL.");
        }
    }

    private void ValidateFormatAndQuality(string format, string quality)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            throw ArgumentValidationException.ForField("Format", "Format is required.");
        }

        var normalizedFormat = format.Trim().ToLowerInvariant();
        if (!_settings.AllowedFormats.Contains(normalizedFormat, StringComparer.OrdinalIgnoreCase))
        {
            throw ArgumentValidationException.ForField("Format", $"Format '{format}' is not supported. Allowed formats: {string.Join(", ", _settings.AllowedFormats)}.");
        }

        if (string.IsNullOrWhiteSpace(quality))
        {
            throw ArgumentValidationException.ForField("Quality", "Quality is required.");
        }

        var normalizedQuality = quality.Trim().ToLowerInvariant();
        if (!_settings.AllowedQualities.Contains(normalizedQuality, StringComparer.OrdinalIgnoreCase))
        {
            throw ArgumentValidationException.ForField("Quality", $"Quality '{quality}' is not supported. Allowed qualities: {string.Join(", ", _settings.AllowedQualities)}.");
        }
    }

    private static string GetContentType(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "mp3" => "audio/mpeg",
            "mp4" => "video/mp4",
            "m4a" => "audio/mp4",
            "wav" => "audio/wav",
            "webm" => "video/webm",
            "ogg" => "audio/ogg",
            "flac" => "audio/flac",
            _ => "application/octet-stream"
        };
    }
}

/// <summary>
/// Custom domain validation exception mapped to 400 Bad Request by middleware.
/// </summary>
public class ArgumentValidationException : Exception
{
    public string FieldName { get; }

    public ArgumentValidationException(string fieldName, string message) : base(message)
    {
        FieldName = fieldName;
    }

    public static ArgumentValidationException ForField(string fieldName, string message) => new(fieldName, message);
}
