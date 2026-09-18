using System.Text;
using System.Text.Json.Nodes;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class SampleProjectRoundTripPersistenceTests
{
    public static IEnumerable<object[]> SampleProjectPaths()
    {
        yield return new object[] { Path.Combine("Samples", "BaseItemTesting", "BaseItemTesting.sbe.json") };
        yield return new object[] { Path.Combine("Samples", "Birmingham", "Birmingham.sbe.json") };
        yield return new object[] { Path.Combine("Samples", "ChessDemo", "ChessDemo.sbe.json") };
        yield return new object[] { Path.Combine("Samples", "MapDemo1", "MapDemo1.sbe.json") };
        yield return new object[] { Path.Combine("Samples", "ObjectPlayLevel1", "ObjectPlayLevel1.sbe.json") };
        yield return new object[] { Path.Combine("Samples", "ObjectPlayLevel2", "ObjectPlayLevel2.sbe.json") };
        yield return new object[] { Path.Combine("Samples", "ObjectPlayLevel3", "ObjectPlayLevel3.sbe.json") };
        yield return new object[] { Path.Combine("Samples", "RoomDesigner1", "RoomDesigner1.sbe.json") };
        yield return new object[] { Path.Combine("Samples", "TraversalExamples", "TraversalExamples.sbe.json") };
        yield return new object[] { Path.Combine("Samples", "WorkshopTutorial", "WorkshopTutorial.sbe.json") };
    }

    [Theory]
    [MemberData(nameof(SampleProjectPaths))]
    public void SampleProject_LoadAndRoundTripSave_IsSemanticallyStable(string relativeProjectPath)
    {
        var repoRoot = FindRepositoryRoot();
        var sourceProjectPath = Path.Combine(repoRoot, relativeProjectPath);

        Assert.True(File.Exists(sourceProjectPath), $"Sample project file is missing: {sourceProjectPath}");

        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", "SampleRoundTrip", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sourceProjectFolder = Path.GetDirectoryName(sourceProjectPath)
                ?? throw new InvalidOperationException("Unable to resolve source project folder.");
            CopyDirectoryRecursive(sourceProjectFolder, tempRoot);

            var destinationProjectPath = Path.Combine(tempRoot, Path.GetFileName(sourceProjectPath));

            var service = new JsonExportService();
            var loaded = service.TryLoadProjectModel(destinationProjectPath);
            Assert.NotNull(loaded);

            service.SaveProjectModel(destinationProjectPath, loaded!);
            var firstSaveJson = File.ReadAllText(destinationProjectPath, Encoding.UTF8);

            var reloaded = service.TryLoadProjectModel(destinationProjectPath);
            Assert.NotNull(reloaded);

            service.SaveProjectModel(destinationProjectPath, reloaded!);
            var secondSaveJson = File.ReadAllText(destinationProjectPath, Encoding.UTF8);

            Assert.True(
                JsonSemanticallyEqual(firstSaveJson, secondSaveJson),
                $"Round-trip semantic mismatch for sample '{relativeProjectPath}'.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static bool JsonSemanticallyEqual(string left, string right)
    {
        var leftNode = JsonNode.Parse(left);
        var rightNode = JsonNode.Parse(right);

        return JsonNode.DeepEquals(leftNode, rightNode);
    }

    private static void CopyDirectoryRecursive(string sourceFolder, string destinationFolder)
    {
        Directory.CreateDirectory(destinationFolder);

        foreach (var filePath in Directory.EnumerateFiles(sourceFolder))
        {
            var destinationPath = Path.Combine(destinationFolder, Path.GetFileName(filePath));
            File.Copy(filePath, destinationPath, overwrite: true);
        }

        foreach (var childFolder in Directory.EnumerateDirectories(sourceFolder))
        {
            var destinationChild = Path.Combine(destinationFolder, Path.GetFileName(childFolder));
            CopyDirectoryRecursive(childFolder, destinationChild);
        }
    }

    private static string FindRepositoryRoot()
    {
        return AppContext.BaseDirectory;
    }
}
