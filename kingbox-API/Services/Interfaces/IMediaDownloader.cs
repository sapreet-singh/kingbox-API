using KingBox.Api.DTOs;

namespace KingBox.Api.Services.Interfaces;

/// <summary>
/// Downloader service interfacing with yt-dlp to inspect metadata and extract source streams.
/// </summary>
public interface IMediaDownloader
{
    /// <summary>
    /// Extracts media metadata (title, duration, thumbnail, available formats/qualities) from source URL.
    /// </summary>
    Task<MediaInfoResponse> GetMediaInfoAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads source media into the specified isolated output directory with live progress callbacks.
    /// </summary>
    /// <returns>Absolute path to the downloaded source file.</returns>
    Task<string> DownloadSourceMediaAsync(
        string url,
        string outputDirectory,
        Action<double?>? onProgress = null,
        CancellationToken cancellationToken = default);
}
