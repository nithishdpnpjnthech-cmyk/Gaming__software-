namespace GamingCafe.ClientAgent.Authentication;

public interface ISecureCredentialStore
{
    DeviceCredentials? LoadCredentials();
    void SaveCredentials(DeviceCredentials credentials);
    void ClearCredentials();
    bool HasCredentials();
}
