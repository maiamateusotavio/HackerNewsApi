using System.Text.Json.Serialization;

namespace HackerNews.Application.DTOs;

/// <summary>
/// Maps directly to the Hacker News Firebase API item response.
/// Only the fields we need are mapped; the rest are ignored by the deserializer.
/// </summary>
public sealed record HackerNewsItemResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("by")]
    public string By { get; init; } = string.Empty;

    [JsonPropertyName("time")]
    public long Time { get; init; }

    [JsonPropertyName("score")]
    public int Score { get; init; }

    [JsonPropertyName("descendants")]
    public int Descendants { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
}