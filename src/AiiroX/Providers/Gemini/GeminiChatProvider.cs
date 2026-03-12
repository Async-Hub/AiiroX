using AiiroX.Core.Interfaces;
using AiiroX.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace AiiroX.Providers.Gemini;

/// <summary>
/// Google Gemini provider implementation.
/// TODO: Obtain your API key from https://aistudio.google.com/app/apikey
/// TODO: For Google account OAuth login, register an OAuth 2.0 client in Google Cloud Console.
/// </summary>
public sealed class GeminiChatProvider : IAIChatProvider
{
    public const string ProviderId = "gemini";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<GeminiChatProvider> _logger;

    private const string ApiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

    private volatile bool _isConnected;

    public string Id => ProviderId;
    public string DisplayName => "Gemini";

    /// <summary>Reflects the current auth state without blocking on async token lookup.</summary>
    public bool IsConnected => _isConnected;

    public GeminiChatProvider(
        IHttpClientFactory httpClientFactory,
        ITokenStore tokenStore,
        GeminiAuthProvider authProvider,
        ILogger<GeminiChatProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenStore = tokenStore;
        _logger = logger;

        // Seed the initial state and subscribe to future changes.
        _isConnected = authProvider.ConnectionState == ProviderConnectionState.Connected;
        authProvider.ConnectionStateChanged += (_, state) =>
            _isConnected = state == ProviderConnectionState.Connected;
    }

    public async Task<string> SendMessageAsync(
        IReadOnlyList<ChatMessage> conversationHistory,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var apiKey = await _tokenStore.LoadTokenAsync($"{ProviderId}:apikey", cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Gemini API key is not configured. Please connect the provider.");

        var contents = new JsonArray();
        foreach (var msg in conversationHistory)
        {
            if (msg.Role == MessageRole.System) continue; // Gemini uses systemInstruction separately
            var role = msg.Role == MessageRole.User ? "user" : "model";
            contents.Add(new JsonObject
            {
                ["role"] = role,
                ["parts"] = new JsonArray { new JsonObject { ["text"] = msg.Content } }
            });
        }

        var payload = new JsonObject { ["contents"] = contents };

        // Use the x-goog-api-key header to keep the key out of the URL (logs, server access logs, etc.)
        using var http = _httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);

        var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        _logger.LogDebug("Sending request to Gemini.");

        var response = await http.PostAsync(ApiBaseUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }
}
