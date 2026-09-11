using HackerNewsBestStories.Api.Models;
using HackerNewsBestStories.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HackerNewsBestStories.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class BestStoriesController(IHackerNewsService hackerNewsService) : ControllerBase
{
    /// <summary>
    /// Returns the best n Hacker News stories, ranked by score, highest first.
    /// Example: GET /api/beststories/10
    /// </summary>
    [HttpGet("{n}")]
    [ProducesResponseType(typeof(IReadOnlyList<StoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<StoryDto>>> GetBestStories(
        string n,
        CancellationToken cancellationToken)
    {
        // Parsed by hand rather than an {n:int} route constraint, so an invalid value gives
        // callers a clear 400 with an explanation instead of a bare, unhelpful 404.
        if (!int.TryParse(n, out var count) || count < 1)
        {
            return BadRequest($"'{n}' is not valid. n must be a whole number of 1 or more.");
        }

        var stories = await hackerNewsService.GetBestStoriesAsync(count, cancellationToken);
        return Ok(stories);
    }
}
