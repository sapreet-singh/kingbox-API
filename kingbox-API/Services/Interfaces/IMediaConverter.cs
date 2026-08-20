namespace KingBox.Api.Services.Interfaces;

/// <summary>
/// Converter service interfacing with FFmpeg for audio transcoding and video remuxing/encoding.
/// </summary>
public interface IMediaConverter
{
    /// <summary>
    /// Converts a downloaded source media file into the requested format and quality.
    /// </summary>
    /// <returns>Absolute path to the converted media output file.</returns>
    Task<string> ConvertAsync(
        string sourceFilePath,
        string outputDirectory,
        string targetFormat,
        string targetQuality,
        string sanitizedBaseName,
        double? durationSeconds = null,
        Action<double?>? onProgress = null,
        CancellationToken cancellationToken = default);
}
