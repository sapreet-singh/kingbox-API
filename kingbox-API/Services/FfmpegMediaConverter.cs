using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using KingBox.Api.Configuration;
using KingBox.Api.Services.Interfaces;

namespace KingBox.Api.Services;

/// <summary>
/// FFmpeg implementation of the media converter.
/// </summary>
public partial class FfmpegMediaConverter : IMediaConverter
{
    private readonly IProcessRunner _processRunner;
    private readonly IToolPathResolver _toolPathResolver;
    private readonly MediaSettings _settings;
    private readonly ILogger<FfmpegMediaConverter> _logger;

    public FfmpegMediaConverter(
        IProcessRunner processRunner,
        IToolPathResolver toolPathResolver,
        IOptions<MediaSettings> settings,
        ILogger<FfmpegMediaConverter> logger)
    {
        _processRunner = processRunner;
        _toolPathResolver = toolPathResolver;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> ConvertAsync(
        string sourceFilePath,
        string outputDirectory,
        string targetFormat,
        string targetQuality,
        string sanitizedBaseName,
        double? durationSeconds = null,
        Action<double?>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"Source file not found for conversion: {sourceFilePath}");
        }

        var executablePath = _toolPathResolver.ResolveFfmpegPath();
        var format = targetFormat.Trim().ToLowerInvariant();
        var quality = targetQuality.Trim().ToLowerInvariant();
        var outputFileName = $"{sanitizedBaseName}.{format}";
        var outputFilePath = Path.Combine(outputDirectory, outputFileName);

        var arguments = BuildFfmpegArguments(sourceFilePath, outputFilePath, format, quality);

        var timeout = TimeSpan.FromMinutes(_settings.ProcessTimeoutMinutes);
        var timeRegex = FfmpegTimeRegex();

        _logger.LogInformation("Starting FFmpeg conversion to format {Format}, quality {Quality} using {Path}", format, quality, executablePath);

        var result = await _processRunner.RunAsync(new ProcessExecutionOptions(
            ExecutablePath: executablePath,
            Arguments: arguments,
            WorkingDirectory: outputDirectory,
            Timeout: timeout,
            OnErrorLine: line =>
            {
                // FFmpeg outputs progress stats to stderr
                if (durationSeconds.HasValue && durationSeconds.Value > 0)
                {
                    var match = timeRegex.Match(line);
                    if (match.Success)
                    {
                        var timeStr = match.Groups[1].Value;
                        if (TimeSpan.TryParse(timeStr, CultureInfo.InvariantCulture, out var currentTime))
                        {
                            var pct = (currentTime.TotalSeconds / durationSeconds.Value) * 100.0;
                            onProgress?.Invoke(Math.Clamp(Math.Round(pct, 1), 0.0, 100.0));
                        }
                    }
                }
            },
            CancellationToken: cancellationToken
        ));

        if (!result.IsSuccess)
        {
            if (result.IsCancelled)
            {
                throw new OperationCanceledException("Conversion process was cancelled.");
            }
            if (result.IsTimedOut)
            {
                throw new TimeoutException($"Media conversion exceeded timeout of {_settings.ProcessTimeoutMinutes} minutes.");
            }

            _logger.LogWarning("FFmpeg conversion failed. ExitCode: {Code}, Stderr: {Error}", result.ExitCode, result.StandardError);
            throw new InvalidOperationException("Failed to convert media file with FFmpeg.");
        }

        if (!File.Exists(outputFilePath) || new FileInfo(outputFilePath).Length == 0)
        {
            throw new FileNotFoundException("Converted output file was not produced.");
        }

        _logger.LogInformation("FFmpeg conversion completed successfully: {OutputFile}", outputFileName);
        return outputFilePath;
    }

    private static List<string> BuildFfmpegArguments(string inputPath, string outputPath, string format, string quality)
    {
        var args = new List<string>
        {
            "-y", // Overwrite output file without asking
            "-i", inputPath
        };

        if (format == "mp3")
        {
            var bitrate = quality switch
            {
                "128" => "128k",
                "192" => "192k",
                "256" => "256k",
                "320" => "320k",
                _ => "192k"
            };

            args.AddRange([
                "-vn", // Strip video stream
                "-c:a", "libmp3lame",
                "-b:a", bitrate,
                "-q:a", "0"
            ]);
        }
        else if (format == "mp4")
        {
            args.AddRange([
                "-c:v", "libx264",
                "-preset", "fast",
                "-crf", "23",
                "-c:a", "aac",
                "-b:a", "192k",
                "-movflags", "+faststart"
            ]);
        }
        else
        {
            // Default copy/remux fallback
            args.AddRange([
                "-c", "copy"
            ]);
        }

        args.Add(outputPath);
        return args;
    }

    [GeneratedRegex(@"time=(\d{2}:\d{2}:\d{2}(?:\.\d+)?)")]
    private static partial Regex FfmpegTimeRegex();
}
