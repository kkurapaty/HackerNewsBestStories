using System.Collections.Concurrent;

namespace HackerNewsBestStories.Api.Infrastructure;

/// <summary>
/// Hands out one semaphore per key, creating it on first use. Used to make sure that if
/// twenty requests land at once asking for the same uncached story, only one of them
/// actually calls out to Hacker News while the other nineteen wait and then read the
/// cached result. Without this, a burst of traffic on a cold cache would multiply straight
/// through to the upstream API, which is exactly what we're trying to avoid.
/// </summary>
public sealed class KeyedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public SemaphoreSlim Get(string key) => _locks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
}
