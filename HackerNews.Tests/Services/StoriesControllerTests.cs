using FluentAssertions;
using HackerNews.Api.Controllers;
using HackerNews.Application.Configuration;
using HackerNews.Application.DTOs;
using HackerNews.Application.Interfaces;
using HackerNews.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace HackerNews.Tests.Services;

public class StoriesControllerTests
{
    private readonly Mock<IStoryService> _serviceMock;
    private readonly StoriesController _sut;

    public StoriesControllerTests()
    {
        _serviceMock = new Mock<IStoryService>();

        var settings = Options.Create(new StoryServiceSettings
        {
            MaxStoriesLimit = 500
        });

        _sut = new StoriesController(_serviceMock.Object, settings);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(501)]
    [InlineData(1000)]
    public async Task GetBestStories_InvalidN_ReturnsBadRequest(int n)
    {
        // Act
        var result = await _sut.GetBestStories(n);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(400);
    }

    [Fact]
    public async Task GetBestStories_ValidN_ReturnsOkWithStories()
    {
        // Arrange
        var stories = new List<StoryResponse>
        {
            new() { Title = "Story 1", Score = 100, PostedBy = "user1", Time = "2024-01-01T00:00:00+00:00", CommentCount = 5 },
            new() { Title = "Story 2", Score = 50, PostedBy = "user2", Time = "2024-01-02T00:00:00+00:00", CommentCount = 3 }
        };

        _serviceMock.Setup(s => s.GetBestStoriesAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stories);

        // Act
        var result = await _sut.GetBestStories(2);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var returned = ok.Value.Should().BeAssignableTo<IReadOnlyList<StoryResponse>>().Subject;
        returned.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBestStories_ServiceThrowsHttpRequestException_Returns503()
    {
        // Arrange
        _serviceMock.Setup(s => s.GetBestStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("HN API is down"));

        // Act
        var result = await _sut.GetBestStories(10);

        // Assert
        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task GetBestStories_DefaultN_Is10()
    {
        // Arrange
        _serviceMock.Setup(s => s.GetBestStoriesAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StoryResponse>());

        // Act
        var result = await _sut.GetBestStories(); // no parameter = default 10

        // Assert
        _serviceMock.Verify(s => s.GetBestStoriesAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }
}
