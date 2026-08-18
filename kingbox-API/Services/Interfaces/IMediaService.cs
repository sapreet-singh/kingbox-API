using KingBox.Api.DTOs;
using KingBox.Api.Models;

namespace KingBox.Api.Services.Interfaces;

/// <summary>
/// Service abstraction for media information, conversion jobs, cancellation, and download operations.
/// </summary>
public interface IMediaService
{
    /// <summary>
    /// Validates URL and retrieves media metadata (Phase 1 returns ready status; Phase 2 integrates engine).
    /// </summary>
    Task<ApiResponse<MediaInfoResponse?>> GetMediaInfoAsync(MediaInfoRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates parameters and registers a new conversion job.
    /// </summary>
    Task<ConversionResponse> StartConversionAsync(ConversionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves current progress and status of a conversion job.
    /// </summary>
    Task<ConversionProgressResponse?> GetProgressAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an in-progress or pending conversion job.
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
public record DownloadFileInfo(string FilePath, string ContentType, string DownloadFileName);
