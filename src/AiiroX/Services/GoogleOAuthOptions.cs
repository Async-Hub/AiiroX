using System;

namespace AiiroX.Services;

/// <summary>
/// Configuration for Google OAuth 2.0 credentials.
/// </summary>
/// <remarks>
/// TODO: Register an OAuth 2.0 client at https://console.cloud.google.com/
///   1. Create or select a project.
///   2. Enable the "Generative Language API".
///   3. Go to Credentials → Create Credentials → OAuth 2.0 Client ID.
///   4. Application type: "Desktop app".
///   5. Copy the generated Client ID and Client Secret into <see cref="ClientId"/>
///      and <see cref="ClientSecret"/> (e.g. via environment variables or a local
///      secrets file excluded from source control).
///
/// TODO: For production, load these values from a secrets manager or environment
///       variables rather than embedding them in application code.
/// </remarks>
public sealed class GoogleOAuthOptions
{
    /// <summary>
    /// Prefix used for placeholder credential values (e.g. <c>TODO_YOUR_GOOGLE_OAUTH_CLIENT_ID</c>).
    /// <see cref="Validate"/> and <see cref="GoogleOAuthService.IsConfigured"/> both detect this prefix.
    /// </summary>
    internal const string PlaceholderPrefix = "TODO_";

    /// <summary>Google OAuth 2.0 Client ID (required).</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>Google OAuth 2.0 Client Secret (required).</summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if the credentials are still
    /// the placeholder values or are otherwise empty, giving a clear error at startup.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId) || ClientId.StartsWith(PlaceholderPrefix))
            throw new InvalidOperationException(
                "Google OAuth Client ID is not configured. " +
                "Register a Desktop-app OAuth 2.0 client at https://console.cloud.google.com/ " +
                "and set GoogleOAuthOptions.ClientId before starting the application.");

        if (string.IsNullOrWhiteSpace(ClientSecret) || ClientSecret.StartsWith(PlaceholderPrefix))
            throw new InvalidOperationException(
                "Google OAuth Client Secret is not configured. " +
                "Register a Desktop-app OAuth 2.0 client at https://console.cloud.google.com/ " +
                "and set GoogleOAuthOptions.ClientSecret before starting the application.");
    }
}
