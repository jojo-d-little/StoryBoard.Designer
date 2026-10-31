using System.Text.Json.Nodes;
using StoryboardDesigner.App.Services;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace StoryboardDesigner.App.Tests;

public sealed class ComputedPropertyProjectRoundTripValidatorTests
{
    private const string ProjectPathsEnvironmentVariable = "STORYBOARD_COMPUTED_PROPERTY_ROUNDTRIP_PROJECTS";
    private readonly ITestOutputHelper _output;

    public ComputedPropertyProjectRoundTripValidatorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IEnumerable<object[]> ProjectPaths => ReadConfiguredProjectPaths()
        .DefaultIfEmpty(string.Empty)
        .Select(static path => new object[] { path });

    [Theory]
    [MemberData(nameof(ProjectPaths))]
    public void MigratedComputedProperties_RoundTripThroughDesignerServices(string sourceProjectFilePath)
    {
        if (string.IsNullOrWhiteSpace(sourceProjectFilePath))
        {
            throw SkipException.ForSkip($"Set {ProjectPathsEnvironmentVariable} to project file paths to run the focused Stage 05 validator.");
        }

        sourceProjectFilePath = Path.GetFullPath(sourceProjectFilePath);
        var projectName = Path.GetFileNameWithoutExtension(sourceProjectFilePath);
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "StoryboardDesigner.Tests",
            "ComputedPropertyRoundTrip",
            Guid.NewGuid().ToString("N"));

        try
        {
            Assert.True(File.Exists(sourceProjectFilePath), $"[{projectName}] Project file does not exist: {sourceProjectFilePath}");
            var sourceProjectFolder = Path.GetDirectoryName(sourceProjectFilePath)
                ?? throw new InvalidOperationException($"[{projectName}] Project path has no parent folder.");
            var isolatedProjectFolder = Path.Combine(tempRoot, Path.GetFileName(sourceProjectFolder));
            CopyDirectoryRecursive(sourceProjectFolder, isolatedProjectFolder);
            var isolatedProjectFilePath = Path.Combine(isolatedProjectFolder, Path.GetFileName(sourceProjectFilePath));

            var initialExpressions = ReadAuthoredExpressions(isolatedProjectFolder, projectName);
            Assert.NotEmpty(initialExpressions);

            var service = new JsonExportService();
            var loadedProject = service.TryLoadProjectModel(isolatedProjectFilePath);
            Assert.NotNull(loadedProject);

            service.SaveProjectModel(isolatedProjectFilePath, loadedProject!);
            AssertExpressionSnapshotsEqual(
                initialExpressions,
                ReadAuthoredExpressions(isolatedProjectFolder, projectName),
                projectName,
                "after save");

            var cleanExportPath = service.ExportCleanProjectV1(isolatedProjectFilePath, loadedProject!);
            Assert.True(File.Exists(cleanExportPath), $"[{projectName}] Clean export root file was not written: {cleanExportPath}");
            var cleanExportFolder = Path.Combine(isolatedProjectFolder, "GameRuntimeJson");
            var exportedExpressions = ReadV2ExpressionsFromFolder(cleanExportFolder, projectName, "clean export");
            AssertExpressionsPreservedInExport(initialExpressions, exportedExpressions, projectName);

            var reloadedProject = service.TryLoadProjectModel(isolatedProjectFilePath);
            Assert.NotNull(reloadedProject);
            service.SaveProjectModel(isolatedProjectFilePath, reloadedProject!);
            AssertExpressionSnapshotsEqual(
                initialExpressions,
                ReadAuthoredExpressions(isolatedProjectFolder, projectName),
                projectName,
                "after reload and save");

            _output.WriteLine($"PASS [{projectName}] {initialExpressions.Count} authored V2 computation(s) survived Designer load, save, clean export, and reload. Isolated copy: {isolatedProjectFolder}");
        }
        catch (Exception exception)
        {
            _output.WriteLine($"FAIL [{projectName}] {exception}");
            throw;
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static IEnumerable<string> ReadConfiguredProjectPaths()
    {
        var configured = Environment.GetEnvironmentVariable(ProjectPathsEnvironmentVariable);
        return string.IsNullOrWhiteSpace(configured)
            ? Array.Empty<string>()
            : configured.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static List<ExpressionSnapshot> ReadAuthoredExpressions(string projectFolder, string projectName)
    {
        var snapshots = new List<ExpressionSnapshot>();
        foreach (var path in EnumerateAuthoringJsonFiles(projectFolder))
        {
            CollectExpressions(JsonNode.Parse(File.ReadAllText(path)), path, projectName, snapshots, requireV2Only: true);
        }

        return snapshots;
    }

    private static List<ExpressionSnapshot> ReadV2ExpressionsFromFolder(string folder, string projectName, string description)
    {
        Assert.True(Directory.Exists(folder), $"[{projectName}] {description} folder does not exist: {folder}");
        var snapshots = new List<ExpressionSnapshot>();
        foreach (var path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            CollectExpressions(JsonNode.Parse(File.ReadAllText(path)), path, projectName, snapshots, requireV2Only: false);
        }

        return snapshots;
    }

    private static IEnumerable<string> EnumerateAuthoringJsonFiles(string projectFolder)
    {
        foreach (var path in Directory.EnumerateFiles(projectFolder, "*.json", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(projectFolder, path);
            var pathSegments = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
            if (pathSegments.Any(static segment =>
                    string.Equals(segment, "GameRuntimeJson", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(segment, "Variants", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            yield return path;
        }
    }

    private static void CollectExpressions(
        JsonNode? node,
        string sourcePath,
        string projectName,
        List<ExpressionSnapshot> snapshots,
        bool requireV2Only)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                var hasV2 = jsonObject.TryGetPropertyValue("computationV2", out var computationV2)
                    && computationV2 is not null;
                var hasLegacy = jsonObject.TryGetPropertyValue("computation", out var legacyComputation)
                    && legacyComputation is not null;
                var isGameProperty = jsonObject.ContainsKey("name")
                    && jsonObject.ContainsKey("defaultValue")
                    && jsonObject.ContainsKey("valueRestriction");

                if (isGameProperty && requireV2Only && hasLegacy)
                {
                    var legacyPropertyName = jsonObject["name"]?.GetValue<string>() ?? "<unnamed>";
                    throw new InvalidOperationException($"[{projectName}] Authored property '{legacyPropertyName}' in '{sourcePath}' still has legacy 'computation'.");
                }

                if (isGameProperty && hasV2)
                {
                    if (hasLegacy)
                    {
                        var legacyPropertyName = jsonObject["name"]?.GetValue<string>() ?? "<unnamed>";
                        throw new InvalidOperationException($"[{projectName}] Property '{legacyPropertyName}' in '{sourcePath}' contains both 'computationV2' and legacy 'computation'.");
                    }

                    var expression = computationV2!["expression"];
                    if (expression is null)
                    {
                        var missingExpressionPropertyName = jsonObject["name"]?.GetValue<string>() ?? "<unnamed>";
                        throw new InvalidOperationException($"[{projectName}] Property '{missingExpressionPropertyName}' in '{sourcePath}' has no V2 expression.");
                    }

                    var identity = jsonObject["id"]?.GetValue<string>()
                        ?? jsonObject["name"]?.GetValue<string>()
                        ?? "<unnamed>";
                    var name = jsonObject["name"]?.GetValue<string>() ?? "<unnamed>";
                    snapshots.Add(new ExpressionSnapshot(identity, name, Canonicalize(expression)));
                }

                foreach (var child in jsonObject.Select(static property => property.Value))
                {
                    CollectExpressions(child, sourcePath, projectName, snapshots, requireV2Only);
                }
                break;

            case JsonArray jsonArray:
                foreach (var child in jsonArray)
                {
                    CollectExpressions(child, sourcePath, projectName, snapshots, requireV2Only);
                }
                break;
        }
    }

    private static string Canonicalize(JsonNode node)
    {
        return node switch
        {
            JsonObject jsonObject => "{" + string.Join(",", jsonObject
                .OrderBy(static property => property.Key, StringComparer.Ordinal)
                .Select(static property => System.Text.Json.JsonSerializer.Serialize(property.Key) + ":" + Canonicalize(property.Value!))) + "}",
            JsonArray jsonArray => "[" + string.Join(",", jsonArray.Select(static item => Canonicalize(item!))) + "]",
            _ => node.ToJsonString()
        };
    }

    private static void AssertExpressionSnapshotsEqual(
        IReadOnlyList<ExpressionSnapshot> expected,
        IReadOnlyList<ExpressionSnapshot> actual,
        string projectName,
        string stage)
    {
        var expectedSet = expected.Select(static item => item.ToString()).OrderBy(static item => item, StringComparer.Ordinal).ToArray();
        var actualSet = actual.Select(static item => item.ToString()).OrderBy(static item => item, StringComparer.Ordinal).ToArray();
        Assert.True(
            expectedSet.SequenceEqual(actualSet, StringComparer.Ordinal),
            $"[{projectName}] Authored V2 computation identities or expressions changed {stage}. Expected {expected.Count}, found {actual.Count}.");
    }

    private static void AssertExpressionsPreservedInExport(
        IReadOnlyList<ExpressionSnapshot> authored,
        IReadOnlyList<ExpressionSnapshot> exported,
        string projectName)
    {
        var expectedCounts = authored.GroupBy(static item => item.Expression, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        var exportedCounts = exported.GroupBy(static item => item.Expression, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

        var missing = expectedCounts
            .Where(pair => !exportedCounts.TryGetValue(pair.Key, out var count) || count < pair.Value)
            .Select(static pair => pair.Value)
            .Sum();
        Assert.True(
            missing == 0,
            $"[{projectName}] Clean export is missing or changed {missing} authored V2 expression(s). Authored: {authored.Count}; exported V2 expressions: {exported.Count}.");
    }

    private static void CopyDirectoryRecursive(string sourceFolder, string destinationFolder)
    {
        Directory.CreateDirectory(destinationFolder);
        foreach (var filePath in Directory.EnumerateFiles(sourceFolder))
        {
            File.Copy(filePath, Path.Combine(destinationFolder, Path.GetFileName(filePath)), overwrite: true);
        }

        foreach (var childFolder in Directory.EnumerateDirectories(sourceFolder))
        {
            CopyDirectoryRecursive(childFolder, Path.Combine(destinationFolder, Path.GetFileName(childFolder)));
        }
    }

    private sealed record ExpressionSnapshot(string Identity, string Name, string Expression)
    {
        public override string ToString() => $"{Identity}|{Name}|{Expression}";
    }
}
