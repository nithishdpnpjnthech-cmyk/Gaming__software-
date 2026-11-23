namespace GamingCafe.ClientAgent.Enrollment;

/// <summary>
/// Lifecycle states of the Gaming PC Client Agent during backend registration and approval.
/// </summary>
public enum EnrollmentState
{
    Unregistered = 0,
    RegistrationPending = 1,
    PendingApproval = 2,
    Approved = 3,
    Authenticated = 4,
    Rejected = 5,
    Revoked = 6,
    Error = 7,
    Retrying = 8
}
