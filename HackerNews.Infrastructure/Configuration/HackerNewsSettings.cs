namespace HackerNews.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for the Hacker News API integration.
/// Bound from appsettings.json section "HackerNews".
/// </summary>
public sealed class HackerNewsSettings
{
    public const string SectionName = "HackerNews";

    /// <summary>Base URL for the Hacker News Firebase API.</summary>
    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/";

    /// <summary>TTL in minutes for the cached list of best story IDs.</summary>
    public int StoryIdsCacheTtlMinutes { get; set; } = 5;

    /// <summary>TTL in minutes for each cached individual story detail.</summary>
    public int StoryDetailCacheTtlMinutes { get; set; } = 5;

    /// <summary>
    /// Maximum number of concurrent HTTP requests to the HN API.
    /// Prevents overwhelming the external service under heavy load.
    /// </summary>
    public int MaxParallelRequests { get; set; } = 20;

    /// <summary>HTTP request timeout in seconds for the typed HttpClient.</summary>
    public int HttpTimeoutSeconds { get; set; } = 10;

    /// <summary>Maximum value of n that a caller can request.</summary>
    public int MaxStoriesLimit { get; set; } = 500;
}