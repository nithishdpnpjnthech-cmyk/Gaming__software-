namespace GamingCafe.ClientAgent.Identity;

public interface IDeviceIdentityStore
{
    DeviceIdentity? LoadIdentity();
    void SaveIdentity(DeviceIdentity identity);
    void DeleteIdentity();
    bool IdentityExists();
}
