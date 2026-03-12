using AiiroX.Core.Interfaces;
using AiiroX.Core.Models;
using AiiroX.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiiroX.Providers.Gemini;

/// <summary>
/// Google OAuth 2.0 authentication for the Gemini provider.
/// Uses the installed-application flow: opens the system browser and listens on localhost
/// for the OAuth callback. The refresh token is persisted via <see cref="GoogleOAuthService"/>
/// so the session is automatically restored on the next app launch.
/// </summary>
public sealed class GeminiAuthProvider : IAIAuthProvider
{
    private readonly GoogleOAuthService _oauthService;
    private readonly ILogger<GeminiAuthProvider> _logger;

    private ProviderConnectionState _state = ProviderConnectionState.Disconnected;

    public string ProviderId => GeminiChatProvider.ProviderId;

    /// <inheritdoc/>
    public bool UsesOAuthFlow => true;

    public ProviderConnectionState ConnectionState => _state;
    public event EventHandler<ProviderConnectionState>? ConnectionStateChanged;

    public GeminiAuthProvider(GoogleOAuthService oauthService, ILogger<GeminiAuthProvider> logger)
    {
        _oauthService = oauthService;
        _logger = logger;
    }

    /// <summary>
    /// Triggers the Google OAuth browser sign-in flow.
    /// The <paramref name="credential"/> parameter is not used for OAuth providers.
    /// </summary>
    public async Task<bool> ConnectAsync(string credential, CancellationToken cancellationToken = default)
    {
        SetState(ProviderConnectionState.Connecting);
        try
        {
            var success = await _oauthService.AuthorizeAsync(cancellationToken);
            SetState(success ? ProviderConnectionState.Connected : ProviderConnectionState.Disconnected);
            if (success) _logger.LogInformation("Gemini provider connected via Google OAuth.");
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini Google OAuth connect failed.");
            SetState(ProviderConnectionState.Error);
            return false;
        }
    }

    /// <summary>Revokes the Google OAuth token and signs out.</summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _oauthService.RevokeAsync(cancellationToken);
        SetState(ProviderConnectionState.Disconnected);
        _logger.LogInformation("Gemini provider disconnected (Google OAuth revoked).");
    }

    /// <summary>
    /// Attempts to restore the previous Google OAuth session silently (no browser prompt).
    /// </summary>
    public async Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        var restored = await _oauthService.TryRestoreAsync(cancellationToken);
        SetState(restored ? ProviderConnectionState.Connected : ProviderConnectionState.Disconnected);
        return restored;
    }

    private void SetState(ProviderConnectionState state)
    {
        _state = state;
        ConnectionStateChanged?.Invoke(this, state);
    }
}
