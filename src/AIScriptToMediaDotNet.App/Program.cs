using AIScriptToMediaDotNet.Core.Interfaces;
using AIScriptToMediaDotNet.Core.Options;
using AIScriptToMediaDotNet.Core.Prompts;
using AIScriptToMediaDotNet.Providers.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIScriptToMediaDotNet.App;

/// <summary>
/// Main entry point for AI Script to Media application.
/// </summary>
internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("AI Script to Media (.NET)");
        Console.WriteLine("=========================");
        Console.WriteLine();

        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Build service collection
        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Test the AI provider
        await TestAIProvider(serviceProvider, configuration);
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add Ollama AI provider
        services.AddOllama(options =>
        {
            configuration.GetSection("Ollama").Bind(options);
        });

        // Configure agent prompts from settings
        services.Configure<AgentPrompts>(configuration.GetSection("AgentPrompts"));
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentPrompts>>().Value);

        // Add HTTP client factory
        services.AddHttpClient();
    }

    private static async Task TestAIProvider(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        Console.WriteLine("Testing AI Provider...");
        Console.WriteLine();

        var aiProvider = serviceProvider.GetRequiredService<IAIProvider>();

        // Check availability
        Console.Write("Checking Ollama availability... ");
        var isAvailable = await aiProvider.IsAvailableAsync();

        if (!isAvailable)
        {
            Console.WriteLine("❌ Not available");
            Console.WriteLine();
            Console.WriteLine("Make sure Ollama is running:");
            Console.WriteLine("  1. Install from: https://ollama.ai");
            Console.WriteLine("  2. Run: ollama serve");
            Console.WriteLine($"  3. Pull a model: ollama pull {configuration["Ollama:DefaultModel"]}");
            return;
        }

        Console.WriteLine("✓ Available");
        Console.WriteLine();

        // Show configured models per agent
        Console.WriteLine("Configured Models per Agent:");
        Console.WriteLine("---------------------------");
        var agents = new[] {
            "SceneParser", "SceneVerifier",
            "PhotoPromptCreator", "PhotoPromptVerifier",
            "VideoPromptCreator", "VideoPromptVerifier"
        };

        foreach (var agent in agents)
        {
            var model = aiProvider.GetModelForAgent(agent);
            Console.WriteLine($"  {agent,-25} → {model}");
        }
        Console.WriteLine();

        // Test generation
        Console.WriteLine("Generating test response...");
        Console.WriteLine();

        try
        {
            var options = new ModelOptions
            {
                Model = configuration["Ollama:DefaultModel"] ?? "lfm2.5-thinking",
                MaxTokens = 256,
                Temperature = 0.7
            };

            var response = await aiProvider.GenerateResponseAsync(
                "Hello! I am testing the AI Script to Media system. Please respond with a brief greeting.",
                options);

            Console.WriteLine("Response:");
            Console.WriteLine("---------");
            Console.WriteLine(response);
            Console.WriteLine();
            Console.WriteLine("✓ AI Provider is working correctly!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Troubleshooting:");
            Console.WriteLine($"  - Make sure you have a model installed: ollama pull {configuration["Ollama:DefaultModel"]}");
            Console.WriteLine("  - Check Ollama is running: ollama list");
        }
    }
}
