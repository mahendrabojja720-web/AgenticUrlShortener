namespace AgenticUrlShortener.Controllers;

using Microsoft.AspNetCore.Mvc;
using AgenticUrlShortener.Application.Services;
using AgenticUrlShortener.Application.DTOs;

[ApiController]
[Route("api/[controller]")]
public class UrlShortenerController : ControllerBase
{
    private readonly IUrlShortenerService _service;
    private readonly ILogger<UrlShortenerController> _logger;

    public UrlShortenerController(IUrlShortenerService service, ILogger<UrlShortenerController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Create a shortened URL.
    /// </summary>
    [HttpPost("create")]
    [Produces(typeof(CreateShortUrlResponse))]
    public async Task<IActionResult> CreateShortUrl([FromBody] CreateShortUrlRequest request)
    {
        try
        {
            var response = await _service.CreateShortUrlAsync(request);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning($"Validation error: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning($"Operation error: {ex.Message}");
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating short URL");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get a shortened URL by short code.
    /// </summary>
    [HttpGet("{shortCode}")]
    [Produces(typeof(GetShortUrlResponse))]
    public async Task<IActionResult> GetShortUrl(string shortCode)
    {
        try
        {
            var response = await _service.GetShortUrlAsync(shortCode);
            if (response == null)
            {
                return NotFound(new { error = "Short URL not found or expired" });
            }
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving short URL");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get analytics for a shortened URL.
    /// </summary>
    [HttpGet("{shortCode}/analytics")]
    [Produces(typeof(AnalyticsResponse))]
    public async Task<IActionResult> GetAnalytics(string shortCode)
    {
        try
        {
            var response = await _service.GetAnalyticsAsync(shortCode);
            if (response == null)
            {
                return NotFound(new { error = "Short URL not found" });
            }
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving analytics");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete a shortened URL.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteShortUrl(string id)
    {
        try
        {
            var success = await _service.DeleteShortUrlAsync(id);
            if (!success)
            {
                return NotFound(new { error = "Short URL not found" });
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting short URL");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// List all active shortened URLs.
    /// </summary>
    [HttpGet]
    [Produces(typeof(List<GetShortUrlResponse>))]
    public async Task<IActionResult> ListUrls()
    {
        try
        {
            var response = await _service.ListUrlsAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing URLs");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
