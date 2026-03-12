using AiiroX.Core.Interfaces;
using AiiroX.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiiroX.Providers.Gemini;

/// <summary>
/// API-key-based authentication for Google Gemini.
/// TODO: For full Google OAuth login flow, integrate Google.Apis.Auth and implement PKCE OAuth flow.
/// </summary>
public sealed class GeminiAuthProvider : IAIAuthProvider
{
    private readonly ITokenStore _tokenStore;
    private readonly IProviderSessionStore _sessionStore;
    private readonly ILogger<GeminiAuthProvider> _logger;

    private ProviderConnectionState _state = ProviderConnectionState.Disconnected;

    public string ProviderId => GeminiChatProvider.ProviderId;
    public ProviderConnectionState ConnectionState => _state;
    public event EventHandler<ProviderConnectionState>? ConnectionStateChanged;

    public GeminiAuthProvider(
        ITokenStore tokenStore,
        IProviderSessionStore sessionStore,
        ILogger<GeminiAuthProvider> logger)
    {
        _tokenStore = tokenStore;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(string credential, CancellationToken cancellationToken = default)
    {
        SetState(ProviderConnectionState.Connecting);
        try
        {
            if (string.IsNullOrWhiteSpace(credential))
            {
                SetState(ProviderConnectionState.Error);
                return false;
            }

            await _tokenStore.SaveTokenAsync($"{ProviderId}:apikey", credential.Trim(), cancellationToken);
            await _sessionStore.SaveSessionAsync(ProviderId, true, cancellationToken);
            SetState(ProviderConnectionState.Connected);
            _logger.LogInformation("Gemini provider connected.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect Gemini provider.");
            SetState(ProviderConnectionState.Error);
            return false;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _tokenStore.DeleteTokenAsync($"{ProviderId}:apikey", cancellationToken);
        await _sessionStore.ClearSessionAsync(ProviderId, cancellationToken);
        SetState(ProviderConnectionState.Disconnected);
        _logger.LogInformation("Gemini provider disconnected.");
    }

    public async Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        var wasConnected = await _sessionStore.LoadSessionAsync(ProviderId, cancellationToken);
        if (!wasConnected)
        {
            SetState(ProviderConnectionState.Disconnected);
            return false;
        }

        var key = await _tokenStore.LoadTokenAsync($"{ProviderId}:apikey", cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
        {
            SetState(ProviderConnectionState.Disconnected);
            return false;
        }

        SetState(ProviderConnectionState.Connected);
        return true;
    }

    private void SetState(ProviderConnectionState state)
    {
        _state = state;
        ConnectionStateChanged?.Invoke(this, state);
    }
}
