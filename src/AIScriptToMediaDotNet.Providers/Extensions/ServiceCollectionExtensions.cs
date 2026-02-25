using AIScriptToMediaDotNet.Core.Interfaces;
using AIScriptToMediaDotNet.Core.Options;
using AIScriptToMediaDotNet.Providers.Ollama;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIScriptToMediaDotNet.Providers.Extensions;

/// <summary>
/// Extension methods for configuring AI providers.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Ollama as the AI provider to the service collection.
    /// </summary>
    public static IServiceCollection AddOllama(
        this IServiceCollection services,
        Action<OllamaOptions>? configureOptions = null)
    {
        // Configure options using Options pattern
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // Register HttpClient factory
        services.AddHttpClient();
        
        // Register OllamaProvider with factory that creates HttpClient with correct timeout
        services.AddScoped<OllamaProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsSnapshot<OllamaOptions>>().Value;
            var logger = serviceProvider.GetRequiredService<ILogger<OllamaProvider>>();
            
            // Create HttpClient with explicit timeout
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(options.Endpoint),
                Timeout = TimeSpan.FromSeconds(Math.Max(options.TimeoutSeconds, 300))
            };
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            
            WriteDebugLog($"[AddOllama #1] Timeout={httpClient.Timeout.TotalSeconds}s, Endpoint={options.Endpoint}");
            
            return new OllamaProvider(httpClient, Microsoft.Extensions.Options.Options.Create(options), logger);
        });

        // Register as IAIProvider
        services.AddScoped<IAIProvider>(sp => sp.GetRequiredService<OllamaProvider>());

        return services;
    }

    /// <summary>
    /// Adds Ollama as the AI provider with options from configuration.
    /// </summary>
    public static IServiceCollection AddOllama(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfigurationSection configurationSection)
    {
        // Bind configuration to options
        services.Configure<OllamaOptions>(configurationSection);

        // Register HttpClient factory
        services.AddHttpClient();
        
        // Register OllamaProvider with factory that creates HttpClient with correct timeout
        services.AddScoped<OllamaProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsSnapshot<OllamaOptions>>().Value;
            var logger = serviceProvider.GetRequiredService<ILogger<OllamaProvider>>();
            
            // Create HttpClient with explicit timeout
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(options.Endpoint),
                Timeout = TimeSpan.FromSeconds(Math.Max(options.TimeoutSeconds, 300))
            };
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            
            WriteDebugLog($"[AddOllama Direct] Timeout={httpClient.Timeout.TotalSeconds}s, Endpoint={options.Endpoint}");
            
            return new OllamaProvider(httpClient, Microsoft.Extensions.Options.Options.Create(options), logger);
        });

        // Register as IAIProvider
        services.AddScoped<IAIProvider>(sp => sp.GetRequiredService<OllamaProvider>());

        return services;
    }

    private static void WriteDebugLog(string message)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "debug-ollama.log");
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            File.AppendAllText(logPath, $"[{timestamp}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
