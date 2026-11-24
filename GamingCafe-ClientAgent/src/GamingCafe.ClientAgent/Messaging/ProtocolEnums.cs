namespace GamingCafe.ClientAgent.Messaging;

public enum EnrollmentState
{
    Unregistered,
    RegistrationPending,
    PendingApproval,
    Approved,
    Authenticated,
    Rejected,
    Revoked,
    Error
}

public enum AgentConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Suspended
}
