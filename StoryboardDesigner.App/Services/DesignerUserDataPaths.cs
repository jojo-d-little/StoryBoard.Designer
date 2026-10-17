using System.IO;

namespace StoryboardDesigner.App.Services;

internal static class DesignerUserDataPaths
{
    private const string UserDataRootEnvironmentVariable = "STORYBOARD_DESIGNER_USER_DATA_ROOT";

    public static string GetFilePath(string fileName)
    {
        var userDataRoot = Environment.GetEnvironmentVariable(UserDataRootEnvironmentVariable);
        var root = string.IsNullOrWhiteSpace(userDataRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StoryboardDesigner")
            : Path.GetFullPath(userDataRoot);

        return Path.Combine(root, fileName);
    }
}
