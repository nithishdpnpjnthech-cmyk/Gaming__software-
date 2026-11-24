namespace GamingCafe.ClientAgent.Messaging;

public abstract class BaseMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
    public string Version { get; set; } = "1.0";
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid DeviceId { get; set; }
    public abstract string MessageType { get; }
}

public sealed class DeviceRegistrationMessage : BaseMessage
{
    public override string MessageType => "DeviceRegistration";
    public string DeviceName { get; set; } = string.Empty;
    public string HardwareIdentifier { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string ClientVersion { get; set; } = string.Empty;
}

public sealed class DeviceApprovalMessage : BaseMessage
{
    public override string MessageType => "DeviceApproval";
    public bool IsApproved { get; set; }
    public EnrollmentState ApprovalStatus { get; set; }
    public string? AssignedZone { get; set; }
    public string? RejectionReason { get; set; }
}

public sealed class DeviceAuthenticationMessage : BaseMessage
{
    public override string MessageType => "DeviceAuthentication";
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTimeOffset TokenExpiresAtUtc { get; set; }
    public DateTimeOffset ServerTimeUtc { get; set; }
}

public sealed class HeartbeatMessage : BaseMessage
{
    public override string MessageType => "Heartbeat";
    public long SequenceNumber { get; set; }
    public string AgentVersion { get; set; } = string.Empty;
    public double UpTimeSeconds { get; set; }
    public double CpuPercent { get; set; }
    public double MemoryUsageMb { get; set; }
}

public sealed class HeartbeatAcknowledgment : BaseMessage
{
    public override string MessageType => "HeartbeatAck";
    public long SequenceNumber { get; set; }
    public string OriginalMessageId { get; set; } = string.Empty;
    public DateTimeOffset ServerTimestampUtc { get; set; }
}

public sealed class DeviceConfigurationMessage : BaseMessage
{
    public override string MessageType => "DeviceConfiguration";
    public string ConfigVersion { get; set; } = string.Empty;
    public int HeartbeatIntervalSeconds { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
}

public sealed class ConnectionStatusMessage : BaseMessage
{
    public override string MessageType => "ConnectionStatus";
    public AgentConnectionStatus Status { get; set; }
    public string? Reason { get; set; }
}

public sealed class DeviceRevocationMessage : BaseMessage
{
    public override string MessageType => "DeviceRevocation";
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset RevokedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DeviceErasedMessage : BaseMessage
{
    public override string MessageType => "DeviceErased";
    public string DeviceCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset ErasedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ErrorMessage : BaseMessage
{
    public override string MessageType => "ErrorMessage";
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorDescription { get; set; } = string.Empty;
}
