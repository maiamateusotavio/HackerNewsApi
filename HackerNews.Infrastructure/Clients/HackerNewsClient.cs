using System.Net.Http.Json;
using HackerNews.Application.DTOs;
using HackerNews.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace HackerNews.Infrastructure.Clients;

/// <summary>
/// Typed HttpClient that communicates with the Hacker News Firebase API.
/// 
/// Design decisions:
/// - Uses System.Net.Http.Json for zero-allocation deserialization where possible.
/// - Returns null on failure instead of throwing — the service layer decides how to handle gaps.
/// - Logging is structured (Serilog-friendly) with story IDs for traceability.
/// - Retry/Circuit Breaker are handled externally via Polly (configured in DI registration).
/// </summary>
public sealed class HackerNewsClient : IHackerNewsClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HackerNewsClient> _logger;

    public HackerNewsClient(HttpClient httpClient, ILogger<HackerNewsClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching best story IDs from Hacker News API");

            var ids = await _httpClient.GetFromJsonAsync<int[]>(
                "v0/beststories.json",
                cancellationToken);

            if (ids is null || ids.Length == 0)
            {
                _logger.LogWarning("Hacker News API returned empty or null best stories list");
                return Array.Empty<int>();
            }

            _logger.LogDebug("Retrieved {Count} best story IDs", ids.Length);
            return ids;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to fetch best story IDs from Hacker News API");
            throw;
        }
    }

    public async Task<HackerNewsItemResponse?> GetStoryByIdAsync(int storyId, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _httpClient.GetFromJsonAsync<HackerNewsItemResponse>(
                $"v0/item/{storyId}.json",
                cancellationToken);

            if (item is null)
            {
                _logger.LogWarning("Hacker News API returned null for story {StoryId}", storyId);
            }

            return item;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to fetch story {StoryId} — will be skipped", storyId);
            return null;
        }
    }
}