using System.Collections.Concurrent;
using GamingCafe.ClientAgent.Interfaces;

namespace GamingCafe.ClientAgent.Security;

/// <summary>
/// In-memory credential storage implementation for unit testing and mock scenarios.
/// </summary>
public class InMemoryCredentialStorage : ICredentialStorage
{
    private readonly ConcurrentDictionary<string, string> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveCredentialAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        _store[key] = secret;
        return Task.CompletedTask;
    }

    public Task<string?> GetCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _store.TryGetValue(key, out var val);
        return Task.FromResult(val);
    }

    public Task DeleteCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<bool> HasCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Task.FromResult(_store.ContainsKey(key));
    }
}
