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
        ProviderConnectionState.Connecting => "Connecting...",
        ProviderConnectionState.Error => "Connection failed",
        _ => "Not connected"
    };

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(CredentialInput))
        {
            StatusMessage = "Please enter your API key.";
            return;
        }
        IsBusy = true;
        try
        {
            var success = await _authProvider.ConnectAsync(CredentialInput);
            CredentialInput = string.Empty;
            if (!success) StatusMessage = "Connection failed. Check your API key.";
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
