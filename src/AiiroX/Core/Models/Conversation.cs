using System;
using System.Collections.ObjectModel;

namespace AiiroX.Core.Models;

/// <summary>Represents a conversation thread with an AI provider.</summary>
public sealed class Conversation
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "New Conversation";
    public string ProviderName { get; init; } = string.Empty;
    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
