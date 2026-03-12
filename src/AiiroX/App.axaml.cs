using AiiroX.Core.Interfaces;
using AiiroX.Core.Registry;
using AiiroX.Providers.Gemini;
using AiiroX.Providers.OpenAI;
using AiiroX.Services;
using AiiroX.ViewModels;
using AiiroX.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace AiiroX;

public partial class App : Application
{
    private IServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _services = services.BuildServiceProvider();

        var restorer = _services.GetRequiredService<ISessionRestoreService>();
        _ = restorer.RestoreAllSessionsAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddHttpClient();

        services.AddSingleton<ITokenStore, SecureTokenStore>();
        services.AddSingleton<IProviderSessionStore, JsonProviderSessionStore>();

        // Concrete auth providers (registered first so chat providers can depend on them).
        services.AddSingleton<OpenAIAuthProvider>();
        services.AddSingleton<GeminiAuthProvider>();

        // IAIAuthProvider registrations — both are enumerated by ProviderRegistry via IEnumerable<IAIAuthProvider>.
        services.AddSingleton<IAIAuthProvider>(sp => sp.GetRequiredService<OpenAIAuthProvider>());
        services.AddSingleton<IAIAuthProvider>(sp => sp.GetRequiredService<GeminiAuthProvider>());

        // Concrete chat providers — depend on their matching auth provider for IsConnected state tracking.
        services.AddSingleton<OpenAIChatProvider>();
        services.AddSingleton<GeminiChatProvider>();

        // IAIChatProvider registrations — both are enumerated by ProviderRegistry via IEnumerable<IAIChatProvider>.
        services.AddSingleton<IAIChatProvider>(sp => sp.GetRequiredService<OpenAIChatProvider>());
        services.AddSingleton<IAIChatProvider>(sp => sp.GetRequiredService<GeminiChatProvider>());

        services.AddSingleton<ProviderRegistry>();
        services.AddSingleton<ISessionRestoreService, SessionRestoreService>();

        services.AddSingleton<AccountsViewModel>();
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in dataValidationPluginsToRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}