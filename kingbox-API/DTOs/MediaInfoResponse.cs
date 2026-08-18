namespace KingBox.Api.DTOs;

/// <summary>
/// Media information details for a requested source URL.
/// </summary>
public class MediaInfoResponse
{
    /// <summary>
    /// Title of the media item.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Total duration in seconds or formatted string.
    /// </summary>
    public double? Duration { get; set; }

    /// <summary>
    /// URL to media thumbnail image.
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Original source URL.
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// List of available formats (e.g. mp3, mp4, webm).
    /// </summary>
    public List<string>? AvailableFormats { get; set; }

    /// <summary>
    /// List of available quality presets (e.g. 128, 192, 256, 320, 720p, 1080p).
    /// </summary>
    public List<string>? AvailableQualities { get; set; }
}
