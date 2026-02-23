using AIScriptToMediaDotNet.Agents.Scene;
using AIScriptToMediaDotNet.Agents.Photo;
using AIScriptToMediaDotNet.Agents.Video;
using AIScriptToMediaDotNet.Core.Interfaces;
using AIScriptToMediaDotNet.Core.Options;
using AIScriptToMediaDotNet.Core.Orchestration;
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

        // Parse command-line options
        var options = ParseOptions(args);

        if (options.Interactive)
        {
            await RunInteractiveMode(options);
        }
        else
        {
            await RunPipelineMode(options);
        }
    }

    /// <summary>
    /// Parses command-line arguments into options.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Parsed options.</returns>
    private static AppOptions ParseOptions(string[] args)
    {
        var options = new AppOptions();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--title":
                case "-t":
                    if (i + 1 < args.Length)
                    {
                        options.Title = args[++i];
                    }
                    break;

                case "--input":
                case "-i":
                    if (i + 1 < args.Length)
                    {
                        options.InputFile = args[++i];
                    }
                    break;

                case "--script":
                case "-s":
                    if (i + 1 < args.Length)
                    {
                        options.ScriptText = args[++i];
                    }
                    break;

                case "--output":
                case "-o":
                    if (i + 1 < args.Length)
                    {
                        options.OutputPath = args[++i];
                    }
                    break;

                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
            }
        }

        // Determine if we should run in interactive mode
        options.Interactive = string.IsNullOrEmpty(options.InputFile) &&
                              string.IsNullOrEmpty(options.ScriptText);

        return options;
    }

    /// <summary>
    /// Runs the application in interactive mode.
    /// </summary>
    private static async Task RunInteractiveMode(AppOptions options)
    {
        Console.WriteLine("Interactive Mode");
        Console.WriteLine("================");
        Console.WriteLine();

        // Get title
        Console.Write("Enter script title: ");
        var title = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            title = "Untitled Script";
        }
        options.Title = title;

        // Get script
        Console.WriteLine();
        Console.WriteLine("Enter script text (or type 'file:<path>' to load from file):");
        Console.WriteLine("(Type 'END' on a new line when done)");
        Console.WriteLine();

        var scriptLines = new List<string>();
        var inputFile = false;

        while (true)
        {
            var line = Console.ReadLine();
            if (line == null) break;

            if (line.Trim().ToUpper() == "END") break;

            if (line.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                var filePath = line.Substring(5).Trim();
                if (File.Exists(filePath))
                {
                    scriptLines.Add(await File.ReadAllTextAsync(filePath));
                    inputFile = true;
                    break;
                }
                else
                {
                    Console.WriteLine($"File not found: {filePath}");
                }
            }
            else
            {
                scriptLines.Add(line);
            }
        }

        var script = inputFile ? scriptLines[0] : string.Join(Environment.NewLine, scriptLines);

        if (string.IsNullOrWhiteSpace(script))
        {
            Console.WriteLine("No script provided. Exiting.");
            return;
        }

        options.ScriptText = script;
        await RunPipelineMode(options);
    }

    /// <summary>
    /// Runs the pipeline with the provided options.
    /// </summary>
    private static async Task RunPipelineMode(AppOptions options)
    {
        // Build configuration
        var appPath = AppContext.BaseDirectory;
        var configuration = new ConfigurationBuilder()
            .SetBasePath(appPath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Build service collection
        var services = new ServiceCollection();
        ConfigureServices(services, configuration, options);
        var serviceProvider = services.BuildServiceProvider();

        // Get script text
        var script = options.ScriptText;
        if (!string.IsNullOrEmpty(options.InputFile))
        {
            if (!File.Exists(options.InputFile))
            {
                Console.WriteLine($"Error: File not found: {options.InputFile}");
                return;
            }
            script = await File.ReadAllTextAsync(options.InputFile);
        }

        if (string.IsNullOrWhiteSpace(script))
        {
            Console.WriteLine("Error: No script provided.");
            return;
        }

        try
        {
            // Run pipeline
            var pipelineService = serviceProvider.GetRequiredService<ScriptToMediaService>();
            var context = await pipelineService.ProcessScriptAsync(
                options.Title ?? "Untitled",
                script,
                options.OutputPath,
                CancellationToken.None);

            // Print summary
            Console.WriteLine();
            Console.WriteLine("Pipeline Complete!");
            Console.WriteLine("==================");
            Console.WriteLine($"Title: {context.Title}");
            Console.WriteLine($"Scenes: {context.Scenes.Count}");
            Console.WriteLine($"Status: {(context.IsComplete ? "Success" : "Failed")}");

            if (context.Scenes.Any())
            {
                Console.WriteLine();
                Console.WriteLine("Scenes:");
                foreach (var scene in context.Scenes)
                {
                    Console.WriteLine($"  {scene.Id}: {scene.Title} ({scene.Location})");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration, AppOptions options)
    {
        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add Ollama AI provider
        services.AddOllama(opt =>
        {
            configuration.GetSection("Ollama").Bind(opt);
        });

        // Configure agent prompts from settings
        services.Configure<AgentPrompts>(configuration.GetSection("AgentPrompts"));
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentPrompts>>().Value);

        // Add agents
        services.AddScoped<SceneParserAgent>(sp =>
        {
            var aiProvider = sp.GetRequiredService<IAIProvider>();
            var logger = sp.GetRequiredService<ILogger<SceneParserAgent>>();
            var prompts = sp.GetRequiredService<AgentPrompts>();
            return new SceneParserAgent(aiProvider, logger, prompts);
        });

        services.AddScoped<SceneVerifierAgent>(sp =>
        {
            var aiProvider = sp.GetRequiredService<IAIProvider>();
            var logger = sp.GetRequiredService<ILogger<SceneVerifierAgent>>();
            var prompts = sp.GetRequiredService<AgentPrompts>();
            return new SceneVerifierAgent(aiProvider, logger, prompts);
        });

        // Add photo prompt agents
        services.AddScoped<PhotoPromptCreatorAgent>(sp =>
        {
            var aiProvider = sp.GetRequiredService<IAIProvider>();
            var logger = sp.GetRequiredService<ILogger<PhotoPromptCreatorAgent>>();
            var prompts = sp.GetRequiredService<AgentPrompts>();
            return new PhotoPromptCreatorAgent(aiProvider, logger, prompts);
        });

        services.AddScoped<PhotoPromptVerifierAgent>(sp =>
        {
            var aiProvider = sp.GetRequiredService<IAIProvider>();
            var logger = sp.GetRequiredService<ILogger<PhotoPromptVerifierAgent>>();
            var prompts = sp.GetRequiredService<AgentPrompts>();
            return new PhotoPromptVerifierAgent(aiProvider, logger, prompts);
        });

        // Add video prompt agents
        services.AddScoped<VideoPromptCreatorAgent>(sp =>
        {
            var aiProvider = sp.GetRequiredService<IAIProvider>();
            var logger = sp.GetRequiredService<ILogger<VideoPromptCreatorAgent>>();
            var prompts = sp.GetRequiredService<AgentPrompts>();
            return new VideoPromptCreatorAgent(aiProvider, logger, prompts);
        });

        services.AddScoped<VideoPromptVerifierAgent>(sp =>
        {
            var aiProvider = sp.GetRequiredService<IAIProvider>();
            var logger = sp.GetRequiredService<ILogger<VideoPromptVerifierAgent>>();
            var prompts = sp.GetRequiredService<AgentPrompts>();
            return new VideoPromptVerifierAgent(aiProvider, logger, prompts);
        });

        // Add orchestrator
        services.AddSingleton<PipelineOrchestrator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PipelineOrchestrator>>();
            return new PipelineOrchestrator(logger, maxRetriesPerStage: 3);
        });

        // Add pipeline service
        services.AddSingleton<ScriptToMediaService>();

        // Add HTTP client factory
        services.AddHttpClient();
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"AI Script to Media - Usage

Usage: dotnet run [options]

Options:
  --title, -t <title>       Script title (used for output folder naming)
  --input, -i <file>        Path to input script file
  --script, -s <text>       Script text (directly on command line)
  --output, -o <path>       Output directory (default: ./output)
  --help, -h                Show this help message

Examples:

  # Interactive mode (default)
  dotnet run

  # From file
  dotnet run --title ""My Script"" --input script.txt --output ./output

  # From command line
  dotnet run --title ""My Script"" --script ""FADE IN: INT. COFFEE SHOP - DAY...""

  # Short form
  dotnet run -t ""My Script"" -i script.txt -o ./output

Output:
  Creates a folder: {Title}_{YYYY-MM-DD_HH-mm-ss}/
  ├── script.md           - Original script
  ├── scenes.md           - Parsed scenes
  └── agent-log.md        - Execution log
");
    }
}
