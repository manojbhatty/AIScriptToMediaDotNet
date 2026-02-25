using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AIScriptToMediaDotNet.Core.Interfaces;
using AIScriptToMediaDotNet.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIScriptToMediaDotNet.Providers.Ollama;

/// <summary>
/// Provides AI inference via Ollama.
/// </summary>
public class OllamaProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaProvider> _logger;

    /// <inheritdoc />
    public string Name => "Ollama";

    /// <summary>
    /// Initializes a new instance of the OllamaProvider class.
    /// </summary>
    public OllamaProvider(
        HttpClient httpClient,
        Microsoft.Extensions.Options.IOptions<OllamaOptions> options,
        ILogger<OllamaProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Log timeout configuration
        var timeout = _httpClient.Timeout;
        WriteDebugLog($"[OllamaProvider] HttpClient.Timeout={timeout.TotalSeconds}s, Options.TimeoutSeconds={_options.TimeoutSeconds}, Endpoint={_options.Endpoint}");
        _logger.LogInformation("OllamaProvider initialized: Timeout={Timeout}s, Endpoint={Endpoint}", timeout.TotalSeconds, _options.Endpoint);
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

    /// <inheritdoc />
    public string GetModelForAgent(string agentName)
    {
        return _options.AgentModels.GetModelForAgent(agentName);
    }

    /// <inheritdoc />
    public async Task<string> GenerateResponseAsync(
        string prompt,
        ModelOptions modelOptions,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt cannot be empty", nameof(prompt));
        }

        var model = modelOptions?.Model ?? _options.DefaultModel;
        var maxRetries = _options.MaxRetries;
        var retryDelay = TimeSpan.FromSeconds(_options.RetryDelaySeconds);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "Generating response with model '{Model}' (attempt {Attempt}/{MaxRetries})",
                    model, attempt, maxRetries);

                var request = new OllamaRequest
                {
                    Model = model,
                    Prompt = prompt,
                    Stream = false,
                    Options = new OllamaGenerationOptions
                    {
                        NumPredict = modelOptions?.MaxTokens ?? _options.TimeoutSeconds,
                        Temperature = modelOptions?.Temperature ?? 0.7,
                        TopP = modelOptions?.TopP ?? 0.9,
                        Seed = modelOptions?.Seed ?? -1
                    }
                };

                _logger.LogDebug("Sending request to {Url} with model {Model}", "api/generate", model);

                var response = await _httpClient.PostAsJsonAsync(
                    $"{_options.Endpoint}/api/generate",
                    request,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                
                _logger.LogDebug("Raw Ollama response: {Response}", responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent);
                
                var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseContent);

                if (ollamaResponse?.Response != null)
                {
                    _logger.LogInformation("Successfully generated response ({Length} chars)",
                        ollamaResponse.Response.Length);
                    
                    // Log first 200 chars for debugging
                    var previewLength = Math.Min(200, ollamaResponse.Response.Length);
                    _logger.LogDebug("Response preview: {Preview}", ollamaResponse.Response.Substring(0, previewLength));
                    
                    return ollamaResponse.Response;
                }

                _logger.LogWarning("Empty response from Ollama. Full response: {Response}", responseContent);
                throw new InvalidOperationException("Empty response from Ollama");
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(
                    ex,
                    "Request failed (attempt {Attempt}/{MaxRetries}), retrying in {Delay}s...",
                    attempt, maxRetries, retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Failed to generate response after {maxRetries} attempts");
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_options.Endpoint}/api/tags",
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama availability check failed");
            return false;
        }
    }

    #region Request/Response Models

    private class OllamaRequest
    {
        public string Model { get; set; } = "";
        public string Prompt { get; set; } = "";
        public bool Stream { get; set; }
        public OllamaGenerationOptions? Options { get; set; }
    }

    private class OllamaGenerationOptions
    {
        public int NumPredict { get; set; }
        public double Temperature { get; set; }
        public double TopP { get; set; }
        public int Seed { get; set; }
    }

    private class OllamaResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("model")]
        public string Model { get; set; } = "";
        
        [System.Text.Json.Serialization.JsonPropertyName("response")]
        public string Response { get; set; } = "";
        
        [System.Text.Json.Serialization.JsonPropertyName("done")]
        public bool Done { get; set; }
    }

    #endregion
}
