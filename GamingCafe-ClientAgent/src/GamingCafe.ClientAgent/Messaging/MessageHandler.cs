using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Messaging;

public sealed class MessageHandler : IMessageHandler
{
    private readonly ConcurrentDictionary<string, List<Func<BaseMessage, Task>>> _handlers = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _processedMessageIds = new();
    private readonly ILogger<MessageHandler> _logger;
    private const int MaxTrackedMessageIds = 1000;
    private static readonly TimeSpan CacheRetention = TimeSpan.FromMinutes(10);

    public MessageHandler(ILogger<MessageHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void RegisterHandler<T>(string messageType, Func<T, Task> handler) where T : BaseMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentNullException.ThrowIfNull(handler);

        _handlers.AddOrUpdate(
            messageType,
            _ => new List<Func<BaseMessage, Task>> { msg => handler((T)msg) },
            (_, existing) =>
            {
                lock (existing)
                {
                    existing.Add(msg => handler((T)msg));
                }
                return existing;
            });
    }

    public async Task ProcessInboundMessageAsync<T>(T message) where T : BaseMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(message.MessageId))
        {
            _logger.LogWarning("Rejected message: Missing MessageId.");
            return;
        }

        // De-duplication check
        if (!TryTrackMessage(message.MessageId))
        {
            _logger.LogWarning("Duplicate message ignored: {MessageId} ({MessageType})", message.MessageId, message.MessageType);
            return;
        }

        if (_handlers.TryGetValue(message.MessageType, out var handlers))
        {
            List<Func<BaseMessage, Task>> snapshot;
            lock (handlers)
            {
                snapshot = handlers.ToList();
            }

            foreach (var handler in snapshot)
            {
                try
                {
                    await handler(message).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing handler for message {MessageId} of type {MessageType}.", message.MessageId, message.MessageType);
                }
            }
        }
        else
        {
            _logger.LogDebug("No registered handler for message type {MessageType}.", message.MessageType);
        }
    }

    public bool HasProcessed(string messageId) => _processedMessageIds.ContainsKey(messageId);

    private bool TryTrackMessage(string messageId)
    {
        CleanupExpiredMessageIds();

        if (_processedMessageIds.TryAdd(messageId, DateTimeOffset.UtcNow))
        {
            return true;
        }

        return false;
    }

    private void CleanupExpiredMessageIds()
    {
        if (_processedMessageIds.Count < MaxTrackedMessageIds) return;

        var cutoff = DateTimeOffset.UtcNow - CacheRetention;
        foreach (var (id, timestamp) in _processedMessageIds)
        {
            if (timestamp < cutoff)
            {
                _processedMessageIds.TryRemove(id, out _);
            }
        }
    }
}
