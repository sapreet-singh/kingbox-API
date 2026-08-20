using KingBox.Api.DTOs;

namespace KingBox.Api.Services.Interfaces;

/// <summary>
/// Service to validate availability and version of external tools (yt-dlp and FFmpeg).
/// </summary>
public interface IToolValidationService
{
    /// <summary>
    /// Checks readiness and versions of all required media processing tools.
    /// </summary>
    Task<ToolStatusResponse> GetToolStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether yt-dlp is installed and available.
    /// </summary>
    Task<bool> IsYtDlpAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether FFmpeg is installed and available.
    /// </summary>
    Task<bool> IsFfmpegAvailableAsync(CancellationToken cancellationToken = default);
}
