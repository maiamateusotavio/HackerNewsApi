using HackerNews.Application.Interfaces;
using HackerNews.Infrastructure.Clients;
using HackerNews.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace HackerNews.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers all Infrastructure services:
    /// - HackerNewsSettings (Options pattern)
    /// - HackerNewsClient (Typed HttpClient + Polly policies)
    /// - IMemoryCache
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Bind settings
        var settings = new HackerNewsSettings();
        configuration.GetSection(HackerNewsSettings.SectionName).Bind(settings);
        services.Configure<HackerNewsSettings>(
            configuration.GetSection(HackerNewsSettings.SectionName));

        // 2. Register IMemoryCache
        services.AddMemoryCache();

        // 3. Register typed HttpClient with Polly resilience policies
        services.AddHttpClient<IHackerNewsClient, HackerNewsClient>(client =>
        {
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.HttpTimeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        return services;
    }

    /// <summary>
    /// Retry policy: 3 attempts with exponential backoff + jitter.
    /// Jitter prevents the "thundering herd" problem when the HN API recovers.
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, attempt))
                    + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500)),
                onRetry: (outcome, delay, attempt, _) =>
                {
                    // In production, inject ILogger here via a policy registry.
                    // Kept simple for the coding test scope.
                });
    }

    /// <summary>
    /// Circuit breaker: opens after 5 consecutive failures, stays open for 30s.
    /// Prevents cascading failures when the HN API is fully down.
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }
}