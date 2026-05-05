namespace HackerNews.Application.Configuration;

public sealed class StoryServiceSettings
{
    public const string SectionName = "HackerNews";

    public int StoryIdsCacheTtlMinutes { get; set; } = 5;
    public int StoryDetailCacheTtlMinutes { get; set; } = 5;
    public int MaxParallelRequests { get; set; } = 20;
    public int MaxStoriesLimit { get; set; } = 500;
}