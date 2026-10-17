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
            var packagedRuntimePath = GetPackagedRuntimePath();
            Assert.True(File.Exists(packagedRuntimePath), $"Packaged runtime export is missing: {packagedRuntimePath}");
            var runtimePath = jsonExportService.ExportCleanProjectV1(
                Path.Combine(tempRoot, "LargeRoomTraversals.sbe.json"),
                project);

            Assert.True(File.Exists(runtimePath), $"Temporary runtime export is missing: {runtimePath}");
            AssertTraversalRuntimeExportMatches(runtimePath, packagedRuntimePath, area.Id);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void AssertTraversalRuntimeExportMatches(
        string temporaryRuntimePath,
        string packagedRuntimePath,
        Guid areaId)
    {
        var temporaryRoot = Path.GetDirectoryName(temporaryRuntimePath)
            ?? throw new InvalidOperationException("Unable to resolve temporary runtime export folder.");
        var packagedRoot = Path.GetDirectoryName(packagedRuntimePath)
            ?? throw new InvalidOperationException("Unable to resolve packaged runtime export folder.");

        var areaRuntimeRelativePath = Path.Combine("Area", $"{areaId:D}.runtime.json");
        var temporaryAreaPath = Path.Combine(temporaryRoot, areaRuntimeRelativePath);
        var packagedAreaPath = Path.Combine(packagedRoot, areaRuntimeRelativePath);
        Assert.True(File.Exists(temporaryAreaPath), $"Temporary area runtime export is missing: {temporaryAreaPath}");
        Assert.True(File.Exists(packagedAreaPath), $"Packaged area runtime export is missing: {packagedAreaPath}");

        var packagedJson = JsonNode.Parse(File.ReadAllText(packagedAreaPath));
        var temporaryJson = JsonNode.Parse(File.ReadAllText(temporaryAreaPath));
        var difference = FindFirstJsonDifference(packagedJson, temporaryJson, "$");
        Assert.True(difference is null,
            $"Temporary and packaged area traversal exports differ in '{areaRuntimeRelativePath}': {difference}");
    }

    private static string? FindFirstJsonDifference(JsonNode? packaged, JsonNode? temporary, string path)
    {
        if (packaged is JsonObject packagedObject && temporary is JsonObject temporaryObject)
        {
            var keys = packagedObject.Select(property => property.Key)
                .Concat(temporaryObject.Select(property => property.Key))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal);

            foreach (var key in keys)
            {
                var hasPackaged = packagedObject.TryGetPropertyValue(key, out var packagedValue);
                var hasTemporary = temporaryObject.TryGetPropertyValue(key, out var temporaryValue);
                if (!hasPackaged || !hasTemporary)
                {
                    return $"{path}.{key}: property exists in packaged={hasPackaged}, temporary={hasTemporary}.";
                }

                var nestedDifference = FindFirstJsonDifference(packagedValue, temporaryValue, $"{path}.{key}");
                if (nestedDifference is not null)
                {
                    return nestedDifference;
                }
            }

            return null;
        }

        if (packaged is JsonArray packagedArray && temporary is JsonArray temporaryArray)
        {
            if (packagedArray.Count != temporaryArray.Count)
            {
                return $"{path}: array lengths differ (packaged={packagedArray.Count}, temporary={temporaryArray.Count}).";
            }

            for (var index = 0; index < packagedArray.Count; index++)
            {
                var nestedDifference = FindFirstJsonDifference(packagedArray[index], temporaryArray[index], $"{path}[{index}]");
                if (nestedDifference is not null)
                {
                    return nestedDifference;
                }
            }

            return null;
        }

        if (JsonNode.DeepEquals(packaged, temporary))
        {
            return null;
        }

        return $"{path}: packaged={FormatJsonValue(packaged)}, temporary={FormatJsonValue(temporary)}.";
    }

    private static string FormatJsonValue(JsonNode? value)
    {
        var json = value?.ToJsonString() ?? "null";
        return json.Length <= 160 ? json : json[..157] + "...";
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
