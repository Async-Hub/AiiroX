using AiiroX.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AiiroX.Core.Interfaces;

/// <summary>Interface for providers that support text chat.</summary>
public interface IAIChatProvider : IAIProvider
{
    /// <summary>Sends a message and returns the assistant's reply.</summary>
    Task<string> SendMessageAsync(
        IReadOnlyList<ChatMessage> conversationHistory,
        string userMessage,
        CancellationToken cancellationToken = default);
}
