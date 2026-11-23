namespace GamingCafe.ClientAgent.Models;

/// <summary>
/// Represents the distinct lifecycle states of the Gaming PC Client Agent.
/// </summary>
public enum EnrollmentState
{
    /// <summary>
    /// Agent has not initiated registration with the Admin PC backend.
    /// </summary>
    Unregistered = 0,

    /// <summary>
    /// Agent has requested enrollment and is awaiting approval by the cafe administrator.
    /// </summary>
    EnrollmentPending = 1,

    /// <summary>
    /// Admin has approved the device, ready for credential issuance/exchange.
    /// </summary>
    Approved = 2,

    /// <summary>
    /// Device has obtained valid credentials and is authorized to send heartbeats.
    /// </summary>
    Authenticated = 3,

    /// <summary>
    /// Enrollment was explicitly rejected by the cafe administrator.
    /// </summary>
    Rejected = 4,

    /// <summary>
    /// Previously approved device has had its authorization revoked.
    /// </summary>
    Revoked = 5,

    /// <summary>
    /// Temporary error occurred during communication; backoff/retry in progress.
    /// </summary>
    ErrorRetrying = 6
}
