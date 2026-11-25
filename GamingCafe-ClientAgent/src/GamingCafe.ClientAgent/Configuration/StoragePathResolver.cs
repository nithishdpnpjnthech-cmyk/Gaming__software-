using Microsoft.Extensions.Logging;

namespace GamingCafe.ClientAgent.Configuration;

/// <summary>
/// Resolves storage directory paths deterministically for both:
/// 1. Development (dotnet run from repository root or project folder)
/// 2. Production (Installed Windows Service or published single-file deployment)
/// </summary>
public static class StoragePathResolver
{
    public static string ResolveStorageDirectory(
        string configuredStorageDirectory, 
        ILogger? logger = null)
    {
        var rawDir = string.IsNullOrWhiteSpace(configuredStorageDirectory) ? "data" : configuredStorageDirectory;

        // 1. If explicitly configured with an absolute path, use it directly
        if (Path.IsPathRooted(rawDir))
        {
            var absolutePath = Path.GetFullPath(rawDir);
            logger?.LogDebug("StorageDirectory is explicitly rooted: '{StorageDir}'", absolutePath);
            return absolutePath;
        }

        // 2. Candidate base paths to probe in deterministic order
        var baseDir = AppContext.BaseDirectory;
        var currentDir = Directory.GetCurrentDirectory();

        // Candidates for search:
        // A. If CurrentDirectory contains the relative folder (e.g. running from repo root or solution root)
        var cwdCandidate = Path.GetFullPath(Path.Combine(currentDir, rawDir));
        if (Directory.Exists(cwdCandidate))
        {
            logger?.LogDebug("StorageDirectory resolved from CurrentDirectory: '{StorageDir}'", cwdCandidate);
            return cwdCandidate;
        }

        // B. If running from repository root where subdirectory GamingCafe-ClientAgent exists
        var repoSubdirCandidate = Path.GetFullPath(Path.Combine(currentDir, "GamingCafe-ClientAgent", rawDir));
        if (Directory.Exists(repoSubdirCandidate))
        {
            logger?.LogDebug("StorageDirectory resolved from solution subfolder: '{StorageDir}'", repoSubdirCandidate);
            return repoSubdirCandidate;
        }

        // C. Probe ancestor directories from BaseDirectory (e.g. dotnet run places binary in bin/Debug/net10.0/)
        var currentSearchDir = new DirectoryInfo(baseDir);
        while (currentSearchDir != null && currentSearchDir.Parent != null)
        {
            // If we find GamingCafe.ClientAgent.sln or GamingCafe-ClientAgent root
            var slnFile = Path.Combine(currentSearchDir.FullName, "GamingCafe.ClientAgent.sln");
            var candidateFolder = Path.Combine(currentSearchDir.FullName, rawDir);

            if (File.Exists(slnFile) && Directory.Exists(candidateFolder))
            {
                logger?.LogDebug("StorageDirectory resolved from project ancestor '{Ancestor}': '{StorageDir}'",
                    currentSearchDir.FullName, candidateFolder);
                return Path.GetFullPath(candidateFolder);
            }

            currentSearchDir = currentSearchDir.Parent;
        }

        // D. Fallback to AppContext.BaseDirectory / rawDir (standard for standalone single-file / installed service)
        var fallbackPath = Path.GetFullPath(Path.Combine(baseDir, rawDir));
        logger?.LogDebug("StorageDirectory defaulting to BaseDirectory: '{StorageDir}'", fallbackPath);
        return fallbackPath;
    }
}
