namespace KingBox.Api.DTOs;

/// <summary>
/// Status and version information for external media processing tools (yt-dlp and FFmpeg).
/// </summary>
public class ToolStatusResponse
{
    /// <summary>
    /// yt-dlp tool availability and version.
    /// </summary>
    public ToolInfo YtDlp { get; set; } = new();

    /// <summary>
    /// FFmpeg tool availability and version.
    /// </summary>
    public ToolInfo Ffmpeg { get; set; } = new();
}

/// <summary>
/// Status for a single external tool.
/// </summary>
public class ToolInfo
{
    /// <summary>
    /// Indicates whether the tool is available and executable.
    /// </summary>
    public bool Available { get; set; }

    /// <summary>
    /// Clean version string of the tool (or null if unavailable).
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Optional error details if tool is missing or unexecutable.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
