using System.IO;
using System.Text.Json;

namespace StoryboardDesigner.App.Services;

public sealed class BackupPreferencesService
{
    private readonly JsonSerializerOptions _jsonOptions = FoundationStoryboardJsonSerializerOptions.Create();
    private readonly string _settingsPath;

    public BackupPreferencesService(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? DesignerUserDataPaths.GetFilePath("backup.preferences.json");
    }

    public static string DefaultBackupRootPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StoryboardDesigner",
        "Backups");

    public BackupPreferences Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new BackupPreferences { BackupRootPath = DefaultBackupRootPath };
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<BackupPreferences>(File.ReadAllText(_settingsPath), _jsonOptions)
                ?? new BackupPreferences();
            if (loaded.BackupIntervalHours is < 2 or > 24)
            {
                loaded.BackupIntervalHours = BackupPreferences.DefaultIntervalHours;
            }

            if (loaded.MaximumBackupCount < 1)
            {
                loaded.MaximumBackupCount = BackupPreferences.DefaultMaximumBackupCount;
            }

            loaded.BackupRootPath = string.IsNullOrWhiteSpace(loaded.BackupRootPath)
                ? DefaultBackupRootPath
                : Path.GetFullPath(loaded.BackupRootPath);
            return loaded;
        }
        catch
        {
            return new BackupPreferences { BackupRootPath = DefaultBackupRootPath };
        }
    }

    public void Save(BackupPreferences preferences)
    {
        if (preferences.BackupIntervalHours is < 2 or > 24)
        {
            throw new ArgumentOutOfRangeException(nameof(preferences), "Backup interval must be between 2 and 24 hours.");
        }

        if (preferences.MaximumBackupCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(preferences), "At least one backup must be retained.");
        }

        var normalized = new BackupPreferences
        {
            BackupIntervalHours = preferences.BackupIntervalHours,
            MaximumBackupCount = preferences.MaximumBackupCount,
            BackupRootPath = string.IsNullOrWhiteSpace(preferences.BackupRootPath)
                ? DefaultBackupRootPath
                : Path.GetFullPath(preferences.BackupRootPath.Trim())
        };

        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(normalized, _jsonOptions));
    }
}
