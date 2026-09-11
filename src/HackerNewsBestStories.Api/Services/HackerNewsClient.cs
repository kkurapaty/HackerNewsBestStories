using Microsoft.Extensions.Options;
using HackerNewsBestStories.Api.Models;
using HackerNewsBestStories.Api.Options;

namespace HackerNewsBestStories.Api.Services;

public sealed class HackerNewsClient(
    HttpClient httpClient,
    IOptions<HackerNewsOptions> options,
    ILogger<HackerNewsClient> logger) : IHackerNewsClient
{
    private readonly HackerNewsOptions _options = options.Value;
    public async Task<int[]> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        var ids = await httpClient.GetFromJsonAsync<int[]>(_options.BestStoriesEndpoint, cancellationToken);
        return ids ?? [];
    }

    public async Task<HackerNewsItem?> GetItemAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<HackerNewsItem>(string.Format(_options.ItemEndpoint, id), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // A single missing or slow story shouldn't take down the whole response.
            // We log it and let the caller treat this id as unavailable.
            logger.LogWarning(ex, "Could not fetch Hacker News item {ItemId}, skipping it", id);
            return null;
        }
    }
}
