using System.Text;
using System.Text.Json;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public class JsonExportServiceRuntimeExportValidationTests
{
    private const string RuntimeExportFolderName = "GameRuntimeJson";

    [Fact]
    public void ExportRuntimeProjectV1_ProducesExpectedSchemaShape()
    {
        var service = new JsonExportService();
        var sourceProjectPath = Path.Combine(
            FindRepositoryRoot(),
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
            var cleanProjectPath = service.ExportCleanProjectV1(exportProjectPath, project!);

            var legacyAggregateNavigationPath = BuildLegacyRuntimeNavigationPath(exportProjectPath);
            var areaScopeFolder = BuildScopeKindAreaFolderPath(exportProjectPath);
            var roomScopeFolder = BuildScopeKindRoomFolderPath(exportProjectPath);
            var assetsManifestPath = BuildAssetsManifestPath(exportProjectPath);

            Assert.True(File.Exists(cleanProjectPath));
            Assert.False(File.Exists(legacyAggregateNavigationPath));
            Assert.True(Directory.Exists(areaScopeFolder));
            Assert.True(Directory.Exists(roomScopeFolder));
            Assert.True(File.Exists(assetsManifestPath));

            using var projectDoc = JsonDocument.Parse(File.ReadAllText(cleanProjectPath, Encoding.UTF8));
            Assert.Equal("1.0", projectDoc.RootElement.GetProperty("schemaVersion").GetString());
            Assert.Equal(JsonValueKind.String, projectDoc.RootElement.GetProperty("name").ValueKind);
            Assert.Equal(JsonValueKind.Number, projectDoc.RootElement.GetProperty("autoSaveSeconds").ValueKind);
            Assert.Equal(JsonValueKind.Array, projectDoc.RootElement.GetProperty("planetIds").ValueKind);
            Assert.False(projectDoc.RootElement.TryGetProperty("stackScaleStepDefault", out _));
            Assert.False(projectDoc.RootElement.TryGetProperty("minStackScaleDefault", out _));
            Assert.False(projectDoc.RootElement.TryGetProperty("planets", out _));
            Assert.True(projectDoc.RootElement.GetProperty("planetIds").GetArrayLength() > 0);

            var areaFiles = Directory.EnumerateFiles(areaScopeFolder, "*.runtime.json").ToList();
            Assert.NotEmpty(areaFiles);

            using var firstAreaDoc = JsonDocument.Parse(File.ReadAllText(areaFiles[0], Encoding.UTF8));
            Assert.True(firstAreaDoc.RootElement.TryGetProperty("links", out var linksProp));
            Assert.Equal(JsonValueKind.Array, linksProp.ValueKind);
            Assert.True(firstAreaDoc.RootElement.TryGetProperty("roomPlacements", out var placementsProp));
            Assert.Equal(JsonValueKind.Array, placementsProp.ValueKind);
            Assert.False(firstAreaDoc.RootElement.TryGetProperty("traversalConnections", out _));

            using var manifestDoc = JsonDocument.Parse(File.ReadAllText(assetsManifestPath, Encoding.UTF8));
            Assert.Equal("1.0", manifestDoc.RootElement.GetProperty("schemaVersion").GetString());
            Assert.Equal(JsonValueKind.Array, manifestDoc.RootElement.GetProperty("unresolvedSources").ValueKind);

            var roomFiles = Directory.EnumerateFiles(roomScopeFolder, "*.runtime.json").ToList();
            Assert.NotEmpty(roomFiles);

            using var firstRoomDoc = JsonDocument.Parse(File.ReadAllText(roomFiles[0], Encoding.UTF8));
            Assert.False(firstRoomDoc.RootElement.TryGetProperty("schemaVersion", out _));
            Assert.Equal(JsonValueKind.String, firstRoomDoc.RootElement.GetProperty("name").ValueKind);
            Assert.False(firstRoomDoc.RootElement.TryGetProperty("commandPhrases", out _));
            Assert.Equal(JsonValueKind.Array, firstRoomDoc.RootElement.GetProperty("availableGameActions").ValueKind);

            string? firstImagePathSemantics = null;
            foreach (var roomFile in roomFiles)
            {
                using var roomDoc = JsonDocument.Parse(File.ReadAllText(roomFile, Encoding.UTF8));
                if (!roomDoc.RootElement.TryGetProperty("images", out var images)
                    || images.ValueKind != JsonValueKind.Array
                    || images.GetArrayLength() == 0
                    || !images[0].TryGetProperty("imagePathSemantics", out var pathSemantics))
                {
                    continue;
                }

                firstImagePathSemantics = pathSemantics.GetString();
                break;
            }

            if (!string.IsNullOrWhiteSpace(firstImagePathSemantics))
            {
                Assert.Equal("runtimeExportRelative", firstImagePathSemantics);
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
    public void ExportRuntimeProjectV1_MaintainsReferenceIntegrity()
    {
        var service = new JsonExportService();
        var sourceProjectPath = Path.Combine(
            FindRepositoryRoot(),
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
            var cleanProjectPath = service.ExportCleanProjectV1(exportProjectPath, project!);
            var legacyAggregateNavigationPath = BuildLegacyRuntimeNavigationPath(exportProjectPath);
            var roomScopeFolder = BuildScopeKindRoomFolderPath(exportProjectPath);
            var planetScopeFolder = BuildScopeKindPlanetFolderPath(exportProjectPath);
            var countryScopeFolder = BuildScopeKindCountryFolderPath(exportProjectPath);
            var areaScopeFolder = BuildScopeKindAreaFolderPath(exportProjectPath);

            Assert.False(File.Exists(legacyAggregateNavigationPath));

            var roomFiles = Directory.EnumerateFiles(roomScopeFolder, "*.runtime.json").ToList();
            var planetFiles = Directory.EnumerateFiles(planetScopeFolder, "*.runtime.json").ToList();
            var countryFiles = Directory.EnumerateFiles(countryScopeFolder, "*.runtime.json").ToList();
            var areaFiles = Directory.EnumerateFiles(areaScopeFolder, "*.runtime.json").ToList();
            Assert.NotEmpty(roomFiles);
            Assert.NotEmpty(planetFiles);
            Assert.NotEmpty(countryFiles);
            Assert.NotEmpty(areaFiles);

            var roomIds = new HashSet<Guid>();
            foreach (var roomFile in roomFiles)
            {
                using var roomDoc = JsonDocument.Parse(File.ReadAllText(roomFile, Encoding.UTF8));
                var roomId = GetGuidProperty(roomDoc.RootElement, "id", "Id");
                roomIds.Add(roomId);

                var availableActionIds = roomDoc.RootElement
                    .GetProperty("availableGameActions")
                    .EnumerateArray()
                    .Select(action => action.GetProperty("id").GetGuid())
                    .ToHashSet();

                Assert.False(roomDoc.RootElement.TryGetProperty("commandPhrases", out _));
            }

            var countryIds = new HashSet<Guid>();
            foreach (var countryFile in countryFiles)
            {
                using var countryDoc = JsonDocument.Parse(File.ReadAllText(countryFile, Encoding.UTF8));
                countryIds.Add(GetGuidProperty(countryDoc.RootElement, "id", "Id"));
            }

            var areaIds = new HashSet<Guid>();
            foreach (var areaFile in areaFiles)
            {
                using var areaDoc = JsonDocument.Parse(File.ReadAllText(areaFile, Encoding.UTF8));
                areaIds.Add(GetGuidProperty(areaDoc.RootElement, "id", "Id"));
            }

            foreach (var planetFile in planetFiles)
            {
                using var planetDoc = JsonDocument.Parse(File.ReadAllText(planetFile, Encoding.UTF8));
                foreach (var countryIdElement in planetDoc.RootElement.GetProperty("countryIds").EnumerateArray())
                {
                    Assert.Contains(countryIdElement.GetGuid(), countryIds);
                }
            }

            foreach (var countryFile in countryFiles)
            {
                using var countryDoc = JsonDocument.Parse(File.ReadAllText(countryFile, Encoding.UTF8));
                foreach (var areaIdElement in countryDoc.RootElement.GetProperty("areaIds").EnumerateArray())
                {
                    Assert.Contains(areaIdElement.GetGuid(), areaIds);
                }
            }

            foreach (var areaFile in areaFiles)
            {
                using var areaDoc = JsonDocument.Parse(File.ReadAllText(areaFile, Encoding.UTF8));
                if (areaDoc.RootElement.TryGetProperty("startingRoomId", out var startingRoomProp)
                    && startingRoomProp.ValueKind is JsonValueKind.String)
                {
                    Assert.Contains(startingRoomProp.GetGuid(), roomIds);
                }

                foreach (var roomIdElement in areaDoc.RootElement.GetProperty("roomIds").EnumerateArray())
                {
                    Assert.Contains(roomIdElement.GetGuid(), roomIds);
                }
            }

            foreach (var areaFile in areaFiles)
            {
                using var areaNavDoc = JsonDocument.Parse(File.ReadAllText(areaFile, Encoding.UTF8));

                foreach (var link in areaNavDoc.RootElement.GetProperty("links").EnumerateArray())
                {
                    Assert.Contains(link.GetProperty("fromRoomId").GetGuid(), roomIds);
                    Assert.Contains(link.GetProperty("toRoomId").GetGuid(), roomIds);
                }

                foreach (var placement in areaNavDoc.RootElement.GetProperty("roomPlacements").EnumerateArray())
                {
                    Assert.Contains(placement.GetProperty("roomId").GetGuid(), roomIds);
                }
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

    private static string BuildLegacyRuntimeNavigationPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = BuildRuntimeProjectBaseName(projectFilePath);
        return Path.Combine(folder, RuntimeExportFolderName, $"{baseName}.sbr.runtime.navigation.json");
    }

    private static string BuildScopeKindRoomFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName, "Room");
    }

    private static string BuildScopeKindPlanetFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName, "Planet");
    }

    private static string BuildScopeKindCountryFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName, "Country");
    }

    private static string BuildScopeKindAreaFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName, "Area");
    }

    private static string BuildRuntimeProjectBaseName(string projectFilePath)
    {
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return baseName.EndsWith(".sbe", StringComparison.OrdinalIgnoreCase)
            ? baseName[..^4]
            : baseName;
    }

    private static string BuildAssetsManifestPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RuntimeExportFolderName, "assets", "assets-manifest.json");
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

    private static Guid GetGuidProperty(JsonElement element, string primaryName, string fallbackName)
    {
        if (element.TryGetProperty(primaryName, out var primary)
            && primary.ValueKind == JsonValueKind.String)
        {
            return primary.GetGuid();
        }

        if (element.TryGetProperty(fallbackName, out var fallback)
            && fallback.ValueKind == JsonValueKind.String)
        {
            return fallback.GetGuid();
        }

        throw new KeyNotFoundException($"Expected either '{primaryName}' or '{fallbackName}' guid property.");
    }
}
