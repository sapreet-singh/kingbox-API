using Microsoft.Extensions.Options;
using KingBox.Api.Configuration;
using KingBox.Api.Services.Interfaces;

namespace KingBox.Api.Services;

/// <summary>
/// Dynamically resolves tool paths checking explicit paths, local Tools folders, and system PATH.
/// </summary>
public class ToolPathResolver : IToolPathResolver
{
    private readonly MediaSettings _settings;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ToolPathResolver> _logger;

    public ToolPathResolver(
        IOptions<MediaSettings> settings,
        IWebHostEnvironment env,
        ILogger<ToolPathResolver> logger)
    {
        _settings = settings.Value;
        _env = env;
        _logger = logger;
    }

    public string ResolveYtDlpPath() => Resolve(_settings.YtDlpPath, "yt-dlp");

    public string ResolveFfmpegPath() => Resolve(_settings.FfmpegPath, "ffmpeg");

    private string Resolve(string configuredPath, string defaultName)
    {
        var target = string.IsNullOrWhiteSpace(configuredPath) ? defaultName : configuredPath.Trim();

        // 1. Direct absolute/relative file path check
        if (File.Exists(target))
        {
            return Path.GetFullPath(target);
        }

        // 2. Candidate paths in workspace / parent Tools directories
        var isWindows = OperatingSystem.IsWindows();
        var candidates = new List<string>
        {
            Path.Combine(_env.ContentRootPath, "Tools", target),
            Path.Combine(_env.ContentRootPath, "..", "Tools", target),
            Path.Combine(AppContext.BaseDirectory, "Tools", target),
            Path.Combine(AppContext.BaseDirectory, "..", "Tools", target),
        };

        if (isWindows)
        {
            var exeName = target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? target : $"{target}.exe";
            candidates.Add(Path.Combine(_env.ContentRootPath, "Tools", exeName));
            candidates.Add(Path.Combine(_env.ContentRootPath, "..", "Tools", exeName));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "Tools", exeName));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "..", "Tools", exeName));
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                var full = Path.GetFullPath(candidate);
                _logger.LogDebug("Resolved {DefaultName} to local candidate: {Path}", defaultName, full);
                return full;
            }
        }

        // 3. Fallback to command name for PATH resolution (standard in Linux/Docker/PATH)
        return target;
    }
}
