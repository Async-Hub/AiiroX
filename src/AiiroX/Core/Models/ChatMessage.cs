using System;

namespace AiiroX.Core.Models;

/// <summary>Represents a single message in a conversation.</summary>
public sealed class ChatMessage
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public MessageRole Role { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    /// <summary>Name of the AI provider that produced this message (null for user messages).</summary>
    public string? ProviderName { get; init; }
}
