using System.ComponentModel.DataAnnotations;

namespace KingBox.Api.DTOs;

/// <summary>
/// Request payload to initiate a media download/conversion.
/// </summary>
public class ConversionRequest
{
    /// <summary>
    /// Source media URL.
    /// </summary>
    [Required(ErrorMessage = "URL is required.")]
    [Url(ErrorMessage = "A valid absolute URL is required.")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Desired media format (e.g. mp3, mp4).
    /// </summary>
    [Required(ErrorMessage = "Format is required.")]
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Desired quality option (e.g. 128, 192, 256, 320).
    /// </summary>
    [Required(ErrorMessage = "Quality is required.")]
    public string Quality { get; set; } = string.Empty;
}
