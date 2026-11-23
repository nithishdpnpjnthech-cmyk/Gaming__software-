namespace GamingCafe.ClientAgent.Security;

public interface ICredentialProtector
{
    byte[] Protect(byte[] unencryptedData);
    byte[] Unprotect(byte[] encryptedData);
    string ProtectString(string unencrypted);
    string UnprotectString(string encrypted);
}
