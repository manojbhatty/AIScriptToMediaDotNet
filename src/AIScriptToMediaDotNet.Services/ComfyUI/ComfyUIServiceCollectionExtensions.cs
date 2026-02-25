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

        // Register workflow template provider
        services.AddSingleton<IWorkflowTemplateProvider>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsSnapshot<ComfyUIOptions>>().Value;
            return new FileWorkflowTemplateProvider(
                options.WorkflowPath,
                options.NodeMapping,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileWorkflowTemplateProvider>>());
        });

        // Register workflow builder
        services.AddScoped<ComfyUIWorkflowBuilder>();

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

        // Register workflow template provider
        services.AddSingleton<IWorkflowTemplateProvider>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComfyUIOptions>>().Value;
            return new FileWorkflowTemplateProvider(
                options.WorkflowPath,
                options.NodeMapping,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileWorkflowTemplateProvider>>());
        });

        // Register workflow builder
        services.AddScoped<ComfyUIWorkflowBuilder>();

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

    /// <summary>
    /// Adds ComfyUI client with a custom workflow template provider.
    /// This allows using custom workflow sources (e.g., database, remote API).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure ComfyUI options.</param>
    /// <param name="templateProviderFactory">Factory to create the workflow template provider.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddComfyUI(
        this IServiceCollection services,
        Action<ComfyUIOptions>? configureOptions,
        Func<IServiceProvider, IWorkflowTemplateProvider> templateProviderFactory)
    {
        // Configure options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // Register custom workflow template provider
        services.AddSingleton<IWorkflowTemplateProvider>(sp => templateProviderFactory(sp));

        // Register workflow builder
        services.AddScoped<ComfyUIWorkflowBuilder>();

        // Configure HttpClient for ComfyUI
        services.AddHttpClient<ComfyUIClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsSnapshot<ComfyUIOptions>>().Value;
            var endpoint = options.Endpoint ?? "http://127.0.0.1:8188";
            client.BaseAddress = new Uri(endpoint);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        return services;
    }
}
