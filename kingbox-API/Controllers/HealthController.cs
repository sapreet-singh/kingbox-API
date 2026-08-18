using Microsoft.AspNetCore.Mvc;

namespace KingBox.Api.Controllers;

/// <summary>
/// Health check endpoint for system readiness and diagnostics.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Checks API health and application status.
    /// </summary>
    /// <response code="200">API service is healthy and operational.</response>
    [HttpGet]
    [ProducesResponseType(typeof(HealthStatusResponse), StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok(new HealthStatusResponse
        {
            Status = "Healthy",
            Application = "KingBox.Api"
        });
    }
}

/// <summary>
/// Health check status response.
/// </summary>
public class HealthStatusResponse
{
    public string Status { get; set; } = "Healthy";
    public string Application { get; set; } = "KingBox.Api";
}
