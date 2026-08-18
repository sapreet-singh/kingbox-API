using System.ComponentModel.DataAnnotations;

namespace KingBox.Api.DTOs;

/// <summary>
/// Request to inspect and retrieve media details from a source URL.
/// </summary>
public class MediaInfoRequest
{
    /// <summary>
    /// Source media URL.
    /// </summary>
    [Required(ErrorMessage = "URL is required.")]
    [Url(ErrorMessage = "A valid absolute URL is required.")]
    public string Url { get; set; } = string.Empty;
}
