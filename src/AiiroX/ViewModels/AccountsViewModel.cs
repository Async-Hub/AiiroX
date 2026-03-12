using AiiroX.Core.Registry;
using System;
using System.Collections.ObjectModel;

namespace AiiroX.ViewModels;

/// <summary>ViewModel for the Connected Accounts / AI Providers panel.</summary>
public sealed partial class AccountsViewModel : ViewModelBase
{
    public ObservableCollection<ProviderCardViewModel> ProviderCards { get; } = new();

    public AccountsViewModel(ProviderRegistry registry, IServiceProvider services)
    {
        foreach (var chatProvider in registry.ChatProviders)
        {
            var auth = registry.GetAuthProvider(chatProvider.Id);
            if (auth is null) continue;
            ProviderCards.Add(new ProviderCardViewModel(chatProvider, auth));
        }
    }
}
