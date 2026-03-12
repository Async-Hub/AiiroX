using AiiroX.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace AiiroX.Core.Registry;

/// <summary>Registry that holds all registered AI providers.</summary>
public sealed class ProviderRegistry
{
    private readonly List<IAIChatProvider> _chatProviders;
    private readonly Dictionary<string, IAIAuthProvider> _authProviders;

    public ProviderRegistry(
        IEnumerable<IAIChatProvider> chatProviders,
        IEnumerable<IAIAuthProvider> authProviders)
    {
        _chatProviders = chatProviders.ToList();
        _authProviders = authProviders.ToDictionary(p => p.ProviderId);
    }

    public IReadOnlyList<IAIChatProvider> ChatProviders => _chatProviders;

    public IAIAuthProvider? GetAuthProvider(string providerId)
        => _authProviders.TryGetValue(providerId, out var p) ? p : null;

    public IAIChatProvider? GetChatProvider(string providerId)
        => _chatProviders.FirstOrDefault(p => p.Id == providerId);
}
