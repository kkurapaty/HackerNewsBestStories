using FluentAssertions;
using HackerNewsBestStories.Api.Controllers;
using HackerNewsBestStories.Api.Models;
using HackerNewsBestStories.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace HackerNewsBestStories.Tests;

public class BestStoriesControllerTests
{
    private readonly Mock<IHackerNewsService> _service = new();
    private BestStoriesController CreateSut() => new(_service.Object);

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    [InlineData("")]
    public async Task GetBestStories_ReturnsBadRequest_ForInvalidN(string n)
    {
        var result = await CreateSut().GetBestStories(n, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _service.Verify(s => s.GetBestStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetBestStories_ReturnsOkWithStories_ForValidN()
    {
        var stories = new List<StoryDto>
        {
            new("Title", "https://example.com/item?id=", "author", DateTimeOffset.UtcNow, 100, 5)
        };
        _service.Setup(s => s.GetBestStoriesAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(stories);

        var result = await CreateSut().GetBestStories("3", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(stories);
    }

    [Fact]
    public async Task GetBestStories_PassesParsedCount_ToService()
    {
        _service.Setup(s => s.GetBestStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StoryDto>());

        await CreateSut().GetBestStories("25", CancellationToken.None);

        _service.Verify(s => s.GetBestStoriesAsync(25, It.IsAny<CancellationToken>()), Times.Once);
    }
}
