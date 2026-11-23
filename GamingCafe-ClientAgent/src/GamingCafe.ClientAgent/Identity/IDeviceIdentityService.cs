namespace GamingCafe.ClientAgent.Identity;

public interface IDeviceIdentityService
{
    DeviceIdentity GetOrCreateIdentity();
    DeviceIdentity CurrentIdentity { get; }
    void RefreshIdentity();
}
