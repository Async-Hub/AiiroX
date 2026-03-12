using AiiroX.Core.Interfaces;
using AiiroX.Core.Registry;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiiroX.Services;

/// <summary>Restores all known provider sessions on application startup.</summary>
public sealed class SessionRestoreService : ISessionRestoreService
{
    private readonly ProviderRegistry _registry;
    private readonly ILogger<SessionRestoreService> _logger;

    public SessionRestoreService(ProviderRegistry registry, ILogger<SessionRestoreService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public async Task RestoreAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var provider in _registry.ChatProviders)
        {
            var auth = _registry.GetAuthProvider(provider.Id);
            if (auth is null) continue;

            try
            {
                var restored = await auth.TryRestoreSessionAsync(cancellationToken);
                _logger.LogInformation("Provider {Id}: session restore = {Result}", provider.Id, restored);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore session for provider {Id}.", provider.Id);
            }
        }
    }
}
