namespace AiiroX.Core.Interfaces;

/// <summary>Base interface for all AI providers.</summary>
public interface IAIProvider
{
    /// <summary>Unique identifier for the provider.</summary>
    string Id { get; }

    /// <summary>Human-readable display name.</summary>
    string DisplayName { get; }

    /// <summary>Whether the provider is currently authenticated.</summary>
    bool IsConnected { get; }
}
