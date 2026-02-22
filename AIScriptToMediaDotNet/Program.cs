using AIScriptToMediaDotNet.Core.Extensions;
using AIScriptToMediaDotNet.Core.Interfaces;
using AIScriptToMediaDotNet.Core.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIScriptToMediaDotNet;

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
        await TestAIProvider(serviceProvider);
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

        // Add HTTP client factory
        services.AddHttpClient();
    }

    private static async Task TestAIProvider(IServiceProvider serviceProvider)
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
            Console.WriteLine("  3. Pull a model: ollama pull llama3.1");
            return;
        }

        Console.WriteLine("✓ Available");
        Console.WriteLine();

        // Test generation
        Console.WriteLine("Generating test response...");
        Console.WriteLine();

        try
        {
            var options = new ModelOptions
            {
                Model = "llama3.1",
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
            Console.WriteLine("  - Make sure you have a model installed: ollama pull llama3.1");
            Console.WriteLine("  - Check Ollama is running: ollama list");
        }
    }
}
