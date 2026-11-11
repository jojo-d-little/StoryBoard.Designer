using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace StoryboardDesigner.App.Services;

public sealed class ProjectBackupService : IProjectBackupService
{
    private static readonly object BackupLock = new();
    private readonly BackupPreferencesService _preferencesService;

    public ProjectBackupService(BackupPreferencesService? preferencesService = null)
    {
        _preferencesService = preferencesService ?? new BackupPreferencesService();
    }

    public string? CreateBackupIfDue(string projectFilePath, string projectName)
    {
        lock (BackupLock)
        {
            return CreateBackupIfDueLocked(projectFilePath, projectName);
        }
    }

    private string? CreateBackupIfDueLocked(string projectFilePath, string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath) || !File.Exists(projectFilePath)
            || string.IsNullOrWhiteSpace(projectName))
        {
            return null;
        }

        var projectRoot = Path.GetDirectoryName(Path.GetFullPath(projectFilePath));
        if (string.IsNullOrWhiteSpace(projectRoot) || !Directory.Exists(projectRoot))
        {
            return null;
        }

        var preferences = _preferencesService.Load();
        var normalizedBackupRoot = Path.GetFullPath(preferences.BackupRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedProjectRoot = Path.GetFullPath(projectRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var projectPrefix = normalizedProjectRoot + Path.DirectorySeparatorChar;
        if (string.Equals(normalizedBackupRoot, normalizedProjectRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedBackupRoot.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The backup folder must be outside the project folder.");
        }

        var safeProjectName = SanitizeFileName(projectName);
        var projectBackupFolder = BuildProjectBackupFolder(normalizedBackupRoot, projectRoot, safeProjectName);
        Directory.CreateDirectory(projectBackupFolder);

        var existingBackups = Directory.EnumerateFiles(projectBackupFolder, "*.zip", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();
        var now = DateTime.UtcNow;
        var interval = TimeSpan.FromHours(preferences.BackupIntervalHours);
        if (existingBackups.Count > 0 && now - existingBackups[0].LastWriteTimeUtc < interval)
        {
            return null;
        }

        var timestamp = now.ToString("yyyyMMdd'T'HHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var archivePath = Path.Combine(projectBackupFolder, $"{safeProjectName}_{timestamp}.zip");
        var temporaryArchivePath = archivePath + ".tmp";

        try
        {
            CreateProjectArchive(projectRoot, temporaryArchivePath);
            File.Move(temporaryArchivePath, archivePath);
        }
        catch
        {
            TryDeleteFile(temporaryArchivePath);
            throw;
        }

        PruneBackups(projectBackupFolder, preferences.MaximumBackupCount);
        return archivePath;
    }

    private static void CreateProjectArchive(string projectRoot, string archivePath)
    {
        var parentDirectoryName = Path.GetFileName(projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);

        foreach (var directory in Directory.EnumerateDirectories(projectRoot, "*", SearchOption.AllDirectories))
        {
            var relativeDirectory = Path.GetRelativePath(projectRoot, directory);
            archive.CreateEntry($"{parentDirectoryName}/{NormalizeArchivePath(relativeDirectory)}/");
        }

        foreach (var file in Directory.EnumerateFiles(projectRoot, "*", SearchOption.AllDirectories))
        {
            var relativeFile = Path.GetRelativePath(projectRoot, file);
            archive.CreateEntryFromFile(
                file,
                $"{parentDirectoryName}/{NormalizeArchivePath(relativeFile)}",
                CompressionLevel.Fastest);
        }
    }

    private static string BuildProjectBackupFolder(string backupRoot, string projectRoot, string projectName)
    {
        var normalizedProjectRoot = Path.GetFullPath(projectRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var identityBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedProjectRoot.ToUpperInvariant()));
        var identity = Convert.ToHexString(identityBytes.AsSpan(0, 6)).ToLowerInvariant();
        return Path.Combine(backupRoot, $"{projectName}-{identity}");
    }

    private static string SanitizeFileName(string projectName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeName = new string(projectName.Trim()
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray()).TrimEnd('.', ' ');
        return string.IsNullOrWhiteSpace(safeName) ? "Project" : safeName;
    }

    private static string NormalizeArchivePath(string path) => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    private static void PruneBackups(string backupFolder, int maximumBackupCount)
    {
        var backups = Directory.EnumerateFiles(backupFolder, "*.zip", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();

        foreach (var expiredBackup in backups.Skip(maximumBackupCount))
        {
            expiredBackup.Delete();
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Preserve the original archive creation exception.
        }
    }
}
