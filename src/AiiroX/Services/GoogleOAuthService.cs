using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AiiroX.Services;

/// <summary>
/// Manages Google OAuth 2.0 authentication using Authorization Code Flow with PKCE
/// (Proof Key for Code Exchange, RFC 7636). No client secret is used — only the
/// OAuth Client ID registered as a Desktop-app client in Google Cloud Console.
/// </summary>
/// <remarks>
/// Flow summary:
/// 1. Generate a <c>code_verifier</c> and derive <c>code_challenge = BASE64URL(SHA256(verifier))</c>.
/// 2. Open the default browser to Google's authorization endpoint, passing the challenge.
/// 3. Listen on a random <c>localhost</c> port for the OAuth redirect callback.
/// 4. Exchange the authorization code for tokens using the verifier (no client_secret).
/// 5. Persist the refresh token via <see cref="IDataStore"/> for session restore on next launch.
/// 6. Refresh stale access tokens using only the client_id + refresh_token (no client_secret).
/// <para/>
/// Security note: The <see cref="IDataStore"/> implementation (<see cref="Google.Apis.Util.Store.FileDataStore"/>
/// by default) stores the refresh token as a plain JSON file. A refresh token grants long-term
/// access to the user's Google account. For production, replace <see cref="IDataStore"/> with an
/// OS-keychain-backed implementation (Windows Credential Manager / macOS Keychain / Linux Secret Service).
/// </remarks>
public sealed class GoogleOAuthService
{
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";

    /// <summary>Required scope for the Gemini Generative Language API.</summary>
    private const string Scope = "https://www.googleapis.com/auth/generative-language";

    /// <summary>Key used to store the token in <see cref="IDataStore"/>.</summary>
    private const string StoreKey = "google_pkce_user";

    /// <summary>How many seconds before expiry we consider the access token stale.</summary>
    private const int StalenessBufferSeconds = 60;

    private readonly GoogleOAuthOptions _options;
    private readonly IDataStore _dataStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleOAuthService> _logger;

    /// <summary>In-memory token state (access + refresh + expiry).</summary>
    private TokenResponse? _token;

    public GoogleOAuthService(
        GoogleOAuthOptions options,
        IDataStore dataStore,
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleOAuthService> logger)
    {
        _options = options;
        _dataStore = dataStore;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// <c>true</c> when <see cref="GoogleOAuthOptions.ClientId"/> is set to a real
    /// (non-placeholder) value. When <c>false</c>, <see cref="AuthorizeAsync"/> returns
    /// immediately without opening a browser.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.ClientId) &&
        !_options.ClientId.StartsWith(GoogleOAuthOptions.PlaceholderPrefix);

    /// <summary>Whether a valid credential is currently held in memory.</summary>
    public bool HasCredential => _token is not null;

    /// <summary>
    /// Runs the browser-based PKCE authorization flow.
    /// If valid tokens are already in <see cref="IDataStore"/> they are loaded without
    /// opening the browser.
    /// Returns <c>false</c> immediately (no browser) when <see cref="IsConfigured"/> is
    /// <c>false</c>.
    /// </summary>
    public async Task<bool> AuthorizeAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogError(
                "Google OAuth Client ID is not configured. " +
                "Set a valid ClientId in GoogleOAuthOptions before signing in.");
            return false;
        }

        try
        {
            var port = FindFreePort();
            var redirectUri = $"http://localhost:{port}/";

            var codeVerifier = PkceHelper.GenerateCodeVerifier();
            var codeChallenge = PkceHelper.GenerateCodeChallenge(codeVerifier);
            var state = PkceHelper.Base64UrlEncode(Guid.NewGuid().ToByteArray());

            var authUrl = BuildAuthorizationUrl(redirectUri, codeChallenge, state);

            // Start local callback listener before opening the browser so the redirect
            // does not arrive before we are ready.
            using var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);
            listener.Start();

            OpenBrowser(authUrl);
            _logger.LogInformation("Browser opened for Google sign-in. Waiting for callback on {RedirectUri}.", redirectUri);

            // Wait for the browser to redirect with the authorization code.
            var context = await listener.GetContextAsync()
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            // Send a minimal HTML response so the browser tab closes gracefully.
            await SendCallbackResponseAsync(context.Response).ConfigureAwait(false);
            listener.Stop();

            var query = context.Request.Url?.Query ?? string.Empty;
            var code = ExtractQueryParam(query, "code");
            var returnedState = ExtractQueryParam(query, "state");

            if (string.IsNullOrEmpty(code))
            {
                var error = ExtractQueryParam(query, "error");
                _logger.LogWarning("Google sign-in callback did not return a code. Error: {Error}", error);
                return false;
            }

            if (returnedState != state)
            {
                _logger.LogError("OAuth state mismatch — possible CSRF attack; aborting sign-in.");
                return false;
            }

            var tokenResponse = await ExchangeCodeAsync(code, codeVerifier, redirectUri, cancellationToken)
                .ConfigureAwait(false);

            if (tokenResponse is null) return false;

            _token = tokenResponse;
            await _dataStore.StoreAsync(StoreKey, _token).ConfigureAwait(false);

            _logger.LogInformation("Google PKCE authorization succeeded.");
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Google PKCE authorization was cancelled.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google PKCE authorization failed.");
            return false;
        }
    }

    /// <summary>
    /// Tries to restore a previous session from <see cref="IDataStore"/>.
    /// Does NOT open the browser. Returns <c>false</c> when no stored session exists
    /// or when the stored refresh token cannot be used to obtain a new access token.
    /// </summary>
    public async Task<bool> TryRestoreAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogDebug("Google OAuth not configured; skipping session restore.");
            return false;
        }

        try
        {
            var stored = await _dataStore.GetAsync<TokenResponse>(StoreKey).ConfigureAwait(false);
            if (string.IsNullOrEmpty(stored?.RefreshToken))
            {
                _logger.LogDebug("No stored Google refresh token found; skipping session restore.");
                return false;
            }

            // Try to refresh immediately to confirm the token is still valid.
            var refreshed = await RefreshAccessTokenAsync(stored.RefreshToken, cancellationToken)
                .ConfigureAwait(false);

            if (refreshed is null)
            {
                _logger.LogWarning("Stored Google refresh token could not be used; user must sign in again.");
                return false;
            }

            _token = refreshed;
            await _dataStore.StoreAsync(StoreKey, _token).ConfigureAwait(false);

            _logger.LogInformation("Google OAuth session restored successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not restore Google OAuth session.");
            _token = null;
            return false;
        }
    }

    /// <summary>
    /// Returns a valid access token, automatically refreshing it when stale.
    /// Returns <c>null</c> when the user is not authenticated.
    /// </summary>
    public async Task<string?> GetValidAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_token is null) return null;

        if (IsTokenStale(_token))
        {
            _logger.LogDebug("Google access token is stale; refreshing automatically.");

            if (string.IsNullOrEmpty(_token.RefreshToken))
            {
                _logger.LogWarning("No refresh token available; clearing session.");
                _token = null;
                return null;
            }

            var refreshed = await RefreshAccessTokenAsync(_token.RefreshToken, cancellationToken)
                .ConfigureAwait(false);

            if (refreshed is null)
            {
                _logger.LogWarning("Access token refresh failed; clearing session.");
                _token = null;
                return null;
            }

            _token = refreshed;
            await _dataStore.StoreAsync(StoreKey, _token).ConfigureAwait(false);
        }

        return _token.AccessToken;
    }

    /// <summary>Revokes the current token with Google and removes it from local storage.</summary>
    public async Task RevokeAsync(CancellationToken cancellationToken = default)
    {
        if (_token is not null)
        {
            var tokenToRevoke = _token.RefreshToken ?? _token.AccessToken;
            _token = null;

            if (!string.IsNullOrEmpty(tokenToRevoke))
            {
                try
                {
                    using var http = _httpClientFactory.CreateClient();
                    var content = new FormUrlEncodedContent(
                        new[] { new KeyValuePair<string, string>("token", tokenToRevoke) });
                    await http.PostAsync(RevokeEndpoint, content, cancellationToken)
                        .ConfigureAwait(false);
                    _logger.LogInformation("Google OAuth token revoked.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Token revocation request failed; clearing locally regardless.");
                }
            }
        }

        try
        {
            await _dataStore.DeleteAsync<TokenResponse>(StoreKey).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete stored token from data store.");
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private string BuildAuthorizationUrl(string redirectUri, string codeChallenge, string state)
    {
        var sb = new StringBuilder(AuthEndpoint);
        sb.Append('?');
        sb.Append("client_id=").Append(Uri.EscapeDataString(_options.ClientId));
        sb.Append("&redirect_uri=").Append(Uri.EscapeDataString(redirectUri));
        sb.Append("&response_type=code");
        sb.Append("&scope=").Append(Uri.EscapeDataString(Scope));
        sb.Append("&code_challenge=").Append(Uri.EscapeDataString(codeChallenge));
        sb.Append("&code_challenge_method=S256");
        sb.Append("&access_type=offline");
        sb.Append("&prompt=consent");
        sb.Append("&state=").Append(Uri.EscapeDataString(state));
        return sb.ToString();
    }

    /// <summary>Exchanges an authorization code for tokens using PKCE (no client_secret).</summary>
    private async Task<TokenResponse?> ExchangeCodeAsync(
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri
            // NOTE: No client_secret — PKCE is the proof of authorization for public clients.
        };

        return await PostToTokenEndpointAsync(parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Refreshes an access token using only client_id + refresh_token (no client_secret).</summary>
    private async Task<TokenResponse?> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
            // NOTE: No client_secret — public clients (Desktop App) don't use a secret.
        };

        var response = await PostToTokenEndpointAsync(parameters, cancellationToken).ConfigureAwait(false);
        if (response is null) return null;

        // Google does not always return a new refresh_token on refresh; preserve the existing one.
        if (string.IsNullOrEmpty(response.RefreshToken))
            response.RefreshToken = refreshToken;

        return response;
    }

    private async Task<TokenResponse?> PostToTokenEndpointAsync(
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            using var http = _httpClientFactory.CreateClient();
            var content = new FormUrlEncodedContent(parameters);
            var httpResponse = await http.PostAsync(TokenEndpoint, content, cancellationToken)
                .ConfigureAwait(false);

            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Token endpoint returned {Status}: {Body}", httpResponse.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var tokenResponse = new TokenResponse
            {
                AccessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null,
                RefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
                TokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() : "Bearer",
                ExpiresInSeconds = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt64() : 3600,
                IssuedUtc = DateTime.UtcNow
            };

            return tokenResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token endpoint request failed.");
            return null;
        }
    }

    private static bool IsTokenStale(TokenResponse token)
    {
        if (token.ExpiresInSeconds is null) return false;
        var expiresAt = token.IssuedUtc.AddSeconds((double)token.ExpiresInSeconds);
        return DateTime.UtcNow >= expiresAt.AddSeconds(-StalenessBufferSeconds);
    }

    private static void OpenBrowser(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
        else
        {
            Process.Start("xdg-open", url);
        }
    }

    private static int FindFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task SendCallbackResponseAsync(HttpListenerResponse response)
    {
        const string html =
            "<html><body><p>Sign-in complete. You can close this tab and return to AiiroX.</p>" +
            "<script>window.close();</script></body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html";
        response.ContentLength64 = bytes.Length;
        response.StatusCode = 200;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.OutputStream.Close();
    }

    private static string ExtractQueryParam(string query, string paramName)
    {
        // Trim leading '?' if present.
        var trimmedQuery = query.StartsWith('?') ? query[1..] : query;

        foreach (var part in trimmedQuery.Split('&'))
        {
            var idx = part.IndexOf('=');
            if (idx < 0) continue;
            var key = Uri.UnescapeDataString(part[..idx]);
            if (key == paramName)
                return Uri.UnescapeDataString(part[(idx + 1)..]);
        }

        return string.Empty;
    }
}

