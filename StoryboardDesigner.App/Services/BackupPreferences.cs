namespace StoryboardDesigner.App.Services;

public sealed class BackupPreferences
{
    public const int DefaultIntervalHours = 12;
    public const int DefaultMaximumBackupCount = 7;

    public int BackupIntervalHours { get; set; } = DefaultIntervalHours;

    public int MaximumBackupCount { get; set; } = DefaultMaximumBackupCount;

    /// <summary>
    /// Empty means the default local application data backup folder.
    /// </summary>
    public string BackupRootPath { get; set; } = string.Empty;
}
