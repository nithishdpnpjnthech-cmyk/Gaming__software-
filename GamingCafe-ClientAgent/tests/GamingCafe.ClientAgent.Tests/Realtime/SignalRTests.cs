using System.Text.Json;
using GamingCafe.ClientAgent.Configuration;
using GamingCafe.ClientAgent.Realtime;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace GamingCafe.ClientAgent.Tests.Realtime;

public class SignalRTests
{
    [Fact]
    public void SignalRReconnectPolicy_ExponentialBackoffAndJitter_WithinBounds()
    {
        var config = new AgentConfiguration
        {
            MinRetryBackoffSeconds = 2,
            MaxRetryBackoffSeconds = 30,
            BackoffJitterFactor = 0.1
        };

        var policy = new SignalRReconnectPolicy(config);

        // Attempt 0: 2s (+/- 10%)
        var delay0 = policy.NextRetryDelay(new RetryContext { PreviousRetryCount = 0 });
        Assert.NotNull(delay0);
        Assert.InRange(delay0.Value.TotalSeconds, 1.8, 2.2);

        // Attempt 1: 4s (+/- 10%)
        var delay1 = policy.NextRetryDelay(new RetryContext { PreviousRetryCount = 1 });
        Assert.NotNull(delay1);
        Assert.InRange(delay1.Value.TotalSeconds, 3.6, 4.4);

        // Attempt 2: 8s (+/- 10%)
        var delay2 = policy.NextRetryDelay(new RetryContext { PreviousRetryCount = 2 });
        Assert.NotNull(delay2);
        Assert.InRange(delay2.Value.TotalSeconds, 7.2, 8.8);

        // High attempt: capped at max 30s
        var highDelay = policy.NextRetryDelay(new RetryContext { PreviousRetryCount = 15 });
        Assert.NotNull(highDelay);
        Assert.InRange(highDelay.Value.TotalSeconds, 27.0, 30.0);
    }

    [Fact]
    public void RealtimeEvents_Deserialization_DeserializesSafely()
    {
        var jsonAck = "{\"deviceId\":\"9f57d6ea-0863-4467-96a2-613d96ec37ef\",\"serverTimestampUtc\":\"2026-11-20T12:00:00Z\",\"acknowledged\":true}";
        var ack = JsonSerializer.Deserialize<DeviceStatusAcknowledgedEvent>(jsonAck);
        Assert.NotNull(ack);
        Assert.True(ack.Acknowledged);
        Assert.Equal(Guid.Parse("9f57d6ea-0863-4467-96a2-613d96ec37ef"), ack.DeviceId);

        var jsonEnroll = "{\"deviceId\":\"9f57d6ea-0863-4467-96a2-613d96ec37ef\",\"newStatus\":\"Revoked\",\"reason\":\"Terms violation\"}";
        var enroll = JsonSerializer.Deserialize<EnrollmentStatusChangedEvent>(jsonEnroll);
        Assert.NotNull(enroll);
        Assert.Equal("Revoked", enroll.NewStatus);
        Assert.Equal("Terms violation", enroll.Reason);

        var jsonHealth = "{\"serverStatus\":\"Healthy\",\"timestampUtc\":\"2026-11-20T12:00:00Z\",\"message\":\"All systems operational\"}";
        var health = JsonSerializer.Deserialize<ServerHealthNotificationEvent>(jsonHealth);
        Assert.NotNull(health);
        Assert.Equal("Healthy", health.ServerStatus);

        var jsonMaint = "{\"scheduledTimeUtc\":\"2026-11-20T14:00:00Z\",\"durationMinutes\":15,\"noticeMessage\":\"Daily restart\"}";
        var maint = JsonSerializer.Deserialize<MaintenanceNotificationEvent>(jsonMaint);
        Assert.NotNull(maint);
        Assert.Equal(15, maint.DurationMinutes);
    }
}
