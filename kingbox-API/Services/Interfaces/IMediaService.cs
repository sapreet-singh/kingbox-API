using KingBox.Api.DTOs;

namespace KingBox.Api.Services.Interfaces;

/// <summary>
/// Service abstraction for media information, conversion jobs, cancellation, download operations, and tool checks.
/// </summary>
public interface IMediaService
{
    /// <summary>
    /// Checks tool availability and versions for yt-dlp and FFmpeg.
    /// </summary>
    Task<ToolStatusResponse> GetToolStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates URL and retrieves real media metadata via yt-dlp.
    /// </summary>
    Task<ApiResponse<MediaInfoResponse?>> GetMediaInfoAsync(MediaInfoRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates parameters, registers a new conversion job, and enqueues it for background processing.
    /// </summary>
    Task<ConversionResponse> StartConversionAsync(ConversionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves current progress and status of a conversion job.
    /// </summary>
    Task<ConversionProgressResponse?> GetProgressAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an in-progress or pending conversion job, terminating associated process trees.
    /// </summary>
    Task<CancelResponse?> CancelConversionAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves completed file download information for a conversion job, validating security and existence.
    /// </summary>
    Task<DownloadFileInfo?> GetDownloadFileAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Output file metadata for client download stream.
/// </summary>
public record DownloadFileInfo(string FilePath, string ContentType, string DownloadFileName, Guid JobId);
