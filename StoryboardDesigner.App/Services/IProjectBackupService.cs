namespace StoryboardDesigner.App.Services;

public interface IProjectBackupService
{
    string? CreateBackupIfDue(string projectFilePath, string projectName);
}
