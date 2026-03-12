using System.Threading;
using System.Threading.Tasks;

namespace AiiroX.Core.Interfaces;

/// <summary>Persists provider session metadata (not raw secrets).</summary>
public interface IProviderSessionStore
{
    Task SaveSessionAsync(string providerId, bool isConnected, CancellationToken cancellationToken = default);
    Task<bool> LoadSessionAsync(string providerId, CancellationToken cancellationToken = default);
    Task ClearSessionAsync(string providerId, CancellationToken cancellationToken = default);
}
