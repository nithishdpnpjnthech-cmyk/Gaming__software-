using GamingCafe.ClientAgent.Messaging;

namespace GamingCafe.ClientAgent.Enrollment;

public interface IDeviceEnrollmentService
{
    EnrollmentState CurrentState { get; }
    event Action<EnrollmentState>? StateChanged;
    Task InitializeStateAsync();
    Task HandleApprovalMessageAsync(DeviceApprovalMessage message);
    Task HandleAuthenticationMessageAsync(DeviceAuthenticationMessage message);
    Task HandleRevocationMessageAsync(DeviceRevocationMessage message);
    Task TransitionToAsync(EnrollmentState newState, string? reason = null);
}
