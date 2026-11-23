using System.Collections.Concurrent;
using System.Text;
using GamingCafe.ClientAgent.Interfaces;

namespace GamingCafe.ClientAgent.Tests.TestDoubles;

/// <summary>
/// In-memory IFileStorage implementation for fast, isolated unit tests.
/// </summary>
public class TestFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public Task WriteTextAtomicAsync(string relativePath, string content, CancellationToken cancellationToken = default)
    {
        _files[relativePath] = Encoding.UTF8.GetBytes(content);
        return Task.CompletedTask;
    }

    public Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (_files.TryGetValue(relativePath, out var bytes))
        {
            return Task.FromResult<string?>(Encoding.UTF8.GetString(bytes));
        }
        return Task.FromResult<string?>(null);
    }

    public Task WriteBytesAtomicAsync(string relativePath, byte[] data, CancellationToken cancellationToken = default)
    {
        _files[relativePath] = (byte[])data.Clone();
        return Task.CompletedTask;
    }

    public Task<byte[]?> ReadBytesAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (_files.TryGetValue(relativePath, out var bytes))
        {
            return Task.FromResult<byte[]?>((byte[])bytes.Clone());
        }
        return Task.FromResult<byte[]?>(null);
    }

    public bool Exists(string relativePath)
    {
        return _files.ContainsKey(relativePath);
    }

    public void Delete(string relativePath)
    {
        _files.TryRemove(relativePath, out _);
    }

    public string GetFullPath(string relativePath)
    {
        return $"memory://{relativePath}";
    }

    public void SeedCorruptFile(string relativePath)
    {
        _files[relativePath] = Encoding.UTF8.GetBytes("{ broken-json: invalid ### }");
    }
}
