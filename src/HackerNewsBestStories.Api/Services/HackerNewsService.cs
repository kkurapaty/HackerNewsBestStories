using HackerNewsBestStories.Api.Infrastructure;
using HackerNewsBestStories.Api.Models;
using HackerNewsBestStories.Api.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HackerNewsBestStories.Api.Services;

public sealed class HackerNewsService(
    IHackerNewsClient client,
    IMemoryCache cache,
    KeyedLock locks,
    IOptions<HackerNewsOptions> options) : IHackerNewsService
{
    private const string BestIdsCacheKey = "hn:best-story-ids";
    private readonly HackerNewsOptions _options = options.Value;

    public async Task<IReadOnlyList<StoryDto>> GetBestStoriesAsync(int count, CancellationToken cancellationToken)
    {
        if (count < 1)
        {
            // Defensive check even though the controller already validates this. A service
            // should never trust that it's only ever called from one place.
            throw new ArgumentOutOfRangeException(nameof(count), "count must be at least 1.");
        }

        var ids = await GetBestStoryIdsAsync(cancellationToken);

        // We fetch every candidate id's details because the beststories endpoint doesn't
        // guarantee it's sorted by score, only that these are the current best stories.
        // Each fetch is individually cached, so repeat calls (very common, since the same
        // handful of ids stay "best" for minutes at a time) cost nothing after the first hit.
        var items = await FetchItemsWithBoundedConcurrencyAsync(ids, cancellationToken);

        return items
            .Where(IsPublishableStory)
            .Select(item => item!) // IsPublishableStory guarantees non-null, this just tells the compiler
            .OrderByDescending(item => item.Score)
            .Take(count)
            .Select(ToDto)
            .ToList();
    }

    private async Task<int[]> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(BestIdsCacheKey, out int[]? cached) && cached is not null)
        {
            return cached;
        }

        var gate = locks.Get(BestIdsCacheKey);
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Re-check now that we hold the lock. Whoever got here first has already
            // populated the cache while everyone else was waiting.
            if (cache.TryGetValue(BestIdsCacheKey, out cached) && cached is not null)
            {
                return cached;
            }

            var ids = await client.GetBestStoryIdsAsync(cancellationToken);
            cache.Set(BestIdsCacheKey, ids, TimeSpan.FromSeconds(_options.BestStoryIdsCacheSeconds));
            return ids;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<HackerNewsItem?[]> FetchItemsWithBoundedConcurrencyAsync(int[] ids, CancellationToken cancellationToken)
    {
        using var throttle = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrentItemFetches));

        var tasks = ids.Select(async id =>
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                return await GetItemAsync(id, cancellationToken);
            }
            finally
            {
                throttle.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    private async Task<HackerNewsItem?> GetItemAsync(int id, CancellationToken cancellationToken)
    {
        var cacheKey = $"hn:item:{id}";
        if (cache.TryGetValue(cacheKey, out HackerNewsItem? cached))
        {
            return cached;
        }

        var gate = locks.Get(cacheKey);
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            var item = await client.GetItemAsync(id, cancellationToken);

            // We cache the miss too (a short lived null), so a deleted or bad id doesn't
            // get retried on every single request while it's still sitting in beststories.
            cache.Set(cacheKey, item, TimeSpan.FromSeconds(_options.ItemCacheSeconds));
            return item;
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool IsPublishableStory(HackerNewsItem? item) =>
        item is { Deleted: false, Dead: false } &&
        string.Equals(item.Type, "story", StringComparison.OrdinalIgnoreCase);

    private static StoryDto ToDto(HackerNewsItem item) => new(
        Title: item.Title ?? string.Empty,
        // "Ask HN" and similar text posts have no url of their own, so we fall back to the
        // HN discussion page rather than returning an empty, useless link.
        Uri: item.Url ?? $"https://example.com/item?id={item.Id}",
        PostedBy: item.By ?? string.Empty,
        Time: DateTimeOffset.FromUnixTimeSeconds(item.Time),
        Score: item.Score,
        CommentCount: item.Descendants);
}
