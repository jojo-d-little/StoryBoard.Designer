using Storyboard.Shared.GameManager;
using Storyboard.Shared.GameServices;
using Storyboard.Shared.HostContracts;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StoryboardDesigner.App.Tests;

public sealed class SampleRuntimeLoadMatrixTests
{
    public static IEnumerable<object[]> NonRuntimeSampleProjectPaths()
    {
        yield return new object[] { Path.Combine("Samples", "Birmingham", "Birmingham.sbe.json") };
        yield return new object[] { Path.Combine("Samples", "ObjectPlayLevel1", "ObjectPlayLevel1.sbe.json") };
        yield return new object[] { Path.Combine("Samples", "ObjectPlayLevel2", "ObjectPlayLevel2.sbe.json") };
        yield return new object[] { Path.Combine("Samples", "TraversalExamples", "TraversalExamples.sbe.json") };
    }

    public static IEnumerable<object[]> RuntimeSampleProjectPaths()
    {
        yield return new object[] { Path.Combine("Samples", "Birmingham", "GameRuntimeJson", "Birmingham.sbr.runtime.json") };
        yield return new object[] { Path.Combine("Samples", "ObjectPlayLevel1", "GameRuntimeJson", "ObjectPlayLevel1.sbr.runtime.json") };
        yield return new object[] { Path.Combine("Samples", "ObjectPlayLevel1", "GameRuntimeJson", "SingleRoomAntics.sbr.runtime.json") };
        yield return new object[] { Path.Combine("Samples", "ObjectPlayLevel2", "GameRuntimeJson", "ObjectPlayLevel2.sbr.runtime.json") };
        yield return new object[] { Path.Combine("Samples", "TraversalExamples", "GameRuntimeJson", "TraversalExamples.sbr.runtime.json") };
    }

    [Theory]
    [MemberData(nameof(NonRuntimeSampleProjectPaths))]
    public void LoadGameRuntimeProject_FromNonCleanSampleProject_ReturnsDiagnostics(string relativePath)
    {
        var manager = CreateSut();
        var projectPath = ResolvePath(relativePath);

        Assert.True(File.Exists(projectPath), $"Sample project file is missing: {projectPath}");

        var result = Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager).LoadGameRuntimeProject(projectPath);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Contains("runtime project file", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(RuntimeSampleProjectPaths))]
    public void LoadGameRuntimeProject_FromRuntimeSampleExport_InitializesRuntime(string relativePath)
    {
        var manager = CreateSut();
        var cleanProjectPath = ResolvePath(relativePath);

        Assert.True(File.Exists(cleanProjectPath), $"Clean sample file is missing: {cleanProjectPath}");

        var result = Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager).LoadGameRuntimeProject(cleanProjectPath);

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics));
        Assert.True(manager.HasActiveRuntime);
        Assert.Equal(cleanProjectPath, Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager).LoadedProjectFilePath);
    }

    [Fact]
    public void LoadGameRuntimeProject_FromNonCleanSampleWithoutRuntimeExport_ReturnsDiagnostics()
    {
        var manager = CreateSut();
        var projectPath = ResolvePath(Path.Combine("Samples", "MapDemo1", "MapDemo1.sbe.json"));

        Assert.True(File.Exists(projectPath), $"Sample project file is missing: {projectPath}");

        var result = Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager).LoadGameRuntimeProject(projectPath);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Contains("runtime project file", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryLoadRuntimeBootstrap_WorkshopTutorialRuntimeExport_LoadsProcedureDefinitionsFromSidecars()
    {
        IGameProjectRuntimeLoaderService loader = new GameProjectRuntimeLoaderService();
        var cleanProjectPath = ResolvePath(Path.Combine("Samples", "WorkshopTutorial", "GameRuntimeJson", "WorkshopTutorial.sbr.runtime.json"));

        Assert.True(File.Exists(cleanProjectPath), $"Clean sample file is missing: {cleanProjectPath}");

        var bootstrap = loader.TryLoadRuntimeBootstrap(cleanProjectPath);

        Assert.NotNull(bootstrap);
        Assert.NotNull(bootstrap!.Project.RuntimeProcedures);
        var procedure = Assert.Single(bootstrap.Project.RuntimeProcedures!);
        Assert.Equal(Guid.Parse("80423A4C-1027-4247-9F32-7762DCEE7AD2"), procedure.Id);
        Assert.Equal("FixKey", procedure.Name);
    }

    [Fact]
    public void LoadGameRuntimeProject_BirminghamRuntimeExport_DoesNotEmitLegacyFallbackWarnings()
    {
        var manager = CreateSut();
        var cleanProjectPath = ResolvePath(Path.Combine("Samples", "Birmingham", "GameRuntimeJson", "Birmingham.sbr.runtime.json"));

        Assert.True(File.Exists(cleanProjectPath), $"Clean sample file is missing: {cleanProjectPath}");

        var result = Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager).LoadGameRuntimeProject(cleanProjectPath);

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics));
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Contains("Runtime load warning", StringComparison.OrdinalIgnoreCase)
            && diagnostic.Contains("legacy fallback", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadGameRuntimeProject_LegacyAliasOnlyRoomLink_FailsAfterHardCut()
    {
        var sourceRoot = ResolvePath(Path.Combine("Samples", "Birmingham", "GameRuntimeJson"));
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var runtimeRoot = Path.Combine(tempRoot, "GameRuntimeJson");
            CopyDirectory(sourceRoot, runtimeRoot);

            var areaFile = Directory
                .EnumerateFiles(Path.Combine(runtimeRoot, "Area"), "*.runtime.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .First();

            var areaJson = JsonNode.Parse(File.ReadAllText(areaFile))?.AsObject();
            Assert.NotNull(areaJson);

            var links = areaJson!["links"]?.AsArray();
            Assert.NotNull(links);
            var firstLink = links![0]?.AsObject();
            Assert.NotNull(firstLink);

            var gameProperties = firstLink!["gameProperties"]?.AsArray();
            Assert.NotNull(gameProperties);
            var passable = gameProperties![0]?.AsObject();
            Assert.NotNull(passable);

            var defaultValue = passable!["defaultValue"]?.GetValue<string>() ?? "true";
            var sharedVariableId = passable["sharedVariableId"]?.GetValue<string>();

            firstLink.Remove("gameProperties");
            firstLink["defaultIsPassable"] = !string.Equals(defaultValue, "false", StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(sharedVariableId))
            {
                firstLink["sharedVariableId"] = sharedVariableId;
            }

            File.WriteAllText(areaFile, areaJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            var manager = CreateSut();
            var runtimeProjectPath = Path.Combine(runtimeRoot, "Birmingham.sbr.runtime.json");
            var result = Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager).LoadGameRuntimeProject(runtimeProjectPath);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Contains("Runtime load failed", StringComparison.OrdinalIgnoreCase)
                && diagnostic.Contains("missing required canonical gameProperties.isPassable", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destinationFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destinationFile, overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var destinationSubdirectory = Path.Combine(destinationDir, Path.GetFileName(directory));
            CopyDirectory(directory, destinationSubdirectory);
        }
    }

    private static IHostRuntimeCommandProcessorClient CreateSut()
    {
        IGameCommandPreprocessorService preprocessor = new GameCommandPreprocessorService();
        IActionScriptEvaluationService evaluator = new ActionScriptEvaluationService();
        IRuntimeCommandProcessorService processor = new GameCommandProcessorService(evaluator, preprocessor);
        IGameProjectRuntimeLoaderService loader = new GameProjectRuntimeLoaderService();
        IRuntimeLoadedGameLoader runtimeLoadedGameLoader = new CleanProjectRuntimeLoadedGameLoader(loader);
        return new Storyboard.Shared.GameManager.GameManager(runtimeLoadedGameLoader, processor);
    }

    private static string ResolvePath(string relativePath)
    {
        return Path.Combine(FindRepositoryRoot(), relativePath);
    }

    private static string FindRepositoryRoot()
    {
        return AppContext.BaseDirectory;
    }
}
