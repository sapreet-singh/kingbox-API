using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using KingBox.Api.Configuration;
using KingBox.Api.DTOs;
using KingBox.Api.Services.Interfaces;

namespace KingBox.Api.Services;

/// <summary>
/// yt-dlp implementation of the media downloader.
/// </summary>
public partial class YtDlpMediaDownloader : IMediaDownloader
{
    private readonly IProcessRunner _processRunner;
    private readonly IToolPathResolver _toolPathResolver;
    private readonly MediaSettings _settings;
    private readonly ILogger<YtDlpMediaDownloader> _logger;

    public YtDlpMediaDownloader(
        IProcessRunner processRunner,
        IToolPathResolver toolPathResolver,
        IOptions<MediaSettings> settings,
        ILogger<YtDlpMediaDownloader> logger)
    {
        _processRunner = processRunner;
        _toolPathResolver = toolPathResolver;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<MediaInfoResponse> GetMediaInfoAsync(string url, CancellationToken cancellationToken = default)
    {
        var executablePath = _toolPathResolver.ResolveYtDlpPath();
        var arguments = new List<string>
        {
            "--dump-single-json",
            "--no-warnings",
            "--no-playlist",
            "--skip-download",
            url
        };

        var timeout = TimeSpan.FromMinutes(2);

        _logger.LogInformation("Retrieving media metadata for URL using yt-dlp ({Path}).", executablePath);

        var result = await _processRunner.RunAsync(new ProcessExecutionOptions(
            ExecutablePath: executablePath,
            Arguments: arguments,
            Timeout: timeout,
            CancellationToken: cancellationToken
        ));

        if (!result.IsSuccess)
        {
            var cleanError = GetUserFriendlyErrorMessage(result.StandardError);
            _logger.LogWarning("yt-dlp failed to inspect URL. Stderr: {Error}", result.StandardError);
            throw new InvalidOperationException(cleanError);
        }

        try
        {
            using var doc = JsonDocument.Parse(result.StandardOutput);
            var root = doc.RootElement;

            var title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : "Untitled Media";
            var duration = root.TryGetProperty("duration", out var durProp) && durProp.TryGetDouble(out var dur) ? dur : (double?)null;
            var thumbnail = root.TryGetProperty("thumbnail", out var thumbProp) ? thumbProp.GetString() : null;
            var webpageUrl = root.TryGetProperty("webpage_url", out var webProp) ? webProp.GetString() : url;

            // Formats and qualities from configured capabilities
            var availableFormats = _settings.AllowedFormats.ToList();
            var availableQualities = _settings.AllowedQualities.ToList();

            return new MediaInfoResponse
            {
                Title = title,
                Duration = duration,
                ThumbnailUrl = thumbnail,
                SourceUrl = webpageUrl,
                AvailableFormats = availableFormats,
                AvailableQualities = availableQualities
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse yt-dlp JSON output.");
            throw new InvalidOperationException("Failed to process media information returned from the source.");
        }
    }

    public async Task<string> DownloadSourceMediaAsync(
        string url,
        string outputDirectory,
        Action<double?>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var executablePath = _toolPathResolver.ResolveYtDlpPath();
        var outputTemplate = Path.Combine(outputDirectory, "source.%(ext)s");

        var arguments = new List<string>
        {
            "--no-warnings",
            "--no-playlist",
            "--newline",
            "--progress",
            "--no-continue",
            "--retries", "3",
            "-o", outputTemplate,
            url
        };

        var timeout = TimeSpan.FromMinutes(_settings.ProcessTimeoutMinutes);

        _logger.LogInformation("Starting yt-dlp source download into directory {Dir}", outputDirectory);

        var downloadRegex = DownloadProgressRegex();

        var result = await _processRunner.RunAsync(new ProcessExecutionOptions(
            ExecutablePath: executablePath,
            Arguments: arguments,
            WorkingDirectory: outputDirectory,
            Timeout: timeout,
            OnOutputLine: line =>
            {
                var match = downloadRegex.Match(line);
                if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
                {
                    onProgress?.Invoke(Math.Clamp(pct, 0.0, 100.0));
                }
            },
            CancellationToken: cancellationToken
        ));

        if (!result.IsSuccess)
        {
            if (result.IsCancelled)
            {
                throw new OperationCanceledException("Download process was cancelled.");
            }
            if (result.IsTimedOut)
            {
                throw new TimeoutException($"Media download exceeded timeout of {_settings.ProcessTimeoutMinutes} minutes.");
            }

            var cleanError = GetUserFriendlyErrorMessage(result.StandardError);
            _logger.LogWarning("yt-dlp download failed. ExitCode: {Code}, Stderr: {Error}", result.ExitCode, result.StandardError);
            throw new InvalidOperationException(cleanError);
        }

        // Find the downloaded source file in outputDirectory
        var downloadedFiles = Directory.GetFiles(outputDirectory, "source.*");
        if (downloadedFiles.Length == 0)
        {
            throw new FileNotFoundException("Downloaded media source file was not found in temporary directory.");
        }

        var sourceFile = downloadedFiles.OrderByDescending(f => new FileInfo(f).Length).First();
        _logger.LogInformation("Source media download complete. File: {SourceFile}", Path.GetFileName(sourceFile));

        return sourceFile;
    }

    private static string GetUserFriendlyErrorMessage(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return "Failed to process media from the requested URL.";
        }

        var lower = stderr.ToLowerInvariant();
        if (lower.Contains("unsupported url") || lower.Contains("is not a valid url"))
        {
            return "The provided URL is not supported or accessible.";
        }
        if (lower.Contains("private video") || lower.Contains("sign in"))
        {
            return "This media is private or requires authentication and cannot be accessed.";
        }
        if (lower.Contains("video unavailable") || lower.Contains("404"))
        {
            return "The requested media is unavailable or was removed.";
        }
        if (lower.Contains("drm") || lower.Contains("protected"))
        {
            return "Protected or DRM-restricted media cannot be downloaded.";
        }

        // Extract first ERROR: line if present
        var lines = stderr.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var errorLine = lines.FirstOrDefault(l => l.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase));
        if (errorLine != null)
        {
            var cleaned = errorLine.Replace("ERROR:", "").Trim();
            if (cleaned.Length > 150) cleaned = cleaned[..150];
            return cleaned;
        }

        return "An error occurred while downloading the media from the source.";
    }

    [GeneratedRegex(@"\[download\]\s+(\d+(?:\.\d+)?)%")]
    private static partial Regex DownloadProgressRegex();
}
