using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using HackerNewsBestStories.Api.Models;
using HackerNewsBestStories.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HackerNewsBestStories.Tests;

/// <summary>
/// Boots the actual ASP.NET Core pipeline (routing, DI, JSON options, error handler) and
/// swaps out only the network boundary, a real Hacker News client for a fake one. This is
/// what actually proves the wiring in Program.cs works, not just the logic in isolation.
/// </summary>
public class BestStoriesEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient CreateClient(IHackerNewsClient stub) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHackerNewsClient>();
                services.AddSingleton(stub);
            });
        }).CreateClient();

    [Fact]
    public async Task Get_ReturnsTopStories_InDescendingScoreOrder()
    {
        var client = CreateClient(new FakeHackerNewsClient());

        var response = await client.GetAsync("/api/beststories/2");
        var stories = await response.Content.ReadFromJsonAsync<List<StoryDto>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stories.Should().HaveCount(2);
        stories![0].Score.Should().BeGreaterThan(stories[1].Score);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("abc")]
    public async Task Get_ReturnsBadRequest_ForInvalidN(string n)
    {
        var client = CreateClient(new FakeHackerNewsClient());

        var response = await client.GetAsync($"/api/beststories/{n}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed class FakeHackerNewsClient : IHackerNewsClient
    {
        public Task<int[]> GetBestStoryIdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new[] { 1, 2, 3 });

        public Task<HackerNewsItem?> GetItemAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult<HackerNewsItem?>(new HackerNewsItem
            {
                Id = id,
                Title = $"Story {id}",
                Score = id * 10,
                Type = "story",
                By = "tester",
                Time = 1570887781,
                Url = $"https://example.com/item?id={id}"
            });
    }
}
