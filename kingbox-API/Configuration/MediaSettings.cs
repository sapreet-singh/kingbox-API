namespace KingBox.Api.Configuration;

/// <summary>
/// Configuration settings for media processing and temporary storage.
/// </summary>
public class MediaSettings
{
    public const string SectionName = "MediaSettings";

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
    /// Whitelist of allowed output audio/video formats.
    /// </summary>
    public string[] AllowedFormats { get; set; } = ["mp3", "mp4"];

    /// <summary>
    /// Whitelist of allowed audio/video qualities.
    /// </summary>
    public string[] AllowedQualities { get; set; } = ["128", "192", "256", "320"];
}
