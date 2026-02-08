using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeLab.Cli.Models.AI;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Configuration;

namespace HomeLab.Cli.Services.AI;

/// <summary>
/// LLM service implementation using Anthropic Claude API.
/// Uses raw HTTP calls — no SDK dependency.
/// </summary>
public class AnthropicLlmService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";

    public string ProviderName => "Anthropic";

    public AnthropicLlmService(IHomelabConfigService configService, HttpClient httpClient)
    {
        _httpClient = httpClient;
        var config = configService.GetServiceConfig("ai");
        _apiKey = config.Token ?? string.Empty;
        _model = config.Model ?? "claude-haiku-4-5-20251001";
    }

    public Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(_apiKey));
    }

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    public async Task<LlmResponse> SendMessageAsync(string systemPrompt, string userMessage, int maxTokens = 1024)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return new LlmResponse
            {
                Success = false,
                Error = "API key not configured. Add services.ai.token to ~/.config/homelab/homelab-cli.yaml"
            };
        }

        try
        {
            var request = new AnthropicRequest
            {
                Model = _model,
                MaxTokens = maxTokens,
                System = systemPrompt,
                Messages = new List<AnthropicMessage>
                {
                    new() { Role = "user", Content = userMessage }
                }
            };

            var json = JsonSerializer.Serialize(request);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            // Per-request headers (shared HttpClient — don't use DefaultRequestHeaders)
            httpRequest.Headers.Add("x-api-key", _apiKey);
            httpRequest.Headers.Add("anthropic-version", ApiVersion);

            using var cts = new CancellationTokenSource(RequestTimeout);
            var response = await _httpClient.SendAsync(httpRequest, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
                return new LlmResponse
                {
                    Success = false,
                    Error = $"API error {(int)response.StatusCode}: {errorBody}"
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
            var anthropicResponse = JsonSerializer.Deserialize<AnthropicResponse>(responseBody);

            if (anthropicResponse?.Content == null || anthropicResponse.Content.Count == 0)
            {
                return new LlmResponse
                {
                    Success = false,
                    Error = "Empty response from API"
                };
            }

            return new LlmResponse
            {
                Success = true,
                Content = anthropicResponse.Content[0].Text,
                InputTokens = anthropicResponse.Usage?.InputTokens ?? 0,
                OutputTokens = anthropicResponse.Usage?.OutputTokens ?? 0,
                Model = anthropicResponse.Model ?? _model
            };
        }
        catch (OperationCanceledException)
        {
            return new LlmResponse
            {
                Success = false,
                Error = "Request timed out after 30 seconds"
            };
        }
        catch (Exception ex)
        {
            return new LlmResponse
            {
                Success = false,
                Error = $"Request failed: {ex.Message}"
            };
        }
    }

    // Anthropic API request/response models

    private class AnthropicRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }
        [JsonPropertyName("system")] public string System { get; set; } = string.Empty;
        [JsonPropertyName("messages")] public List<AnthropicMessage> Messages { get; set; } = new();
    }

    private class AnthropicMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    }

    private class AnthropicResponse
    {
        [JsonPropertyName("content")] public List<ContentBlock>? Content { get; set; }
        [JsonPropertyName("usage")] public UsageInfo? Usage { get; set; }
        [JsonPropertyName("model")] public string? Model { get; set; }
    }

    private class ContentBlock
    {
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    }

    private class UsageInfo
    {
        [JsonPropertyName("input_tokens")] public int InputTokens { get; set; }
        [JsonPropertyName("output_tokens")] public int OutputTokens { get; set; }
    }
}
