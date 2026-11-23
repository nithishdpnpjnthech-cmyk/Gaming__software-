using GamingCafe.ClientAgent.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GamingCafe.ClientAgent.Tests;

public class MessageHandlerTests
{
    [Fact]
    public async Task ProcessInboundMessage_DispatchesToRegisteredHandler()
    {
        var handler = new MessageHandler(NullLogger<MessageHandler>.Instance);
        DeviceApprovalMessage? receivedMessage = null;

        handler.RegisterHandler<DeviceApprovalMessage>("DeviceApproval", msg =>
        {
            receivedMessage = msg;
            return Task.CompletedTask;
        });

        var approval = new DeviceApprovalMessage
        {
            DeviceId = Guid.NewGuid(),
            IsApproved = true,
            AssignedZone = "VIP-Zone"
        };

        await handler.ProcessInboundMessageAsync(approval);

        Assert.NotNull(receivedMessage);
        Assert.Equal(approval.DeviceId, receivedMessage.DeviceId);
        Assert.True(receivedMessage.IsApproved);
        Assert.Equal("VIP-Zone", receivedMessage.AssignedZone);
    }

    [Fact]
    public async Task DuplicateMessageId_IsIgnoredByDeduplication()
    {
        var handler = new MessageHandler(NullLogger<MessageHandler>.Instance);
        var invocationCount = 0;

        handler.RegisterHandler<HeartbeatAcknowledgment>("HeartbeatAck", msg =>
        {
            invocationCount++;
            return Task.CompletedTask;
        });

        var ack = new HeartbeatAcknowledgment
        {
            MessageId = "unique-msg-id-12345",
            SequenceNumber = 42
        };

        await handler.ProcessInboundMessageAsync(ack);
        await handler.ProcessInboundMessageAsync(ack); // duplicate

        Assert.Equal(1, invocationCount);
        Assert.True(handler.HasProcessed("unique-msg-id-12345"));
    }
}
