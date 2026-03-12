using System;

namespace AiiroX.Services;

/// <summary>
/// Configuration for Google OAuth 2.0 (PKCE) credentials.
/// </summary>
/// <remarks>
/// Only the OAuth Client ID is required. No client secret is used: desktop applications
/// are public clients and must use PKCE (RFC 7636) instead of a shared secret.
///
/// How to obtain the Client ID:
///   1. Go to https://console.cloud.google.com/ and create or select a project.
///   2. Enable the "Generative Language API".
///   3. Navigate to Credentials → Create Credentials → OAuth 2.0 Client ID.
///   4. Set Application type to "Desktop app".
///   5. Copy the generated Client ID into <see cref="ClientId"/>
///      (e.g. via an environment variable or a local secrets file excluded from source control).
///
/// TODO: For production, load <see cref="ClientId"/> from a secrets manager or environment
///       variable rather than embedding it in application code.
/// </remarks>
public sealed class GoogleOAuthOptions
{
    /// <summary>
    /// Prefix used for placeholder credential values (e.g. <c>TODO_YOUR_GOOGLE_OAUTH_CLIENT_ID</c>).
    /// Used by <see cref="Validate"/> and <see cref="GoogleOAuthService.IsConfigured"/> to detect
    /// unconfigured credentials.
    /// </summary>
    internal const string PlaceholderPrefix = "TODO_";

    /// <summary>Google OAuth 2.0 Client ID (required). No client secret is needed for Desktop apps.</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if <see cref="ClientId"/> is still
    /// a placeholder or empty, giving a clear error at startup.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId) || ClientId.StartsWith(PlaceholderPrefix))
            throw new InvalidOperationException(
                "Google OAuth Client ID is not configured. " +
                "Register a Desktop-app OAuth 2.0 client at https://console.cloud.google.com/ " +
                "and set GoogleOAuthOptions.ClientId before starting the application.");
    }
}
