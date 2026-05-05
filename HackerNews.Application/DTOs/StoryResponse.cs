using System.Text.Json.Serialization;

namespace HackerNews.Application.DTOs;

/// <summary>
/// The public contract returned by our API.
/// Matches the exact shape specified in the coding challenge.
/// </summary>
public sealed record StoryResponse
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("uri")]
    public string? Uri { get; init; }

    [JsonPropertyName("postedBy")]
    public string PostedBy { get; init; } = string.Empty;

    [JsonPropertyName("time")]
    public string Time { get; init; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; init; }

    [JsonPropertyName("commentCount")]
    public int CommentCount { get; init; }

    /// <summary>
    /// Factory method: transforms the raw HN API item into our public DTO.
    /// Unix epoch → ISO 8601 with UTC offset as required by the spec.
    /// </summary>
    public static StoryResponse FromHackerNewsItem(HackerNewsItemResponse item)
    {
        var dateTime = DateTimeOffset.FromUnixTimeSeconds(item.Time);

        return new StoryResponse
        {
            Title = item.Title,
            Uri = item.Url,
            PostedBy = item.By,
            Time = dateTime.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            Score = item.Score,
            CommentCount = item.Descendants
        };
    }
}