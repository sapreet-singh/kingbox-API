using Microsoft.AspNetCore.Mvc;
using KingBox.Api.DTOs;
using KingBox.Api.Services.Interfaces;

namespace KingBox.Api.Controllers;

/// <summary>
/// Handles media information retrieval, conversion lifecycle requests, progress tracking, cancellation, tool statuses, and file downloads.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MediaController : ControllerBase
{
    private readonly IMediaService _mediaService;
    private readonly ITemporaryFileService _tempFileService;
    private readonly ILogger<MediaController> _logger;

    public MediaController(
        IMediaService mediaService,
        ITemporaryFileService tempFileService,
        ILogger<MediaController> logger)
    {
        _mediaService = mediaService;
        _tempFileService = tempFileService;
        _logger = logger;
    }

    /// <summary>
    /// Checks availability and installed versions of yt-dlp and FFmpeg.
    /// </summary>
    /// <response code="200">Tool readiness and version information.</response>
    [HttpGet("tools/status")]
    [ProducesResponseType(typeof(ApiResponse<ToolStatusResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetToolStatus(CancellationToken cancellationToken)
    {
        var status = await _mediaService.GetToolStatusAsync(cancellationToken);
        return Ok(ApiResponse<ToolStatusResponse>.Ok(status, "Tool status retrieved."));
    }

    /// <summary>
    /// Inspects and retrieves media details for a given source URL using yt-dlp.
    /// </summary>
    /// <param name="request">Source URL request object.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Media info successfully retrieved.</response>
    /// <response code="400">Invalid or empty URL provided.</response>
    /// <response code="409">Tool unavailable or failed to inspect URL.</response>
    [HttpPost("info")]
    [ProducesResponseType(typeof(ApiResponse<MediaInfoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetMediaInfo(
        [FromBody] MediaInfoRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(CreateValidationErrorResponse());
        }

        var response = await _mediaService.GetMediaInfoAsync(request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Initiates and queues a new media download/conversion job.
    /// </summary>
    /// <param name="request">Conversion parameters including URL, format, and quality.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Conversion request accepted and queued.</response>
    /// <response code="400">Invalid parameters, unsupported format, or unsupported quality.</response>
    [HttpPost("convert")]
    [ProducesResponseType(typeof(ConversionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConvertMedia(
        [FromBody] ConversionRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(CreateValidationErrorResponse());
        }

        var response = await _mediaService.StartConversionAsync(request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Retrieves current progress and execution status of a conversion job.
    /// </summary>
    /// <param name="id">Unique conversion job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Progress and status details returned.</response>
    /// <response code="400">Invalid conversion ID format.</response>
    /// <response code="404">Conversion job not found.</response>
    [HttpGet("progress/{id:guid}")]
    [ProducesResponseType(typeof(ConversionProgressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProgress(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var progress = await _mediaService.GetProgressAsync(id, cancellationToken);
        if (progress is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                Message = $"Conversion job with ID '{id}' was not found.",
                ErrorCode = "JOB_NOT_FOUND"
            });
        }

        return Ok(progress);
    }

    /// <summary>
    /// Downloads the finalized media file for a completed conversion job and cleans up temporary files upon transmission.
    /// </summary>
    /// <param name="id">Unique conversion job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns media file binary stream.</response>
    /// <response code="400">Invalid conversion ID format.</response>
    /// <response code="404">Conversion job or output file not found.</response>
    /// <response code="409">Conversion job is not completed yet.</response>
    [HttpGet("download/{id:guid}")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DownloadMedia(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var fileInfo = await _mediaService.GetDownloadFileAsync(id, cancellationToken);
        if (fileInfo is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                Message = $"Conversion job with ID '{id}' was not found.",
                ErrorCode = "JOB_NOT_FOUND"
            });
        }

        // Clean up temporary files on disk after client finishes receiving the stream
        Response.OnCompleted(() =>
        {
            try
            {
                _tempFileService.CleanupJob(fileInfo.JobId);
                _logger.LogInformation("Cleaned up temporary workspace for job {JobId} after download transmission.", fileInfo.JobId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up temporary files post-download for job {JobId}.", fileInfo.JobId);
            }
            return Task.CompletedTask;
        });

        return PhysicalFile(fileInfo.FilePath, fileInfo.ContentType, fileInfo.DownloadFileName, enableRangeProcessing: true);
    }

    /// <summary>
    /// Cancels an in-progress or queued conversion job.
    /// </summary>
    /// <param name="id">Unique conversion job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Conversion job was successfully cancelled.</response>
    /// <response code="400">Invalid conversion ID format.</response>
    /// <response code="404">Conversion job not found.</response>
    /// <response code="409">Conversion job is already completed.</response>
    [HttpPost("cancel/{id:guid}")]
    [ProducesResponseType(typeof(CancelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelMedia(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _mediaService.CancelConversionAsync(id, cancellationToken);
        if (response is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                Message = $"Conversion job with ID '{id}' was not found.",
                ErrorCode = "JOB_NOT_FOUND"
            });
        }

        return Ok(response);
    }

    private ApiErrorResponse CreateValidationErrorResponse()
    {
        var errors = ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage).ToArray()
            );

        return new ApiErrorResponse
        {
            Success = false,
            Message = "One or more validation errors occurred.",
            ErrorCode = "VALIDATION_ERROR",
            Errors = errors
        };
    }
}
