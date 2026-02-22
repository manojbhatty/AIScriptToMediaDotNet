using AIScriptToMediaDotNet.Core.Interfaces;
using AIScriptToMediaDotNet.Core.Options;
using AIScriptToMediaDotNet.Core.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace AIScriptToMediaDotNet.Core.Extensions;

/// <summary>
/// Extension methods for configuring AI services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Ollama as the AI provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure Ollama options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOllama(
        this IServiceCollection services,
        Action<OllamaOptions>? configureOptions = null)
    {
        // Configure options
        services.Configure<OllamaOptions>(options =>
        {
            configureOptions?.Invoke(options);
        });

        // Configure HttpClient for Ollama
        services.AddHttpClient<OllamaProvider>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<OllamaOptions>();
            client.BaseAddress = new Uri(options.Endpoint);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // Register as IAIProvider
        services.AddScoped<IAIProvider, OllamaProvider>();

        return services;
    }

    /// <summary>
    /// Adds Ollama as the AI provider with options from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configurationSection">Configuration section containing Ollama settings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOllama(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfigurationSection configurationSection)
    {
        services.Configure<OllamaOptions>(configurationSection);

        services.AddHttpClient<OllamaProvider>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<OllamaOptions>();
            client.BaseAddress = new Uri(options.Endpoint);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddScoped<IAIProvider, OllamaProvider>();

        return services;
    }
}
