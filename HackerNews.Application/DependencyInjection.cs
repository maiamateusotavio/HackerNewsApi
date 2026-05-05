namespace HackerNews.Application;

using HackerNews.Application.Configuration;
using HackerNews.Application.Interfaces;
using HackerNews.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<StoryServiceSettings>(
            configuration.GetSection(StoryServiceSettings.SectionName));

        services.AddScoped<IStoryService, StoryService>();

        return services;
    }
}