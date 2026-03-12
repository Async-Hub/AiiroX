using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiiroX.Services;

/// <summary>
/// Manages Google OAuth 2.0 authentication using the installed-application flow.
/// Opens the system browser for the consent screen and listens on localhost for the callback.
/// The refresh token is persisted via <see cref="IDataStore"/> so the session survives restarts.
/// </summary>
/// <remarks>
/// Security note: The <see cref="IDataStore"/> implementation (<see cref="Google.Apis.Util.Store.FileDataStore"/>
/// by default) stores the OAuth refresh token as a plain JSON file. A refresh token grants
/// long-term access to the user's Google account. For production deployments, replace
/// <see cref="IDataStore"/> with an implementation backed by the OS keychain (Windows
/// Credential Manager, macOS Keychain, or Linux Secret Service).
/// </remarks>
public sealed class GoogleOAuthService
{
    /// <summary>Required scope for calling the Gemini Generative Language API.</summary>
    private static readonly string[] Scopes =
    [
        "https://www.googleapis.com/auth/generative-language"
    ];

    private readonly GoogleOAuthOptions _options;
    private readonly IDataStore _dataStore;
    private readonly ILogger<GoogleOAuthService> _logger;

    private UserCredential? _credential;

    public GoogleOAuthService(
        GoogleOAuthOptions options,
        IDataStore dataStore,
        ILogger<GoogleOAuthService> logger)
    {
        _options = options;
        _dataStore = dataStore;
        _logger = logger;
    }

    /// <summary>
    /// Launches the Google sign-in flow in the default browser.
    /// If valid tokens are already stored in <see cref="IDataStore"/>, returns immediately
    /// without opening the browser.
    /// </summary>
    /// <returns><c>true</c> when a valid credential is obtained; <c>false</c> on cancellation or failure.</returns>
    public async Task<bool> AuthorizeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var flow = CreateFlow();
            var receiver = new LocalServerCodeReceiver();
            var app = new AuthorizationCodeInstalledApp(flow, receiver);
            _credential = await app.AuthorizeAsync("user", cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Google OAuth authorization succeeded.");
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Google OAuth authorization was cancelled by the user.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google OAuth authorization failed.");
            return false;
        }
    }

    /// <summary>
    /// Attempts to restore a previous session from the persisted token store.
    /// Does NOT open the browser; returns <c>false</c> when no valid stored session exists.
    /// </summary>
    public async Task<bool> TryRestoreAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await _dataStore.GetAsync<TokenResponse>("user").ConfigureAwait(false);
            if (string.IsNullOrEmpty(token?.RefreshToken))
            {
                _logger.LogDebug("No stored Google refresh token found; skipping session restore.");
                return false;
            }

            var flow = CreateFlow();
            _credential = new UserCredential(flow, "user", token);

            // Refresh to confirm the stored token is still valid with Google.
            await _credential.RefreshTokenAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Google OAuth session restored successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not restore Google OAuth session; user will need to sign in again.");
            _credential = null;
            return false;
        }
    }

    /// <summary>
    /// Returns a valid access token, automatically refreshing it if it has expired.
    /// Returns <c>null</c> if the user is not authenticated.
    /// </summary>
    public async Task<string?> GetValidAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_credential is null)
            return null;

        if (_credential.Token.IsStale)
        {
            _logger.LogDebug("Google access token is stale; refreshing automatically.");
            await _credential.RefreshTokenAsync(cancellationToken).ConfigureAwait(false);
        }

        return _credential.Token.AccessToken;
    }

    /// <summary>Revokes the current OAuth token and removes it from local storage.</summary>
    public async Task RevokeAsync(CancellationToken cancellationToken = default)
    {
        if (_credential is not null)
        {
            try
            {
                await _credential.RevokeTokenAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token revocation request failed; clearing locally regardless.");
            }
            _credential = null;
        }

        try
        {
            await _dataStore.DeleteAsync<TokenResponse>("user").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete stored Google token from data store.");
        }
    }

    /// <summary>Whether a valid credential is currently loaded in memory.</summary>
    public bool HasCredential => _credential is not null;

    private GoogleAuthorizationCodeFlow CreateFlow() =>
        new(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _options.ClientId,
                ClientSecret = _options.ClientSecret
            },
            Scopes = Scopes,
            DataStore = _dataStore
        });
}
