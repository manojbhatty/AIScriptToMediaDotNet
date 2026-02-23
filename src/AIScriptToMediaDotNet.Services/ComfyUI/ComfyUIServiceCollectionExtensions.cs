using Microsoft.Extensions.DependencyInjection;

namespace AIScriptToMediaDotNet.Services.ComfyUI;

/// <summary>
/// Extension methods for configuring ComfyUI services.
/// </summary>
public static class ComfyUIServiceCollectionExtensions
{
    /// <summary>
    /// Adds ComfyUI client to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure ComfyUI options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddComfyUI(
        this IServiceCollection services,
        Action<ComfyUIOptions>? configureOptions = null)
    {
        // Configure options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // Configure HttpClient for ComfyUI
        services.AddHttpClient<ComfyUIClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComfyUIOptions>>().Value;
            client.BaseAddress = new Uri(options.Endpoint);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        // Register as singleton (shared HTTP client)
        services.AddScoped<ComfyUIClient>();

        return services;
    }

    /// <summary>
    /// Adds ComfyUI client with options from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configurationSection">Configuration section containing ComfyUI settings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddComfyUI(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfigurationSection configurationSection)
    {
        services.Configure<ComfyUIOptions>(configurationSection);

        services.AddHttpClient<ComfyUIClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComfyUIOptions>>().Value;
            client.BaseAddress = new Uri(options.Endpoint);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddScoped<ComfyUIClient>();

        return services;
    }
}
