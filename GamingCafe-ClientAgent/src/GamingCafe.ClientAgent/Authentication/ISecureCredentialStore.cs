namespace GamingCafe.ClientAgent.Authentication;

public interface ISecureCredentialStore
{
    string StorageDirectory { get; }
    string CredentialFilePath { get; }
    DeviceCredentials? LoadCredentials();
    void SaveCredentials(DeviceCredentials credentials);
    void ClearCredentials();
    bool HasCredentials();
}
