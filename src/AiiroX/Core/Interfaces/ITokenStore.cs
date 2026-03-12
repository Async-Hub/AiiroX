using System.Threading;
using System.Threading.Tasks;

namespace AiiroX.Core.Interfaces;

/// <summary>Secure storage for API keys and tokens.</summary>
public interface ITokenStore
{
    Task SaveTokenAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<string?> LoadTokenAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteTokenAsync(string key, CancellationToken cancellationToken = default);
}
