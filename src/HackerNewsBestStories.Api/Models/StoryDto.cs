using System.Text.Json.Serialization;
using HackerNewsBestStories.Api.Json;

namespace HackerNewsBestStories.Api.Models;
/// <summary>
/// The shape we promise to callers of our API. Field names and casing match the exercise
/// spec exactly, regardless of what Hacker News itself calls things internally.
/// </summary>
public sealed record StoryDto(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("postedBy")] string PostedBy,
    [property: JsonPropertyName("time")]
    [property: JsonConverter(typeof(RoundedDateTimeOffsetConverter))]
    DateTimeOffset Time,
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("commentCount")] int CommentCount);
