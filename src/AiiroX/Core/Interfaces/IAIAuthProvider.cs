using AiiroX.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiiroX.Core.Interfaces;

/// <summary>Interface for authentication on AI providers.</summary>
public interface IAIAuthProvider
{
    /// <summary>The provider this auth handler belongs to.</summary>
    string ProviderId { get; }

    /// <summary>
    /// When <c>true</c> this provider uses a browser-based OAuth flow; the <c>credential</c>
    /// parameter of <see cref="ConnectAsync"/> is ignored and can be passed as empty.
    /// When <c>false</c> the provider expects an API key/secret as the credential.
    /// </summary>
    bool UsesOAuthFlow { get; }

    /// <summary>
    /// Whether the provider's authentication prerequisites are satisfied.
    /// For Google OAuth providers this means the OAuth Client ID has been configured
    /// (PKCE flow — no client secret required); for API-key providers this is always
    /// <c>true</c> because the key is supplied at runtime by the user.
    /// </summary>
    bool IsAuthConfigured { get; }

    /// <summary>
    /// Human-readable setup instructions displayed in the UI when <see cref="IsAuthConfigured"/>
    /// is <c>false</c>.  Returns an empty string when no setup is required (e.g. API-key providers).
    /// </summary>
    string SetupGuidance { get; }

    /// <summary>Attempts to connect/authenticate with the provider.</summary>
    Task<bool> ConnectAsync(string credential, CancellationToken cancellationToken = default);

    /// <summary>Disconnects from the provider and clears local session.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Tries to restore a previously saved session.</summary>
    Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>Current connection state.</summary>
    ProviderConnectionState ConnectionState { get; }

    /// <summary>Raised when connection state changes.</summary>
    event EventHandler<ProviderConnectionState>? ConnectionStateChanged;
}
