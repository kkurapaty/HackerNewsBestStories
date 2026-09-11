using FluentAssertions;
using HackerNewsBestStories.Api.Infrastructure;
using HackerNewsBestStories.Api.Models;
using HackerNewsBestStories.Api.Options;
using HackerNewsBestStories.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace HackerNewsBestStories.Tests;

public class HackerNewsServiceTests
{
    private readonly Mock<IHackerNewsClient> _client = new(MockBehavior.Strict);
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private HackerNewsService CreateSut(HackerNewsOptions? options = null) =>
        new(_client.Object, _cache, new KeyedLock(), Options.Create(options ?? new HackerNewsOptions()));

    private static HackerNewsItem Story(int id, string title, int score, int comments = 0, string? url = null,
        string type = "story", bool deleted = false, bool dead = false, string by = "someone") => new()
        {
            Id = id,
            Title = title,
            Score = score,
            Descendants = comments,
            Url = url ?? $"https://example.com/item?id={id}",
            Type = type,
            Deleted = deleted,
            Dead = dead,
            By = by,
            Time = 1570887781 // 2019-10-12T13:43:01Z, matches the exercise's sample output
        };

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsStories_OrderedByScoreDescending()
    {
        var ids = new[] { 1, 2, 3 };
        _client.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ids);
        _client.Setup(c => c.GetItemAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Story(1, "Low", 10));
        _client.Setup(c => c.GetItemAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(Story(2, "High", 500));
        _client.Setup(c => c.GetItemAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(Story(3, "Mid", 100));

        var result = await CreateSut().GetBestStoriesAsync(3, CancellationToken.None);

        result.Select(s => s.Title).Should().ContainInOrder("High", "Mid", "Low");
    }

    [Fact]
    public async Task GetBestStoriesAsync_LimitsResultsToRequestedCount()
    {
        var ids = new[] { 1, 2, 3 };
        _client.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ids);
        foreach (var id in ids)
        {
            _client.Setup(c => c.GetItemAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(Story(id, $"Story {id}", id));
        }

        var result = await CreateSut().GetBestStoriesAsync(2, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Story 3");
        result[1].Title.Should().Be("Story 2");
    }

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsAllAvailable_WhenCountExceedsAvailableStories()
    {
        _client.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([1, 2]);
        _client.Setup(c => c.GetItemAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Story(1, "A", 5));
        _client.Setup(c => c.GetItemAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(Story(2, "B", 10));

        var result = await CreateSut().GetBestStoriesAsync(50, CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBestStoriesAsync_FiltersOutDeletedDeadAndNonStoryItems()
    {
        _client.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([1, 2, 3, 4, 5]);
        _client.Setup(c => c.GetItemAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Story(1, "Good", 100));
        _client.Setup(c => c.GetItemAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(Story(2, "Deleted", 999, deleted: true));
        _client.Setup(c => c.GetItemAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(Story(3, "Dead", 999, dead: true));
        _client.Setup(c => c.GetItemAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(Story(4, "AJob", 999, type: "job"));
        _client.Setup(c => c.GetItemAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync((HackerNewsItem?)null); // missing item

        var result = await CreateSut().GetBestStoriesAsync(10, CancellationToken.None);

        result.Should().ContainSingle().Which.Title.Should().Be("Good");
    }

    [Fact]
    public async Task GetBestStoriesAsync_FallsBackToDiscussionLink_WhenStoryHasNoUrl()
    {
        _client.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([42]);
        _client.Setup(c => c.GetItemAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Story(42, "Ask HN: something", 20, url: null!));

        var result = await CreateSut().GetBestStoriesAsync(1, CancellationToken.None);

        result[0].Uri.Should().Be("https://example.com/item?id=42");
    }

    [Fact]
    public async Task GetBestStoriesAsync_MapsFieldsCorrectly()
    {
        _client.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([21233041]);
        _client.Setup(c => c.GetItemAsync(21233041, It.IsAny<CancellationToken>())).ReturnsAsync(Story(
            21233041, "A uBlock Origin update was rejected from the Chrome Web Store", 1716,
            comments: 572, url: "https://github.com/uBlockOrigin/uBlock-issues/issues/745", by: "ismaildonmez"));

        var result = await CreateSut().GetBestStoriesAsync(1, CancellationToken.None);

        var story = result.Single();
        story.PostedBy.Should().Be("ismaildonmez");
        story.CommentCount.Should().Be(572);
        story.Score.Should().Be(1716);
        story.Time.Should().Be(DateTimeOffset.Parse("2019-10-12T13:43:01+00:00"));
    }

    [Fact]
    public async Task GetBestStoriesAsync_OnlyFetchesBestStoryIdsOnce_WhenCalledTwiceWithinCacheWindow()
    {
        _client.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([1]);
        _client.Setup(c => c.GetItemAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Story(1, "Once", 1));
        var sut = CreateSut();

        await sut.GetBestStoriesAsync(1, CancellationToken.None);
        await sut.GetBestStoriesAsync(1, CancellationToken.None);

        _client.Verify(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBestStoriesAsync_OnlyFetchesEachItemOnce_AcrossMultipleCalls()
    {
        _client.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([1]);
        _client.Setup(c => c.GetItemAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Story(1, "Cached", 1));
        var sut = CreateSut();

        await sut.GetBestStoriesAsync(1, CancellationToken.None);
        await sut.GetBestStoriesAsync(1, CancellationToken.None);

        _client.Verify(c => c.GetItemAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetBestStoriesAsync_Throws_WhenCountIsNotPositive(int count)
    {
        var act = () => CreateSut().GetBestStoriesAsync(count, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsEmpty_WhenThereAreNoBestStories()
    {
        _client.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateSut().GetBestStoriesAsync(10, CancellationToken.None);

        result.Should().BeEmpty();
    }
}
