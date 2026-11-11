using System.IO.Compression;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class ProjectBackupServiceTests
{
    [Fact]
    public void Load_WhenPreferencesFileIsMissing_UsesDefaultBackupFolder()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"StoryboardDesigner.BackupPreferences.{Guid.NewGuid():N}", "backup.preferences.json");

        var preferences = new BackupPreferencesService(settingsPath).Load();

        Assert.Equal(BackupPreferences.DefaultIntervalHours, preferences.BackupIntervalHours);
        Assert.Equal(BackupPreferences.DefaultMaximumBackupCount, preferences.MaximumBackupCount);
        Assert.Equal(BackupPreferencesService.DefaultBackupRootPath, preferences.BackupRootPath);
    }

    [Fact]
    public void CreateBackupIfDue_CreatesZipContainingProjectFiles_AndHonorsInterval()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"StoryboardDesigner.ProjectBackupTests.{Guid.NewGuid():N}");
        var projectRoot = CreateProject(tempRoot);
        var backupRoot = Path.Combine(tempRoot, "Backups");
        var preferencesService = new BackupPreferencesService(Path.Combine(tempRoot, "backup.preferences.json"));
        preferencesService.Save(new BackupPreferences
        {
            BackupIntervalHours = 12,
            MaximumBackupCount = 7,
            BackupRootPath = backupRoot
        });
        var backupService = new ProjectBackupService(preferencesService);

        try
        {
            var backupPath = backupService.CreateBackupIfDue(Path.Combine(projectRoot, "SampleProject.sbe.json"), "The Docks");

            Assert.NotNull(backupPath);
            Assert.True(File.Exists(backupPath));
            Assert.StartsWith("The Docks-", new DirectoryInfo(Path.GetDirectoryName(backupPath!)!).Name);
            Assert.StartsWith("The Docks_", Path.GetFileName(backupPath!));
            using (var archive = ZipFile.OpenRead(backupPath!))
            {
                Assert.Contains(archive.Entries, entry => entry.FullName == "src/SampleProject.sbe.json");
                Assert.Contains(archive.Entries, entry => entry.FullName == "src/assets/cover.txt");
            }

            Assert.Null(backupService.CreateBackupIfDue(Path.Combine(projectRoot, "SampleProject.sbe.json"), "The Docks"));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void CreateBackupIfDue_PrunesOldestArchivesToConfiguredMaximum()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"StoryboardDesigner.ProjectBackupTests.{Guid.NewGuid():N}");
        var projectRoot = CreateProject(tempRoot);
        var backupRoot = Path.Combine(tempRoot, "Backups");
        var preferencesService = new BackupPreferencesService(Path.Combine(tempRoot, "backup.preferences.json"));
        preferencesService.Save(new BackupPreferences
        {
            BackupIntervalHours = 2,
            MaximumBackupCount = 2,
            BackupRootPath = backupRoot
        });
        var backupService = new ProjectBackupService(preferencesService);
        var projectFilePath = Path.Combine(projectRoot, "SampleProject.sbe.json");

        try
        {
            var firstBackup = backupService.CreateBackupIfDue(projectFilePath, "Sample Project");
            Assert.NotNull(firstBackup);
            File.SetLastWriteTimeUtc(firstBackup!, DateTime.UtcNow.AddHours(-4));

            var secondBackup = backupService.CreateBackupIfDue(projectFilePath, "Sample Project");
            Assert.NotNull(secondBackup);
            File.SetLastWriteTimeUtc(secondBackup!, DateTime.UtcNow.AddHours(-3));

            var thirdBackup = backupService.CreateBackupIfDue(projectFilePath, "Sample Project");

            Assert.NotNull(thirdBackup);
            var projectBackupFolder = Directory.GetDirectories(backupRoot).Single();
            Assert.Equal(2, Directory.GetFiles(projectBackupFolder, "*.zip").Length);
            Assert.False(File.Exists(firstBackup));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateProject(string tempRoot)
    {
        var projectRoot = Path.Combine(tempRoot, "src");
        Directory.CreateDirectory(Path.Combine(projectRoot, "assets"));
        File.WriteAllText(Path.Combine(projectRoot, "SampleProject.sbe.json"), "{\"name\":\"SampleProject\"}");
        File.WriteAllText(Path.Combine(projectRoot, "assets", "cover.txt"), "asset");
        return projectRoot;
    }
}
