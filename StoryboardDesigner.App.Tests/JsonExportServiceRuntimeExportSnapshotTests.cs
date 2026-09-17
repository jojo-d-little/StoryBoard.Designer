using System.Text;
using System.Text.Json;
using StoryboardDesigner.App.Services;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Tests;

public class JsonExportServiceRuntimeExportSnapshotTests
{
    private const string RuntimeExportFolderName = "GameRuntimeJson";

    [Fact]
    public void ExportRuntimeProjectV1_EmitsGlobalAvailableGameActions_WhenConfigured()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var project = new StoryboardDesigner.App.Models.ProjectModel
            {
                Name = "GlobalActionExport",
                CommandVerbs = ["inspect"],
                GlobalObjectScopeName = "Global Objects",
                GlobalObjectAvailableActions =
                [
                    new StoryboardDesigner.App.Models.CommandAction
                    {
                        Name = "Inspect",
                        ActionType = CommandActionType.EchoMessage,
                        Verbs = ["inspect"],
                        EchoMessage = "global",
                        Payload = new StoryboardDesigner.App.Models.EchoPayload()
                    }
                ]
            };

            var projectFilePath = Path.Combine(tempRoot, "GlobalActionExport.sbe.json");
            service.ExportCleanProjectV1(projectFilePath, project);

            using var cleanProjectDoc = JsonDocument.Parse(File.ReadAllText(BuildRuntimeProjectPath(projectFilePath), Encoding.UTF8));
            var actions = cleanProjectDoc.RootElement.GetProperty("availableGameActions");
            Assert.Equal(1, actions.GetArrayLength());
            Assert.Equal("Inspect", actions[0].GetProperty("name").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_MatchesSingleRoomSnapshotBaseline()
    {
        var service = new JsonExportService();
        var repoRoot = FindRepositoryRoot();
        var sourceProjectPath = Path.Combine(
            repoRoot,
            "StoryboardDesigner.App.Tests",
            "SampleProjectData",
            "single room",
            "single-room.sbe.json");

        var project = service.TryLoadProjectModel(sourceProjectPath);
        Assert.NotNull(project);

        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var exportProjectPath = Path.Combine(tempRoot, "single-room.sbe.json");
            service.ExportCleanProjectV1(exportProjectPath, project!);

            var generated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["single-room.sbr.runtime.json"] = File.ReadAllText(BuildRuntimeProjectPath(exportProjectPath), Encoding.UTF8)
            };

            foreach (var areaFile in Directory.EnumerateFiles(BuildRuntimeAreaFolderPath(exportProjectPath), "*.runtime.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                generated[$"Area/{Path.GetFileName(areaFile)}"] = File.ReadAllText(areaFile, Encoding.UTF8);
            }

            foreach (var roomFile in Directory.EnumerateFiles(BuildRuntimeRoomFolderPath(exportProjectPath), "*.runtime.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                generated[$"Room/{Path.GetFileName(roomFile)}"] = File.ReadAllText(roomFile, Encoding.UTF8);
            }

            using (var cleanProjectDoc = JsonDocument.Parse(generated["single-room.sbr.runtime.json"]))
            {
                Assert.False(cleanProjectDoc.RootElement.TryGetProperty("playerCharacterObjectName", out _));
                Assert.False(cleanProjectDoc.RootElement.TryGetProperty("playerGameProperties", out _));
                Assert.False(cleanProjectDoc.RootElement.TryGetProperty("playerAvailableGameActions", out _));
                Assert.False(cleanProjectDoc.RootElement.TryGetProperty("playerGameObjects", out _));
                Assert.False(cleanProjectDoc.RootElement.TryGetProperty("gameObjects", out _));
                Assert.False(cleanProjectDoc.RootElement.TryGetProperty("globalGameObjects", out _));
                Assert.False(cleanProjectDoc.RootElement.TryGetProperty("planets", out _));
                Assert.True(cleanProjectDoc.RootElement.TryGetProperty("planetIds", out _));
            }

            var snapshotRoot = Path.Combine(repoRoot, "StoryboardDesigner.App.Tests", "Snapshots", "RuntimeExportV1", "single-room");
            var updateSnapshots = string.Equals(
                Environment.GetEnvironmentVariable("UPDATE_RUNTIME_EXPORT_SNAPSHOTS"),
                "1",
                StringComparison.OrdinalIgnoreCase);

            if (updateSnapshots)
            {
                Directory.CreateDirectory(snapshotRoot);
                foreach (var pair in generated)
                {
                    var targetPath = Path.Combine(snapshotRoot, pair.Key.Replace('/', Path.DirectorySeparatorChar));
                    var targetFolder = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrWhiteSpace(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                    }

                    File.WriteAllText(targetPath, pair.Value, Encoding.UTF8);
                }

                return;
            }

            foreach (var pair in generated)
            {
                var expectedPath = Path.Combine(snapshotRoot, pair.Key.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(expectedPath),
                    $"Missing snapshot file '{expectedPath}'. Run tests with UPDATE_RUNTIME_EXPORT_SNAPSHOTS=1 to generate baselines.");

                var expected = File.ReadAllText(expectedPath, Encoding.UTF8);
                Assert.Equal(expected, pair.Value);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportRuntimeProjectV1_MatchesBirminghamSnapshotBaseline()
    {
        var service = new JsonExportService();
        var repoRoot = FindRepositoryRoot();
        var sourceProjectPath = Path.Combine(
            repoRoot,
            "Samples",
            "Birmingham",
            "Birmingham.sbe.json");

        var project = service.TryLoadProjectModel(sourceProjectPath);
        Assert.NotNull(project);

        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var exportProjectPath = Path.Combine(tempRoot, "Birmingham.sbe.json");
            service.ExportCleanProjectV1(exportProjectPath, project!);

            var generated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Birmingham.sbr.runtime.json"] = File.ReadAllText(BuildRuntimeProjectPath(exportProjectPath), Encoding.UTF8)
            };

            foreach (var areaFile in Directory.EnumerateFiles(BuildRuntimeAreaFolderPath(exportProjectPath), "*.runtime.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                generated[$"Area/{Path.GetFileName(areaFile)}"] = File.ReadAllText(areaFile, Encoding.UTF8);
            }

            using (var cleanProjectDoc = JsonDocument.Parse(generated["Birmingham.sbr.runtime.json"]))
            {
                Assert.False(cleanProjectDoc.RootElement.TryGetProperty("playerCharacterObjectName", out _));
                Assert.False(cleanProjectDoc.RootElement.TryGetProperty("playerGameProperties", out _));
                Assert.False(cleanProjectDoc.RootElement.TryGetProperty("playerAvailableGameActions", out _));
                Assert.False(cleanProjectDoc.RootElement.TryGetProperty("playerGameObjects", out _));
                Assert.False(cleanProjectDoc.RootElement.TryGetProperty("gameObjects", out _));
                Assert.False(cleanProjectDoc.RootElement.TryGetProperty("globalGameObjects", out _));
                Assert.False(cleanProjectDoc.RootElement.TryGetProperty("planets", out _));
                Assert.True(cleanProjectDoc.RootElement.TryGetProperty("planetIds", out _));
            }

            var snapshotRoot = Path.Combine(repoRoot, "StoryboardDesigner.App.Tests", "Snapshots", "RuntimeExportV1", "birmingham");
            var updateSnapshots = string.Equals(
                Environment.GetEnvironmentVariable("UPDATE_RUNTIME_EXPORT_SNAPSHOTS"),
                "1",
                StringComparison.OrdinalIgnoreCase);

            if (updateSnapshots)
            {
                Directory.CreateDirectory(snapshotRoot);
                foreach (var pair in generated)
                {
                    var targetPath = Path.Combine(snapshotRoot, pair.Key.Replace('/', Path.DirectorySeparatorChar));
                    var targetFolder = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrWhiteSpace(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                    }

                    File.WriteAllText(targetPath, pair.Value, Encoding.UTF8);
                }

                return;
            }

            foreach (var pair in generated)
            {
                var expectedPath = Path.Combine(snapshotRoot, pair.Key.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(expectedPath),
                    $"Missing snapshot file '{expectedPath}'. Run tests with UPDATE_RUNTIME_EXPORT_SNAPSHOTS=1 to generate baselines.");

                var expected = File.ReadAllText(expectedPath, Encoding.UTF8);
                Assert.Equal(expected, pair.Value);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string BuildRuntimeProjectPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = BuildRuntimeProjectBaseName(projectFilePath);
        return Path.Combine(folder, RuntimeExportFolderName, $"{baseName}.sbr.runtime.json");
    }

    private static string BuildRuntimeAreaFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName, "Area");
    }

    private static string BuildRuntimeRoomFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName, "Room");
    }

    private static string BuildRuntimeProjectBaseName(string projectFilePath)
    {
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return baseName.EndsWith(".sbe", StringComparison.OrdinalIgnoreCase)
            ? baseName[..^4]
            : baseName;
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
