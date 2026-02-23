using AIScriptToMediaDotNet.Core.Interfaces;
using AIScriptToMediaDotNet.Core.Options;
using AIScriptToMediaDotNet.Providers.Ollama;
using Microsoft.Extensions.DependencyInjection;

namespace AIScriptToMediaDotNet.Providers.Extensions;

/// <summary>
/// Extension methods for configuring AI providers.
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
        // Configure options using Options pattern
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // Configure HttpClient for Ollama - use IOptionsSnapshot for runtime resolution
        services.AddHttpClient<OllamaProvider>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsSnapshot<OllamaOptions>>().Value;
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
        // Bind configuration to options
        services.Configure<OllamaOptions>(configurationSection);

        // Configure HttpClient for Ollama - use IOptionsSnapshot for runtime resolution
        services.AddHttpClient<OllamaProvider>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsSnapshot<OllamaOptions>>().Value;
            client.BaseAddress = new Uri(options.Endpoint);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // Register as IAIProvider
        services.AddScoped<IAIProvider, OllamaProvider>();

        return services;
    }
}
