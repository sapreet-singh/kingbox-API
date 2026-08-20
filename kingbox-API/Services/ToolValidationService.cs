using KingBox.Api.DTOs;
using KingBox.Api.Services.Interfaces;

namespace KingBox.Api.Services;

/// <summary>
/// Checks availability and versions of yt-dlp and FFmpeg executables without exposing server paths.
/// </summary>
public class ToolValidationService : IToolValidationService
{
    private readonly IProcessRunner _processRunner;
    private readonly IToolPathResolver _toolPathResolver;
    private readonly ILogger<ToolValidationService> _logger;

    public ToolValidationService(
        IProcessRunner processRunner,
        IToolPathResolver toolPathResolver,
        ILogger<ToolValidationService> logger)
    {
        _processRunner = processRunner;
        _toolPathResolver = toolPathResolver;
        _logger = logger;
    }

    public async Task<ToolStatusResponse> GetToolStatusAsync(CancellationToken cancellationToken = default)
    {
        var ytDlpInfo = await CheckYtDlpAsync(cancellationToken);
        var ffmpegInfo = await CheckFfmpegAsync(cancellationToken);

        return new ToolStatusResponse
        {
            YtDlp = ytDlpInfo,
            Ffmpeg = ffmpegInfo
        };
    }

    public async Task<bool> IsYtDlpAvailableAsync(CancellationToken cancellationToken = default)
    {
        var info = await CheckYtDlpAsync(cancellationToken);
        return info.Available;
    }

    public async Task<bool> IsFfmpegAvailableAsync(CancellationToken cancellationToken = default)
    {
        var info = await CheckFfmpegAsync(cancellationToken);
        return info.Available;
    }

    private async Task<ToolInfo> CheckYtDlpAsync(CancellationToken cancellationToken)
    {
        var resolvedPath = _toolPathResolver.ResolveYtDlpPath();
        try
        {
            var result = await _processRunner.RunAsync(new ProcessExecutionOptions(
                ExecutablePath: resolvedPath,
                Arguments: ["--version"],
                Timeout: TimeSpan.FromSeconds(8),
                CancellationToken: cancellationToken
            ));

            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                var version = result.StandardOutput.Trim().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                return new ToolInfo
                {
                    Available = true,
                    Version = version ?? "Unknown"
                };
            }

            return new ToolInfo
            {
                Available = false,
                ErrorMessage = "yt-dlp executable did not return a valid version response."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("yt-dlp check failed using path '{Path}': {Message}", resolvedPath, ex.Message);
            return new ToolInfo
            {
                Available = false,
                ErrorMessage = "yt-dlp executable was not found or is not accessible."
            };
        }
    }

    private async Task<ToolInfo> CheckFfmpegAsync(CancellationToken cancellationToken)
    {
        var resolvedPath = _toolPathResolver.ResolveFfmpegPath();
        try
        {
            var result = await _processRunner.RunAsync(new ProcessExecutionOptions(
                ExecutablePath: resolvedPath,
                Arguments: ["-version"],
                Timeout: TimeSpan.FromSeconds(8),
                CancellationToken: cancellationToken
            ));

            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                var firstLine = result.StandardOutput.Trim().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                var version = firstLine != null && firstLine.StartsWith("ffmpeg version", StringComparison.OrdinalIgnoreCase)
                    ? firstLine.Replace("ffmpeg version", "").Trim().Split(' ').FirstOrDefault()
                    : firstLine;

                return new ToolInfo
                {
                    Available = true,
                    Version = version ?? "Unknown"
                };
            }

            return new ToolInfo
            {
                Available = false,
                ErrorMessage = "FFmpeg executable did not return a valid version response."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("FFmpeg check failed using path '{Path}': {Message}", resolvedPath, ex.Message);
            return new ToolInfo
            {
                Available = false,
                ErrorMessage = "FFmpeg executable was not found or is not accessible."
            };
        }
    }
}
