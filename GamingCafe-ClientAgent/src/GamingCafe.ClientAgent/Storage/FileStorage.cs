using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using GamingCafe.ClientAgent.Interfaces;
using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Storage;

/// <summary>
/// Atomic, resilient file storage abstraction for the Client Agent.
/// </summary>
public class FileStorage : IFileStorage
{
    private readonly string _baseDirectory;
    private readonly ILogger<FileStorage> _logger;
    private readonly object _lock = new();

    public FileStorage(string? customBaseDirectory, ILogger<FileStorage> logger)
    {
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(customBaseDirectory))
        {
            _baseDirectory = Path.GetFullPath(customBaseDirectory);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _baseDirectory = Path.Combine(programData, "GamingCafe", "ClientAgent");
        }
        else
        {
            _baseDirectory = Path.Combine(AppContext.BaseDirectory, "agent_data");
        }

        EnsureDirectoryExists();
    }

    public string BaseDirectory => _baseDirectory;

    public string GetFullPath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return relativePath;
        }
        return Path.Combine(_baseDirectory, relativePath);
    }

    public bool Exists(string relativePath)
    {
        return File.Exists(GetFullPath(relativePath));
    }

    public void Delete(string relativePath)
    {
        var path = GetFullPath(relativePath);
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                _logger.LogInformation("Deleted storage file: {Path}", relativePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete storage file: {Path}", relativePath);
            }
        }
    }

    public async Task WriteTextAtomicAsync(string relativePath, string content, CancellationToken cancellationToken = default)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        await WriteBytesAtomicAsync(relativePath, bytes, cancellationToken);
    }

    public async Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var bytes = await ReadBytesAsync(relativePath, cancellationToken);
        return bytes == null ? null : System.Text.Encoding.UTF8.GetString(bytes);
    }

    public async Task WriteBytesAtomicAsync(string relativePath, byte[] data, CancellationToken cancellationToken = default)
    {
        var targetPath = GetFullPath(relativePath);
        var dir = Path.GetDirectoryName(targetPath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = targetPath + $".{Guid.NewGuid():N}.tmp";

        // Write to temp file then replace atomically
        await File.WriteAllBytesAsync(tempPath, data, cancellationToken);

        lock (_lock)
        {
            if (File.Exists(targetPath))
            {
                File.Replace(tempPath, targetPath, null);
            }
            else
            {
                File.Move(tempPath, targetPath);
            }
        }

        ApplyStrictAclIfWindows(targetPath);
    }

    public async Task<byte[]?> ReadBytesAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var targetPath = GetFullPath(relativePath);
        if (!File.Exists(targetPath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(targetPath, cancellationToken);
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_baseDirectory))
        {
            Directory.CreateDirectory(_baseDirectory);
            ApplyStrictDirectoryAclIfWindows(_baseDirectory);
        }
    }

    private void ApplyStrictDirectoryAclIfWindows(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            var dirInfo = new DirectoryInfo(path);
            var security = new DirectorySecurity();
            // Disable inheritance to isolate agent storage from regular cafe users
            security.SetAccessRuleProtection(true, false);

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
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ACL configuration on directory {Path} could not be applied (likely non-elevated developer mode).", path);
        }
    }

    private void ApplyStrictAclIfWindows(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            var fileInfo = new FileInfo(path);
            var security = new FileSecurity();
            security.SetAccessRuleProtection(true, false);

            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var currentUserSid = WindowsIdentity.GetCurrent().User;

            security.AddAccessRule(new FileSystemAccessRule(systemSid, FileSystemRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(adminSid, FileSystemRights.FullControl, AccessControlType.Allow));
            if (currentUserSid != null && !currentUserSid.Equals(systemSid) && !currentUserSid.Equals(adminSid))
            {
                security.AddAccessRule(new FileSystemAccessRule(currentUserSid, FileSystemRights.FullControl, AccessControlType.Allow));
            }

            fileInfo.SetAccessControl(security);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ACL configuration on file {Path} could not be applied (developer mode).", path);
        }
    }
}
