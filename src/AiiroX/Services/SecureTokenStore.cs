using AiiroX.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace AiiroX.Services;

/// <summary>
/// Token store that persists API keys to a local file.
/// WARNING: Tokens are stored as base64 - this provides obfuscation only, not strong security.
/// TODO: Replace with OS Keychain (Windows DPAPI, macOS Keychain, Linux Secret Service) for production use.
/// </summary>
public sealed class SecureTokenStore : ITokenStore
{
    private readonly string _storePath;
    private readonly ILogger<SecureTokenStore> _logger;
    private readonly Dictionary<string, string> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Initialization task; awaited by all public methods to ensure disk load completes first.</summary>
    private readonly Task _initTask;

    public SecureTokenStore(ILogger<SecureTokenStore> logger)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "AiiroX");
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "tokens.dat");
        _initTask = LoadFromDiskAsync();
    }

    public async Task SaveTokenAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await _initTask.ConfigureAwait(false);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _cache[key] = value;
            await PersistAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    public async Task<string?> LoadTokenAsync(string key, CancellationToken cancellationToken = default)
    {
        await _initTask.ConfigureAwait(false);
        await _lock.WaitAsync(cancellationToken);
        try { return _cache.TryGetValue(key, out var v) ? v : null; }
        finally { _lock.Release(); }
    }

    public async Task DeleteTokenAsync(string key, CancellationToken cancellationToken = default)
    {
        await _initTask.ConfigureAwait(false);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _cache.Remove(key);
            await PersistAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(_cache);
            var bytes = Encoding.UTF8.GetBytes(json);
            // TODO: Replace with OS keychain integration for production security.
            var b64 = Convert.ToBase64String(bytes);
            await File.WriteAllTextAsync(_storePath, b64, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist token store.");
        }
    }

    private async Task LoadFromDiskAsync()
    {
        try
        {
            if (!File.Exists(_storePath)) return;

            var b64 = await File.ReadAllTextAsync(_storePath).ConfigureAwait(false);
            var bytes = Convert.FromBase64String(b64);
            var json = Encoding.UTF8.GetString(bytes);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict is not null)
                foreach (var kv in dict)
                    _cache[kv.Key] = kv.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load token store from disk; starting fresh.");
        }
    }
}

