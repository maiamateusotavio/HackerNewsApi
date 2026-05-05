using HackerNews.Application.Configuration;
using HackerNews.Application.DTOs;
using HackerNews.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HackerNews.Application.Services;

/// <summary>
/// Orchestrates the retrieval of best stories with two-tier caching:
///   1. Story IDs list → cached for N minutes (avoids hammering the /beststories endpoint).
///   2. Individual story details → cached per ID (avoids re-fetching known stories).
///
/// Concurrency control:
///   - SemaphoreSlim limits parallel HTTP calls to MaxParallelRequests (default 20).
///   - This prevents overwhelming the Hacker News API under heavy caller load.
///   - Cache hits bypass the semaphore entirely (no slot consumed).
///
/// Why not just cache the final sorted result?
///   Because callers request variable values of n. Caching per-story lets us
///   serve n=5 and n=50 from the same cached data without redundant fetches.
/// </summary>
public sealed class StoryService : IStoryService
{
    private const string BestStoryIdsCacheKey = "best-story-ids";

    private readonly IHackerNewsClient _client;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StoryService> _logger;
    private readonly StoryServiceSettings _settings;
    private readonly SemaphoreSlim _semaphore;

    
    public StoryService(
        IHackerNewsClient client,
        IMemoryCache cache,
        ILogger<StoryService> logger,
        IOptions<StoryServiceSettings> settings)
    {
        _client = client;
        _cache = cache;
        _logger = logger;
        _settings = settings.Value;
        _semaphore = new SemaphoreSlim(_settings.MaxParallelRequests, _settings.MaxParallelRequests);
    }

    public async Task<IReadOnlyList<StoryResponse>> GetBestStoriesAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        // 1. Get (possibly cached) list of best story IDs
        var allIds = await GetBestStoryIdsWithCacheAsync(cancellationToken);

        if (allIds.Count == 0)
        {
            _logger.LogWarning("No best story IDs available");
            return Array.Empty<StoryResponse>();
        }

        // 2. Take only the IDs we need (the HN API already returns them roughly ranked)
        //    We still take more than `count` because we re-sort by score.
        //    The HN "beststories" list is score-influenced but not strictly sorted by score.
        var idsToFetch = allIds.Take(Math.Min(count, allIds.Count)).ToList();

        // 3. Fetch story details in parallel with bounded concurrency
        var stories = await FetchStoriesInParallelAsync(idsToFetch, cancellationToken);

        // 4. Sort by score descending and return
        return stories
            .OrderByDescending(s => s.Score)
            .ToList()
            .AsReadOnly();
    }

    private async Task<IReadOnlyList<int>> GetBestStoryIdsWithCacheAsync(
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(BestStoryIdsCacheKey, out IReadOnlyList<int>? cachedIds)
            && cachedIds is not null)
        {
            _logger.LogDebug("Cache HIT for best story IDs ({Count} ids)", cachedIds.Count);
            return cachedIds;
        }

        _logger.LogDebug("Cache MISS for best story IDs — fetching from HN API");
        var ids = await _client.GetBestStoryIdsAsync(cancellationToken);

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow =
                TimeSpan.FromMinutes(_settings.StoryIdsCacheTtlMinutes)
        };

        _cache.Set(BestStoryIdsCacheKey, ids, cacheOptions);
        return ids;
    }

    private async Task<List<StoryResponse>> FetchStoriesInParallelAsync(
        List<int> storyIds,
        CancellationToken cancellationToken)
    {
        var tasks = storyIds.Select(id => FetchSingleStoryWithCacheAsync(id, cancellationToken));
        var results = await Task.WhenAll(tasks);

        return results
            .Where(story => story is not null)
            .Select(story => story!)
            .ToList();
    }

    private async Task<StoryResponse?> FetchSingleStoryWithCacheAsync(
        int storyId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"story-{storyId}";

        // Cache hit → return immediately, no semaphore needed
        if (_cache.TryGetValue(cacheKey, out StoryResponse? cachedStory))
        {
            return cachedStory;
        }

        // Cache miss → acquire semaphore slot, then fetch
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring the semaphore (another thread may have populated it)
            if (_cache.TryGetValue(cacheKey, out cachedStory))
            {
                return cachedStory;
            }

            var item = await _client.GetStoryByIdAsync(storyId, cancellationToken);
            if (item is null)
            {
                return null;
            }

            var story = StoryResponse.FromHackerNewsItem(item);

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromMinutes(_settings.StoryDetailCacheTtlMinutes)
            };

            _cache.Set(cacheKey, story, cacheOptions);
            return story;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}