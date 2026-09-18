using System.Text.Json;

namespace StoryboardDesigner.App.Tests;

public sealed class SampleRoomRenderOrderMigrationGuardrailTests
{
    [Fact]
    public void SampleRoomArtifacts_WithRenderZOrderAlsoIncludeAuthoredRenderOrder()
    {
        var samplesRoot = Path.Combine(FindRepositoryRoot(), "Samples");
        Assert.True(Directory.Exists(samplesRoot), $"Samples folder is missing: {samplesRoot}");

        var roomFiles = EnumerateCanonicalRoomArtifactFiles(samplesRoot);

        Assert.NotEmpty(roomFiles);

        var failures = new List<string>();

        foreach (var roomFile in roomFiles)
        {
            var json = File.ReadAllText(roomFile);
            using var document = JsonDocument.Parse(json);

            foreach (var (jsonPath, element) in EnumerateJsonObjects(document.RootElement, "$"))
            {
                if (!element.TryGetProperty("renderZOrder", out _))
                {
                    continue;
                }

                if (!element.TryGetProperty("authoredRenderOrder", out _))
                {
                    var relativePath = Path.GetRelativePath(samplesRoot, roomFile);
                    failures.Add($"{relativePath} missing authoredRenderOrder at {jsonPath}");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void SampleRoomArtifacts_HeightPinningFieldsStayPairedWhenPresent()
    {
        var samplesRoot = Path.Combine(FindRepositoryRoot(), "Samples");
        Assert.True(Directory.Exists(samplesRoot), $"Samples folder is missing: {samplesRoot}");

        var roomFiles = EnumerateCanonicalRoomArtifactFiles(samplesRoot);

        Assert.NotEmpty(roomFiles);

        var failures = new List<string>();

        foreach (var roomFile in roomFiles)
        {
            var json = File.ReadAllText(roomFile);
            using var document = JsonDocument.Parse(json);

            foreach (var (jsonPath, element) in EnumerateJsonObjects(document.RootElement, "$"))
            {
                var hasAuthoredBaseHeight = element.TryGetProperty("authoredBaseHeightInRoom", out _);
                var hasIsHeightPinned = element.TryGetProperty("isHeightPinned", out _);

                if (hasAuthoredBaseHeight == hasIsHeightPinned)
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(samplesRoot, roomFile);
                failures.Add($"{relativePath} has unpaired height pinning fields at {jsonPath}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static IEnumerable<(string JsonPath, JsonElement Element)> EnumerateJsonObjects(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return (path, element);

            foreach (var property in element.EnumerateObject())
            {
                foreach (var child in EnumerateJsonObjects(property.Value, $"{path}.{property.Name}"))
                {
                    yield return child;
                }
            }

            yield break;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            foreach (var child in EnumerateJsonObjects(item, $"{path}[{index}]"))
            {
                yield return child;
            }

            index++;
        }
    }

    private static List<string> EnumerateCanonicalRoomArtifactFiles(string samplesRoot)
    {
        var separator = Path.DirectorySeparatorChar;
        var cleanExportRoomSegment = $"{separator}GameExportedJson{separator}";

        // These authored invariants are enforced against clean-export room artifacts, not runtime transport files.
        return Directory.EnumerateFiles(samplesRoot, "*.room.json", SearchOption.AllDirectories)
            .Where(path => path.Contains(cleanExportRoomSegment, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FindRepositoryRoot()
    {
        return AppContext.BaseDirectory;
    }
}
