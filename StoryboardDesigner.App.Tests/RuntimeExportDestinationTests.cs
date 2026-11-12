using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class RuntimeExportDestinationTests
{
    [Fact]
    public void ExportRuntimeProject_UsesConfiguredDirectoryAsTheCompleteDestination()
    {
        var root = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        var authoring = Path.Combine(root, "authoring");
        var destination = Path.Combine(root, "runtime output with spaces");
        Directory.CreateDirectory(authoring);
        var projectPath = Path.Combine(authoring, "Example.sbe.json");
        var project = new ProjectModel { Name = "Example", RuntimeExportDirectory = "../runtime output with spaces" };

        try
        {
            var runtimePath = new JsonExportService().ExportCleanProjectV1(projectPath, project);
            Assert.Equal(Path.Combine(destination, "Example.sbr.runtime.json"), runtimePath);
            Assert.True(File.Exists(runtimePath));
            Assert.True(File.Exists(Path.Combine(destination, "runtime-index.html")));
            Assert.False(Directory.Exists(Path.Combine(authoring, "GameRuntimeJson")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RuntimeExportDirectory_RoundTripsInTheAuthoringRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var projectPath = Path.Combine(root, "Example.sbe.json");
        var project = new ProjectModel { Name = "Example", RuntimeExportDirectory = "../runtime" };
        var service = new JsonExportService();
        try
        {
            service.SaveProjectModel(projectPath, project);
            Assert.Equal("../runtime", service.TryLoadProjectModel(projectPath)?.RuntimeExportDirectory);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ConfiguredRuntimeExportDirectory_CannotOverlapAuthoringFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var projectPath = Path.Combine(root, "Example.sbe.json");
        var project = new ProjectModel { Name = "Example", RuntimeExportDirectory = "./generated" };
        try
        {
            Assert.Throws<InvalidOperationException>(() => new JsonExportService().ExportCleanProjectV1(projectPath, project));
            Assert.False(Directory.Exists(Path.Combine(root, "generated")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExportRuntimeProject_RejectsAnotherProjectsConfiguredOutput()
    {
        var root = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        var first = Path.Combine(root, "first");
        var second = Path.Combine(root, "second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        File.WriteAllText(Path.Combine(second, "Other.sbe.json"), "{\"runtimeExportDirectory\":\"../shared-runtime\"}");
        var project = new ProjectModel { Name = "Example", RuntimeExportDirectory = "../shared-runtime" };
        try
        {
            Assert.Throws<InvalidOperationException>(() => new JsonExportService().ExportCleanProjectV1(
                Path.Combine(first, "Example.sbe.json"), project));
            Assert.False(Directory.Exists(Path.Combine(root, "shared-runtime")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
