using AiiroX.Core.Interfaces;
using AiiroX.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace AiiroX.Providers.OpenAI;

/// <summary>
/// OpenAI ChatGPT provider implementation.
/// TODO: Obtain your API key from https://platform.openai.com/api-keys
/// </summary>
public sealed class OpenAIChatProvider : IAIChatProvider
{
    public const string ProviderId = "openai";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<OpenAIChatProvider> _logger;

    private const string ApiUrl = "https://api.openai.com/v1/chat/completions";
    private const string Model = "gpt-4o-mini";

    public string Id => ProviderId;
    public string DisplayName => "ChatGPT";

    public bool IsConnected
    {
        get
        {
            var key = _tokenStore.LoadTokenAsync($"{ProviderId}:apikey").GetAwaiter().GetResult();
            return !string.IsNullOrWhiteSpace(key);
        }
    }

    public OpenAIChatProvider(
        IHttpClientFactory httpClientFactory,
        ITokenStore tokenStore,
        ILogger<OpenAIChatProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public async Task<string> SendMessageAsync(
        IReadOnlyList<ChatMessage> conversationHistory,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var apiKey = await _tokenStore.LoadTokenAsync($"{ProviderId}:apikey", cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OpenAI API key is not configured. Please connect the provider.");

        var messages = new JsonArray();
        foreach (var msg in conversationHistory)
        {
            messages.Add(new JsonObject
            {
                ["role"] = msg.Role switch
                {
                    MessageRole.User => "user",
                    MessageRole.Assistant => "assistant",
                    MessageRole.System => "system",
                    _ => "user"
                },
                ["content"] = msg.Content
            });
        }

        var payload = new JsonObject
        {
            ["model"] = Model,
            ["messages"] = messages
        };

        var http = _httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var request = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        _logger.LogDebug("Sending request to OpenAI ({Model}).", Model);

        var response = await http.PostAsync(ApiUrl, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
