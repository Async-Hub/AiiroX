using System.Threading;
using System.Threading.Tasks;

namespace AiiroX.Core.Interfaces;

/// <summary>Restores provider sessions on app startup.</summary>
public interface ISessionRestoreService
{
    Task RestoreAllSessionsAsync(CancellationToken cancellationToken = default);
}
