using AiiroX.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AiiroX.Services;

/// <summary>Stores provider connection state (not secrets) as a JSON file.</summary>
public sealed class JsonProviderSessionStore : IProviderSessionStore
{
    private readonly string _storePath;
    private readonly ILogger<JsonProviderSessionStore> _logger;
    private Dictionary<string, bool> _sessions = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Initialization task; awaited by all public methods to ensure disk load completes first.</summary>
    private readonly Task _initTask;

    public JsonProviderSessionStore(ILogger<JsonProviderSessionStore> logger)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "AiiroX");
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "sessions.json");
        _initTask = LoadAsync();
    }

    public async Task SaveSessionAsync(string providerId, bool isConnected, CancellationToken cancellationToken = default)
    {
        await _initTask.ConfigureAwait(false);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _sessions[providerId] = isConnected;
            await PersistAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    public async Task<bool> LoadSessionAsync(string providerId, CancellationToken cancellationToken = default)
    {
        await _initTask.ConfigureAwait(false);
        await _lock.WaitAsync(cancellationToken);
        try { return _sessions.TryGetValue(providerId, out var v) && v; }
        finally { _lock.Release(); }
    }

    public async Task ClearSessionAsync(string providerId, CancellationToken cancellationToken = default)
    {
        await _initTask.ConfigureAwait(false);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _sessions.Remove(providerId);
            await PersistAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(_sessions, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_storePath, json, cancellationToken);
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to persist session store."); }
    }

    private async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_storePath)) return;
            var json = await File.ReadAllTextAsync(_storePath).ConfigureAwait(false);
            _sessions = JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not load session store; starting fresh."); }
    }
}
