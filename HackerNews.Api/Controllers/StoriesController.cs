using HackerNews.Application.Configuration;
using HackerNews.Application.DTOs;
using HackerNews.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HackerNews.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StoriesController : ControllerBase
{
    private readonly IStoryService _storyService;
    private readonly StoryServiceSettings _settings;

    public StoriesController(
        IStoryService storyService,
        IOptions<StoryServiceSettings> settings)
    {
        _storyService = storyService;
        _settings = settings.Value;
    }

    /// <summary>
    /// Returns the top n best stories from Hacker News, sorted by score descending.
    /// </summary>
    /// <param name="n">Number of stories to return (1–500).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Array of stories sorted by score descending.</response>
    /// <response code="400">Invalid value for n.</response>
    /// <response code="503">Hacker News API is unavailable.</response>
    [HttpGet("best")]
    [ProducesResponseType(typeof(IReadOnlyList<StoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetBestStories(
        [FromQuery] int n = 10,
        CancellationToken cancellationToken = default)
    {
        if (n < 1 || n > _settings.MaxStoriesLimit)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid parameter",
                Detail = $"Parameter 'n' must be between 1 and {_settings.MaxStoriesLimit}.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            var stories = await _storyService.GetBestStoriesAsync(n, cancellationToken);
            return Ok(stories);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "External service unavailable",
                Detail = "The Hacker News API is currently unreachable. Please try again later.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
    }
}