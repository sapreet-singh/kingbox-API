namespace KingBox.Api.Services.Interfaces;

/// <summary>
/// Resolves executable paths for media processing tools across local, Linux, and Docker environments.
/// </summary>
public interface IToolPathResolver
{
    /// <summary>
    /// Resolves the absolute path or command name for yt-dlp.
    /// </summary>
    string ResolveYtDlpPath();

    /// <summary>
    /// Resolves the absolute path or command name for FFmpeg.
    /// </summary>
    string ResolveFfmpegPath();
}
