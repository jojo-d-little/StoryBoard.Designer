namespace StoryboardDesigner.App.Services;

internal sealed class NullProjectBackupService : IProjectBackupService
{
    public string? CreateBackupIfDue(string projectFilePath, string projectName) => null;
}
