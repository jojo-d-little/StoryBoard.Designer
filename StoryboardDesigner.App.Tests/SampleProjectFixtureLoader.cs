using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

internal static class SampleProjectFixtureLoader
{
    public static ProjectModel LoadSingleRoomFixture()
    {
        var root = FindRepositoryRoot();
        var projectFilePath = Path.Combine(
            root,
            "StoryboardDesigner.App.Tests",
            "SampleProjectData",
            "single room",
            "single-room.sbe.json");

        var jsonService = new JsonExportService();
        var project = jsonService.TryLoadProjectModel(projectFilePath)
            ?? throw new InvalidOperationException($"Unable to load fixture project from '{projectFilePath}'.");

        ScopeHierarchy.AttachParents(project);
        return project;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var marker = Path.Combine(current.FullName, "StoryboardDesigner.slnx");
            if (File.Exists(marker))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root from test execution directory.");
    }
}
