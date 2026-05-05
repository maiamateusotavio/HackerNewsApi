using HackerNews.Application.Interfaces;
using HackerNews.Application.Services;

namespace HackerNews.Api.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IStoryService, StoryService>();
        return services;
    }
}
