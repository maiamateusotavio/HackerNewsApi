namespace HackerNews.Infrastructure.Configuration;

public sealed class HackerNewsClientSettings
{
    public const string SectionName = "HackerNews";

    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/";
    public int HttpTimeoutSeconds { get; set; } = 10;
}