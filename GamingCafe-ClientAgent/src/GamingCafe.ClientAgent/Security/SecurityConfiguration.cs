using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Security;

/// <summary>
/// Security configuration and OS permissions helper.
/// Enforces least privilege directory ACLs on Windows:
/// - Administrators: Full Control
/// - SYSTEM: Full Control
/// - Standard Users: No Access to data/credential folders.
/// </summary>
public static class SecurityConfiguration
{
    public const string RecommendedServiceAccount = "NT AUTHORITY\\LocalService";

    public static void ApplyRestrictiveDirectoryAcl(string directoryPath, ILogger? logger = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            var dirInfo = new DirectoryInfo(directoryPath);
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var currentUserSid = WindowsIdentity.GetCurrent().User;

            security.AddAccessRule(new FileSystemAccessRule(systemSid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(adminSid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));

            if (currentUserSid != null && !currentUserSid.Equals(systemSid) && !currentUserSid.Equals(adminSid))
            {
                security.AddAccessRule(new FileSystemAccessRule(currentUserSid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            }

            dirInfo.SetAccessControl(security);
            logger?.LogDebug("Applied strict Windows ACLs on directory: {Path}", directoryPath);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "ACL hardening on {Path} skipped (non-elevated developer environment).", directoryPath);
        }
    }
}
