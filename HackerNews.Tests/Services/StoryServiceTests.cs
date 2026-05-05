using FluentAssertions;
using HackerNews.Application.Configuration;
using HackerNews.Application.DTOs;
using HackerNews.Application.Interfaces;
using HackerNews.Application.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HackerNews.Tests.Services;

public class StoryServiceTests : IDisposable
{
    private readonly Mock<IHackerNewsClient> _clientMock;
    private readonly IMemoryCache _cache;
    private readonly StoryService _sut;

    public StoryServiceTests()
    {
        _clientMock = new Mock<IHackerNewsClient>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        var settings = Options.Create(new StoryServiceSettings
        {
            StoryIdsCacheTtlMinutes = 5,
            StoryDetailCacheTtlMinutes = 5,
            MaxParallelRequests = 5,
            MaxStoriesLimit = 500
        });

        _sut = new StoryService(
            _clientMock.Object,
            _cache,
            NullLogger<StoryService>.Instance,
            settings);
    }

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsStoriesSortedByScoreDescending()
    {
        // Arrange
        var ids = new[] { 1, 2, 3 };
        _clientMock.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

        SetupStory(1, "Low Score", score: 100);
        SetupStory(2, "High Score", score: 500);
        SetupStory(3, "Mid Score", score: 300);

        // Act
        var result = await _sut.GetBestStoriesAsync(3);

        // Assert
        result.Should().HaveCount(3);
        result[0].Score.Should().Be(500);
        result[1].Score.Should().Be(300);
        result[2].Score.Should().Be(100);
    }

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsOnlyRequestedCount()
    {
        // Arrange
        var ids = new[] { 1, 2, 3, 4, 5 };
        _clientMock.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

        foreach (var id in ids)
            SetupStory(id, $"Story {id}", score: id * 100);

        // Act
        var result = await _sut.GetBestStoriesAsync(2);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBestStoriesAsync_UsesCache_DoesNotCallClientTwice()
    {
        // Arrange
        var ids = new[] { 1 };
        _clientMock.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

        SetupStory(1, "Cached Story", score: 42);

        // Act — call twice
        await _sut.GetBestStoriesAsync(1);
        await _sut.GetBestStoriesAsync(1);

        // Assert — client should only be called once for IDs and once for story detail
        _clientMock.Verify(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _clientMock.Verify(c => c.GetStoryByIdAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBestStoriesAsync_SkipsNullStories_GracefulDegradation()
    {
        // Arrange
        var ids = new[] { 1, 2, 3 };
        _clientMock.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

        SetupStory(1, "Valid", score: 100);
        _clientMock.Setup(c => c.GetStoryByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((HackerNewsItemResponse?)null); // simulates fetch failure
        SetupStory(3, "Also Valid", score: 200);

        // Act
        var result = await _sut.GetBestStoriesAsync(3);

        // Assert — story 2 was skipped, we get 2 results
        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Also Valid");
        result[1].Title.Should().Be("Valid");
    }

    [Fact]
    public async Task GetBestStoriesAsync_EmptyIds_ReturnsEmptyList()
    {
        // Arrange
        _clientMock.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<int>());

        // Act
        var result = await _sut.GetBestStoriesAsync(10);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBestStoriesAsync_MapsFieldsCorrectly()
    {
        // Arrange
        _clientMock.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 42 });

        _clientMock.Setup(c => c.GetStoryByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HackerNewsItemResponse
            {
                Id = 42,
                Title = "Test Title",
                Url = "https://example.com",
                By = "testuser",
                Time = 1570887781, // 2019-10-12T13:43:01+00:00
                Score = 1716,
                Descendants = 572,
                Type = "story"
            });

        // Act
        var result = await _sut.GetBestStoriesAsync(1);

        // Assert
        var story = result.Should().ContainSingle().Subject;
        story.Title.Should().Be("Test Title");
        story.Uri.Should().Be("https://example.com");
        story.PostedBy.Should().Be("testuser");
        story.Score.Should().Be(1716);
        story.CommentCount.Should().Be(572);
        story.Time.Should().Contain("2019-10-12");
    }

    private void SetupStory(int id, string title, int score)
    {
        _clientMock.Setup(c => c.GetStoryByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HackerNewsItemResponse
            {
                Id = id,
                Title = title,
                Url = $"https://example.com/{id}",
                By = "author",
                Time = 1570887781,
                Score = score,
                Descendants = 10,
                Type = "story"
            });
    }

    public void Dispose()
    {
        _cache.Dispose();
    }
}
