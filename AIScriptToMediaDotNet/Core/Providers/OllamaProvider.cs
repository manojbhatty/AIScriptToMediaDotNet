using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AIScriptToMediaDotNet.Core.Interfaces;
using AIScriptToMediaDotNet.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIScriptToMediaDotNet.Core.Providers;

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
        IOptions<OllamaOptions> options,
        ILogger<OllamaProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                _logger.LogDebug(
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

                var response = await _httpClient.PostAsJsonAsync(
                    $"{_options.Endpoint}/api/generate",
                    request,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseContent);

                if (ollamaResponse?.Response != null)
                {
                    _logger.LogDebug("Successfully generated response ({Length} chars)",
                        ollamaResponse.Response.Length);
                    return ollamaResponse.Response;
                }

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
        public string Model { get; set; } = "llama3.1";
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
        public string Model { get; set; } = "";
        public string Response { get; set; } = "";
        public bool Done { get; set; }
    }

    #endregion
}
