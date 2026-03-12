using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AiiroX.ViewModels;

/// <summary>Main window view model - manages navigation between chat and accounts panels.</summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    public ChatViewModel ChatPage { get; }
    public AccountsViewModel AccountsPage { get; }

    public MainWindowViewModel(ChatViewModel chatViewModel, AccountsViewModel accountsViewModel)
    {
        ChatPage = chatViewModel;
        AccountsPage = accountsViewModel;
        _currentPage = chatViewModel;
    }

    [RelayCommand]
    private void ShowChat() => CurrentPage = ChatPage;

    [RelayCommand]
    private void ShowAccounts() => CurrentPage = AccountsPage;
}

