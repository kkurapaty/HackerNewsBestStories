using System.Text.Json.Serialization;

namespace HackerNewsBestStories.Api.Models;

/// <summary>
/// A one to one mapping of the JSON returned by /item/{id}.json.
/// Deliberately kept separate from our public StoryDto, we don't 
/// want to leak Hacker News' field names or unrelated fields 
/// (kids, poll parts, text, etc.) out through our own API.
/// </summary>
public sealed class HackerNewsItem
{
    public int Id { get; init; }

    /// <summary>True if the item was deleted. Comes back on some old items instead of being omitted.</summary>
    public bool Deleted { get; init; }

    /// <summary>"story", "job", "comment", "poll" or "pollopt". We only care about "story".</summary>
    public string? Type { get; init; }

    /// <summary>Username of the submitter. Null for a small number of legacy or removed items.</summary>
    public string? By { get; init; }

    /// <summary>Creation time, unix epoch seconds.</summary>
    public long Time { get; init; }

    /// <summary>The story's URL. Missing for "Ask HN" / text style posts, which is a normal, valid case.</summary>
    public string? Url { get; init; }

    public int Score { get; init; }

    public string? Title { get; init; }

    /// <summary>True if flagged dead by HN moderation. We treat this the same as deleted.</summary>
    public bool Dead { get; init; }

    /// <summary>Comment count. HN calls this "descendants" and it includes nested replies.</summary>
    public int Descendants { get; init; }
}
