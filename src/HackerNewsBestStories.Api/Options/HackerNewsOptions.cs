namespace HackerNewsBestStories.Api.Options;

/// <summary>
/// Settings for talking to the public Hacker News API. Pulled from the "HackerNewsApi"
/// section of appsettings.json so they can be tuned per environment without a code change.
/// </summary>
public sealed class HackerNewsOptions
{
    public const string SectionName = "HackerNewsApi";

    /// <summary>Root address of the Hacker News Firebase API.</summary>
    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/v0/";

    public string TopStoriesEndpoint { get; set; } = "topstories.json";

    public string BestStoriesEndpoint { get; set; } = "beststories.json";

    public string ItemEndpoint { get; set; } = "item/{0}.json";

    /// <summary>How long we'll wait for a single HTTP call before giving up.</summary>
    public int HttpTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// How long the list of best story ids is kept in memory. This list doesn't change
    /// second to second, so a short cache saves us from hammering the upstream API every
    /// time someone hits our endpoint.
    /// </summary>
    public int BestStoryIdsCacheSeconds { get; set; } = 60;

    /// <summary>How long an individual story's details are cached for.</summary>
    public int ItemCacheSeconds { get; set; } = 120;

    /// <summary>
    /// Caps how many story detail requests we fire at Hacker News at the same time.
    /// Keeps us a good citizen of a free public API instead of opening 200 sockets at once.
    /// </summary>
    public int MaxConcurrentItemFetches { get; set; } = 20;
}
