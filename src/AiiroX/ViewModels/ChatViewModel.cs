using AiiroX.Core.Models;
using AiiroX.Core.Registry;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace AiiroX.ViewModels;

/// <summary>ViewModel for the chat page.</summary>
public sealed partial class ChatViewModel : ViewModelBase
{
    private readonly ProviderRegistry _registry;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private string _userInput = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ProviderCardViewModel? _selectedProvider;

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<ProviderCardViewModel> AvailableProviders { get; } = new();

    private readonly Conversation _conversation = new();

    public ChatViewModel(ProviderRegistry registry, AccountsViewModel accounts)
    {
        _registry = registry;
        foreach (var card in accounts.ProviderCards)
            AvailableProviders.Add(card);
        if (AvailableProviders.Count > 0)
            SelectedProvider = AvailableProviders[0];
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendMessageAsync()
    {
        if (SelectedProvider is null || string.IsNullOrWhiteSpace(UserInput)) return;

        var provider = _registry.GetChatProvider(SelectedProvider.ProviderId);
        if (provider is null) return;

        var text = UserInput.Trim();
        UserInput = string.Empty;
        ErrorMessage = string.Empty;

        var userMsg = new ChatMessage
        {
            Role = MessageRole.User,
            Content = text
        };
        Messages.Add(userMsg);
        _conversation.Messages.Add(userMsg);

        _cts = new CancellationTokenSource();
        IsLoading = true;
        SendMessageCommand.NotifyCanExecuteChanged();
        try
        {
            var reply = await provider.SendMessageAsync(_conversation.Messages, text, _cts.Token);
            var assistantMsg = new ChatMessage
            {
                Role = MessageRole.Assistant,
                Content = reply,
                ProviderName = provider.DisplayName
            };
            Messages.Add(assistantMsg);
            _conversation.Messages.Add(assistantMsg);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Request cancelled.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _cts.Dispose();
            _cts = null;
            SendMessageCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanSend()
        => !IsLoading && !string.IsNullOrWhiteSpace(UserInput) && SelectedProvider?.IsConnected == true;

    [RelayCommand]
    private void CancelRequest()
    {
        _cts?.Cancel();
    }

    partial void OnUserInputChanged(string value) => SendMessageCommand.NotifyCanExecuteChanged();
    partial void OnSelectedProviderChanged(ProviderCardViewModel? value) => SendMessageCommand.NotifyCanExecuteChanged();
}
