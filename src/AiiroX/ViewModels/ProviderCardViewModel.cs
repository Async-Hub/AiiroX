using AiiroX.Core.Interfaces;
using AiiroX.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace AiiroX.ViewModels;

/// <summary>ViewModel for a single provider card in the Accounts panel.</summary>
public sealed partial class ProviderCardViewModel : ViewModelBase
{
    private readonly IAIAuthProvider _authProvider;
    private readonly IAIChatProvider _chatProvider;

    [ObservableProperty]
    private ProviderConnectionState _connectionState;

    [ObservableProperty]
    private string _credentialInput = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public string ProviderId => _chatProvider.Id;
    public string DisplayName => _chatProvider.DisplayName;

    public bool IsConnected => ConnectionState == ProviderConnectionState.Connected;
    public bool IsDisconnected => ConnectionState == ProviderConnectionState.Disconnected;

    /// <summary>
    /// <c>true</c> when this provider authenticates via browser-based OAuth
    /// (e.g. Google Sign-In) rather than an API key entered by the user.
    /// Drives which connect UI is shown on the provider card.
    /// </summary>
    public bool IsOAuthProvider => _authProvider.UsesOAuthFlow;

    /// <summary>
    /// <c>true</c> when the OAuth Client ID has been configured in the application.
    /// Always <c>true</c> for API-key providers. When <c>false</c> the provider card
    /// shows a setup guidance notice (PKCE flow — no client secret required).
    /// </summary>
    public bool IsOAuthReady => _authProvider.IsAuthConfigured;

    /// <summary>
    /// Provider-specific setup instructions shown in the UI when <see cref="IsOAuthReady"/>
    /// is <c>false</c>. Empty for API-key providers.
    /// </summary>
    public string OAuthSetupGuidance => _authProvider.SetupGuidance;

    public ProviderCardViewModel(IAIChatProvider chatProvider, IAIAuthProvider authProvider)
    {
        _chatProvider = chatProvider;
        _authProvider = authProvider;
        _connectionState = authProvider.ConnectionState;
        _authProvider.ConnectionStateChanged += OnConnectionStateChanged;
        UpdateStatus();
    }

    private void OnConnectionStateChanged(object? sender, ProviderConnectionState state)
    {
        ConnectionState = state;
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsDisconnected));
        UpdateStatus();
    }

    private void UpdateStatus() => StatusMessage = ConnectionState switch
    {
        ProviderConnectionState.Connected => "Connected",
        ProviderConnectionState.Connecting => IsOAuthProvider ? "Signing in with Google..." : "Connecting...",
        ProviderConnectionState.Error => "Connection failed",
        _ => "Not connected"
    };

    [RelayCommand]
    private async Task ConnectAsync()
    {
        // API-key providers require the user to type a key first.
        if (!IsOAuthProvider && string.IsNullOrWhiteSpace(CredentialInput))
        {
            StatusMessage = "Please enter your API key.";
            return;
        }

        IsBusy = true;
        try
        {
            // OAuth providers ignore the credential string; the browser flow is handled internally.
            var success = await _authProvider.ConnectAsync(
                IsOAuthProvider ? string.Empty : CredentialInput);

            if (!IsOAuthProvider) CredentialInput = string.Empty;

            if (!success)
            {
                // Distinguish: credentials missing/invalid config vs. user cancelled vs. other failure.
                if (IsOAuthProvider && ConnectionState == ProviderConnectionState.Error)
                    StatusMessage = $"Sign-in failed: {OAuthSetupGuidance}";
                else if (IsOAuthProvider)
                    StatusMessage = "Sign-in was cancelled. Click \"Sign in with Google\" to try again.";
                else
                    StatusMessage = "Connection failed. Check your API key.";
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        IsBusy = true;
        try { await _authProvider.DisconnectAsync(); }
        finally { IsBusy = false; }
    }
}
