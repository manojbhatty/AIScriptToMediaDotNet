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

        // Configure HttpClient for ComfyUI using IOptionsSnapshot for runtime resolution
        services.AddHttpClient<ComfyUIClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsSnapshot<ComfyUIOptions>>().Value;
            var endpoint = options.Endpoint ?? "http://127.0.0.1:8188";
            client.BaseAddress = new Uri(endpoint);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

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

        // Configure HttpClient for ComfyUI
        services.AddHttpClient<ComfyUIClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComfyUIOptions>>().Value;
            var endpoint = options.Endpoint ?? "http://127.0.0.1:8188";
            client.BaseAddress = new Uri(endpoint);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        return services;
    }
}
