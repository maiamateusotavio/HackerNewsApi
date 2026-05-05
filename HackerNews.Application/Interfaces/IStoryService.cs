using HackerNews.Application.DTOs;

namespace HackerNews.Application.Interfaces;

/// <summary>
/// Business logic for retrieving and ranking Hacker News stories.
/// Owns the caching strategy, parallel fetch orchestration, and sort logic.
/// </summary>
public interface IStoryService
{
    /// <summary>
    /// Returns the top <paramref name="count"/> best stories ordered by score descending.
    /// </summary>
    /// <param name="count">Number of stories to return. Must be between 1 and 500.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<StoryResponse>> GetBestStoriesAsync(int count, CancellationToken cancellationToken = default);
}