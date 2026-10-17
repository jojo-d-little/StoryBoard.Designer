using System.Text.Json.Nodes;
using StoryboardDesigner.App.Services;
using Direction10 = Storyboard.Shared.RuntimeContracts.Enums.Direction10;

namespace StoryboardDesigner.App.Tests;

public sealed class AreaMapMultiCellTraversalRuntimeSmokeTests
{
    private static readonly Guid TripleWideId = Guid.Parse("5ba31fc8-19f5-4aa9-8aea-94319aa5bcd2");
    private static readonly Guid AreWeThereYetId = Guid.Parse("5ab1177b-da1e-4e2c-a310-134a67f2caf5");

    [Fact]
    public void LargeRoomTraversals_SavedProjectLoadsAndCanBeReExported()
    {
        var projectFilePath = GetAuthoredProjectPath();
        Assert.True(File.Exists(projectFilePath), $"Authored sample project is missing: {projectFilePath}");

        var jsonExportService = new JsonExportService();
        var project = jsonExportService.TryLoadProjectModel(projectFilePath);
        Assert.NotNull(project);

        var area = Assert.Single(project!.Planets.Single().Countries.Single().Areas);
        var tripleWide = Assert.Single(area.Rooms, room => room.Id == TripleWideId);
        var areWeThereYet = Assert.Single(area.Rooms, room => room.Id == AreWeThereYetId);
        Assert.Contains(area.TraversalConnections, connection =>
            connection.BaseTraversalDirectionFromA == Direction10.South
            && connection.RoomAId == tripleWide.Id
            && connection.RoomBId == areWeThereYet.Id);

        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var runtimePath = jsonExportService.ExportCleanProjectV1(
                Path.Combine(tempRoot, "LargeRoomTraversals.sbe.json"),
                project);

            Assert.True(File.Exists(runtimePath), $"Temporary runtime export is missing: {runtimePath}");
            var packagedRuntimePath = GetPackagedRuntimePath();
            Assert.True(File.Exists(packagedRuntimePath), $"Packaged runtime export is missing: {packagedRuntimePath}");
            AssertRuntimeExportsMatch(runtimePath, packagedRuntimePath);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void AssertRuntimeExportsMatch(string temporaryRuntimePath, string packagedRuntimePath)
    {
        var temporaryRoot = Path.GetDirectoryName(temporaryRuntimePath)
            ?? throw new InvalidOperationException("Unable to resolve temporary runtime export folder.");
        var packagedRoot = Path.GetDirectoryName(packagedRuntimePath)
            ?? throw new InvalidOperationException("Unable to resolve packaged runtime export folder.");

        var temporaryFiles = GetRelativeJsonFiles(temporaryRoot);
        var packagedFiles = GetRelativeJsonFiles(packagedRoot);
        Assert.Equal(packagedFiles.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase),
            temporaryFiles.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

        foreach (var relativePath in packagedFiles.Keys)
        {
            var packagedJson = JsonNode.Parse(File.ReadAllText(packagedFiles[relativePath]));
            var temporaryJson = JsonNode.Parse(File.ReadAllText(temporaryFiles[relativePath]));
            Assert.True(
                JsonNode.DeepEquals(packagedJson, temporaryJson),
                $"Temporary and packaged runtime exports differ in '{relativePath}'.");
        }
    }

    private static Dictionary<string, string> GetRelativeJsonFiles(string root)
    {
        return Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).EndsWith(
                ".sbr.runtime.direct-command-echoes.json",
                StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                path => Path.GetRelativePath(root, path),
                path => path,
                StringComparer.OrdinalIgnoreCase);
    }

    private static string GetAuthoredProjectPath() => Path.Combine(
        AppContext.BaseDirectory,
        "Samples",
        "LargeRoomTraversals",
        "LargeRoomTraversals.sbe.json");

    private static string GetPackagedRuntimePath() => Path.Combine(
        AppContext.BaseDirectory,
        "Samples",
        "LargeRoomTraversals",
        "GameRuntimeJson",
        "LargeRoomTraversals.sbr.runtime.json");
}
