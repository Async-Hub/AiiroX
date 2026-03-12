using AiiroX.Core.Interfaces;
using AiiroX.Core.Models;
using AiiroX.Services;
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

namespace AiiroX.Providers.Gemini;

/// <summary>
/// Google Gemini provider using the Generative Language REST API.
/// Authenticates with an OAuth 2.0 Bearer token obtained via <see cref="GoogleOAuthService"/>.
/// </summary>
public sealed class GeminiChatProvider : IAIChatProvider
{
    public const string ProviderId = "gemini";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GoogleOAuthService _oauthService;
    private readonly ILogger<GeminiChatProvider> _logger;

    private const string ApiBaseUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

    private volatile bool _isConnected;

    public string Id => ProviderId;
    public string DisplayName => "Gemini";

    /// <summary>Reflects the current auth state without blocking on async calls.</summary>
    public bool IsConnected => _isConnected;

    public GeminiChatProvider(
        IHttpClientFactory httpClientFactory,
        GoogleOAuthService oauthService,
        GeminiAuthProvider authProvider,
        ILogger<GeminiChatProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _oauthService = oauthService;
        _logger = logger;

        _isConnected = authProvider.ConnectionState == ProviderConnectionState.Connected;
        authProvider.ConnectionStateChanged += (_, state) =>
            _isConnected = state == ProviderConnectionState.Connected;
    }

    public async Task<string> SendMessageAsync(
        IReadOnlyList<ChatMessage> conversationHistory,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await _oauthService.GetValidAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException(
                "Not signed in with Google. Please connect the Gemini provider.");

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

        using var http = _httpClientFactory.CreateClient();
        // Use OAuth 2.0 Bearer token for authentication.
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        _logger.LogDebug("Sending request to Gemini (OAuth).");

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
