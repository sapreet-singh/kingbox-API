using Microsoft.Extensions.Options;
using KingBox.Api.Configuration;
using KingBox.Api.Models;
using KingBox.Api.Services.Interfaces;

namespace KingBox.Api.Services;

/// <summary>
/// Background hosted service processing queued media conversion jobs with concurrency throttling, startup cleanup, and graceful shutdown.
/// </summary>
public class MediaProcessingWorker : BackgroundService
{
    private readonly IConversionQueue _queue;
    private readonly IConversionJobStore _jobStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MediaSettings _settings;
    private readonly ILogger<MediaProcessingWorker> _logger;
    private readonly SemaphoreSlim _semaphore;

    public MediaProcessingWorker(
        IConversionQueue queue,
        IConversionJobStore jobStore,
        IServiceScopeFactory scopeFactory,
        IOptions<MediaSettings> settings,
        ILogger<MediaProcessingWorker> logger)
    {
        _queue = queue;
        _jobStore = jobStore;
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
        _semaphore = new SemaphoreSlim(_settings.MaxConcurrentConversions, _settings.MaxConcurrentConversions);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KingBox Media Processing Worker started. Max concurrency: {Concurrency}", _settings.MaxConcurrentConversions);

        // Purge any stale temporary job folders left from a previous crash/restart
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var tempFileService = scope.ServiceProvider.GetRequiredService<ITemporaryFileService>();
            tempFileService.CleanupAllStaleJobs();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to run startup temporary directory cleanup.");
        }

        // Start background cleanup timer loop for old jobs
        _ = RunPeriodicCleanupAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobId = await _queue.DequeueAsync(stoppingToken);

                // Wait for an available concurrency slot
                await _semaphore.WaitAsync(stoppingToken);

                // Process task within concurrency bounds
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessJobAsync(jobId, stoppingToken);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in media processing worker loop.");
            }
        }

        // Graceful shutdown: Cancel any remaining active in-flight jobs
        try
        {
            var activeJobs = _jobStore.GetAll()
                .Where(j => j.Status is ConversionStatus.Pending
                                     or ConversionStatus.Downloading
                                     or ConversionStatus.Converting
                                     or ConversionStatus.Finalizing);

            foreach (var activeJob in activeJobs)
            {
                try
                {
                    activeJob.CancellationTokenSource?.Cancel();
                }
                catch (Exception cancelEx)
                {
                    _logger.LogDebug(cancelEx, "Could not cancel job {JobId} during worker shutdown.", activeJob.Id);
                }
            }
        }
        catch (Exception shutdownEx)
        {
            _logger.LogWarning(shutdownEx, "Error during worker shutdown cleanup.");
        }

        _logger.LogInformation("KingBox Media Processing Worker stopped.");
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken stoppingToken)
    {
        if (!_jobStore.TryGet(jobId, out var job) || job == null)
        {
            _logger.LogWarning("Job {JobId} not found in store upon processing.", jobId);
            return;
        }

        if (job.Status == ConversionStatus.Cancelled)
        {
            _logger.LogInformation("Job {JobId} was cancelled before processing started.", jobId);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var toolValidator = scope.ServiceProvider.GetRequiredService<IToolValidationService>();
        var downloader = scope.ServiceProvider.GetRequiredService<IMediaDownloader>();
        var converter = scope.ServiceProvider.GetRequiredService<IMediaConverter>();
        var tempFileService = scope.ServiceProvider.GetRequiredService<ITemporaryFileService>();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken,
            job.CancellationTokenSource?.Token ?? CancellationToken.None);

        var cancellationToken = linkedCts.Token;

        try
        {
            // 1. Tool availability verification
            var ytDlpAvailable = await toolValidator.IsYtDlpAvailableAsync(cancellationToken);
            var ffmpegAvailable = await toolValidator.IsFfmpegAvailableAsync(cancellationToken);

            if (!ytDlpAvailable || !ffmpegAvailable)
            {
                var missing = new List<string>();
                if (!ytDlpAvailable) missing.Add("yt-dlp");
                if (!ffmpegAvailable) missing.Add("FFmpeg");

                throw new InvalidOperationException($"Required external tool(s) missing or inaccessible: {string.Join(", ", missing)}. Please check tool configuration.");
            }

            // 2. Setup isolated temporary directory
            var jobDir = tempFileService.CreateJobDirectory(job.Id);
            job.TempDirectory = jobDir;

            // 3. Stage: Downloading
            job.Status = ConversionStatus.Downloading;
            job.Stage = "Downloading media from source...";
            job.Progress = 0;
            _jobStore.TryUpdate(job);
            _logger.LogInformation("Job {JobId} entered Downloading stage.", job.Id);

            // Attempt to retrieve title/metadata for clean output naming
            string rawTitle = "media";
            double? duration = null;
            try
            {
                var info = await downloader.GetMediaInfoAsync(job.Url, cancellationToken);
                if (!string.IsNullOrWhiteSpace(info.Title))
                {
                    rawTitle = info.Title;
                }
                duration = info.Duration;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not pre-fetch metadata for title. Using default naming.");
            }

            var sanitizedBaseName = Path.GetFileNameWithoutExtension(
                tempFileService.SanitizeFileName(rawTitle, job.Format));

            var sourceFilePath = await downloader.DownloadSourceMediaAsync(
                job.Url,
                jobDir,
                pct =>
                {
                    job.Progress = pct;
                    _jobStore.TryUpdate(job);
                },
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // 4. Stage: Converting
            job.Status = ConversionStatus.Converting;
            job.Stage = $"Converting media to {job.Format.ToUpperInvariant()} ({job.Quality})...";
            job.Progress = 0;
            _jobStore.TryUpdate(job);
            _logger.LogInformation("Job {JobId} entered Converting stage.", job.Id);

            var outputFilePath = await converter.ConvertAsync(
                sourceFilePath,
                jobDir,
                job.Format,
                job.Quality,
                sanitizedBaseName,
                duration,
                pct =>
                {
                    job.Progress = pct;
                    _jobStore.TryUpdate(job);
                },
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // 5. Stage: Finalizing
            job.Status = ConversionStatus.Finalizing;
            job.Stage = "Finalizing output file...";
            _jobStore.TryUpdate(job);

            var fileInfo = new FileInfo(outputFilePath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                throw new FileNotFoundException("Converted media file is empty or missing.");
            }

            if (fileInfo.Length > _settings.MaxFileSize)
            {
                throw new InvalidOperationException($"Output file size ({fileInfo.Length / 1024 / 1024} MB) exceeds maximum allowed limit ({_settings.MaxFileSize / 1024 / 1024} MB).");
            }

            // Clean intermediate source file to conserve disk space
            if (File.Exists(sourceFilePath) && !string.Equals(sourceFilePath, outputFilePath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(sourceFilePath);
                }
                catch (Exception delEx)
                {
                    _logger.LogDebug(delEx, "Could not delete intermediate source file: {File}", sourceFilePath);
                }
            }

            // 6. Stage: Completed
            job.Status = ConversionStatus.Completed;
            job.Stage = "Completed";
            job.Progress = 100;
            job.FilePath = outputFilePath;
            job.FileName = Path.GetFileName(outputFilePath);
            job.CompletedAt = DateTime.UtcNow;
            _jobStore.TryUpdate(job);

            _logger.LogInformation("Job {JobId} completed successfully. Output: {FileName} ({Size} KB)", job.Id, job.FileName, fileInfo.Length / 1024);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Job {JobId} was cancelled during execution.", job.Id);
            job.Status = ConversionStatus.Cancelled;
            job.Stage = "Cancelled";
            job.CompletedAt = DateTime.UtcNow;
            _jobStore.TryUpdate(job);

            tempFileService.CleanupJob(job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed during execution: {Message}", job.Id, ex.Message);
            job.Status = ConversionStatus.Failed;
            job.Stage = "Failed";
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            _jobStore.TryUpdate(job);

            tempFileService.CleanupJob(job.Id);
        }
    }

    private async Task RunPeriodicCleanupAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(1);
        var maxAge = TimeSpan.FromHours(_settings.OldJobCleanupHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var tempFileService = scope.ServiceProvider.GetRequiredService<ITemporaryFileService>();
                tempFileService.CleanupOldJobs(maxAge);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Periodic temporary file cleanup encountered an error.");
            }
        }
    }
}
