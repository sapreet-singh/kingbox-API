namespace KingBox.Api.Configuration;

/// <summary>
/// Configuration settings for media processing, external tools, and temporary storage.
/// </summary>
public class MediaSettings
{
    public const string SectionName = "MediaSettings";

    /// <summary>
    /// Path or executable name for yt-dlp.
    /// </summary>
    public string YtDlpPath { get; set; } = "yt-dlp";

    /// <summary>
    /// Path or executable name for FFmpeg.
    /// </summary>
    public string FfmpegPath { get; set; } = "ffmpeg";

    /// <summary>
    /// Path to temporary processing directory.
    /// </summary>
    public string TempDirectory { get; set; } = "Storage/Temp";

    /// <summary>
    /// Maximum allowed concurrent active conversions.
    /// </summary>
    public int MaxConcurrentConversions { get; set; } = 2;

    /// <summary>
    /// Maximum allowed file size in bytes (e.g. 1GB = 1073741824).
    /// </summary>
    public long MaxFileSize { get; set; } = 1073741824;

    /// <summary>
    /// Process execution timeout in minutes.
    /// </summary>
    public int ProcessTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Retention time in hours before cleaning up old/abandoned temporary job folders.
    /// </summary>
    public int OldJobCleanupHours { get; set; } = 24;

    /// <summary>
    /// Whitelist of allowed output audio/video formats.
    /// </summary>
    public string[] AllowedFormats { get; set; } = ["mp3", "mp4"];

    /// <summary>
    /// Whitelist of allowed audio/video qualities.
    /// </summary>
    public string[] AllowedQualities { get; set; } = ["128", "192", "256", "320"];

    /// <summary>
    /// JavaScript runtime for yt-dlp to solve n-challenges and signature scripts (e.g. "node").
    /// </summary>
    public string JsRuntime { get; set; } = "node";

    /// <summary>
    /// Additional extractor arguments for yt-dlp (e.g. "youtube:player_client=mweb,android;player_skip=webpage,configs").
    /// </summary>
    public string ExtractorArgs { get; set; } = "youtube:player_client=mweb,android;player_skip=webpage,configs";
}
