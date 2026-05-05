using HackerNews.Application.DTOs;

namespace HackerNews.Application.Interfaces;

/// <summary>
/// Abstracts HTTP communication with the Hacker News Firebase API.
/// This interface sits in Application so the layer has zero knowledge of HttpClient.
/// Infrastructure provides the concrete implementation.
/// </summary>
public interface IHackerNewsClient
{
    /// <summary>
    /// Retrieves the full list of best story IDs from the HN API.
    /// </summary>
    Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the detail of a single story by its ID.
    /// Returns null if the item cannot be fetched (404, transient failure after retries, etc.).
    /// </summary>
    Task<HackerNewsItemResponse?> GetStoryByIdAsync(int storyId, CancellationToken cancellationToken = default);
}