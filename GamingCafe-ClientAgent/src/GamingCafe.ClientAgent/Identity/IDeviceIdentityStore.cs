namespace GamingCafe.ClientAgent.Identity;

public interface IDeviceIdentityStore
{
    string StorageDirectory { get; }
    string IdentityFilePath { get; }
    DeviceIdentity? LoadIdentity();
    void SaveIdentity(DeviceIdentity identity);
    void DeleteIdentity();
    bool IdentityExists();
}
