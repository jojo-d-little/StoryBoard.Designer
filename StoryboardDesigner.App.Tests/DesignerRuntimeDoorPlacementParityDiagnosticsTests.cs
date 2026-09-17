using Storyboard.Shared.GameManager;
using Storyboard.Shared.GameServices;
using Storyboard.Shared.GameStateData;
using Storyboard.Shared.HostContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using Xunit.Abstractions;

namespace StoryboardDesigner.App.Tests;

public sealed class DesignerRuntimeDoorPlacementParityDiagnosticsTests
{
    private readonly ITestOutputHelper _output;

    public DesignerRuntimeDoorPlacementParityDiagnosticsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void WorkshopTutorialAtrium_Doors_DesignerVsRuntime_DrawDeltaReport()
    {
        var root = FindRepositoryRoot();
        var authoredProjectPath = Path.Combine(root, "Samples", "WorkshopTutorial", "WorkshopTutorial.sbe.json");
        var cleanProjectPath = Path.Combine(root, "Samples", "WorkshopTutorial", "GameRuntimeJson", "WorkshopTutorial.sbr.runtime.json");

        var authoredProject = LoadAuthoredProject(authoredProjectPath);
        var authoredAtrium = FindRoomByName(authoredProject, "Atrium");

        var manager = CreateGameManager();
        var loadResult = Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager).LoadGameRuntimeProject(cleanProjectPath);
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));
        var debugger = Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager);

        // Force runtime placement normalization pass so render* variables are populated.
        _ = manager.ProcessCommand(new HostRequestContext(), "look", includeTechnicalDiagnostics: true);

        var runtimeAtrium = debugger.CurrentSession?.CurrentRoom;
        Assert.NotNull(runtimeAtrium);

        var doorNames = new[] { "Door_N", "Door_E", "Door_S", "Door_W" };
        var reportLines = new List<string>();

        foreach (var doorName in doorNames)
        {
            var authoredDoor = authoredAtrium.GameObjects.FirstOrDefault(obj =>
                string.Equals(obj.Name, doorName, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(authoredDoor);

            var runtimeDoor = runtimeAtrium!.Children.FirstOrDefault(node =>
                node.Kind == ScopeNodeKind.GameObject
                && string.Equals(node.Name, doorName, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(runtimeDoor);

            var variantName = ResolveDoorVariantName(authoredDoor!);
            var previewVm = new RoomDesignerPreviewObjectViewModel(authoredDoor!);
            previewVm.SelectedPreviewVariantName = variantName;

            var designerDrawX = authoredDoor!.PositionX + previewVm.ImageLocalAlignmentOffsetX;
            var designerDrawY = authoredDoor.PositionY + previewVm.ImageLocalAlignmentOffsetY;

            var runtimeDrawX = ResolveDoubleVariable(runtimeDoor!, "renderPositionX", "x", "positionX", "offsetX");
            var runtimeDrawY = ResolveDoubleVariable(runtimeDoor!, "renderPositionY", "y", "positionY", "offsetY");
            var runtimeAnchorX = ResolveDoubleVariable(runtimeDoor!, "renderAnchorX");
            var runtimeAnchorY = ResolveDoubleVariable(runtimeDoor!, "renderAnchorY");
            var runtimeIconOffsetX = ResolveDoubleVariable(runtimeDoor!, "renderIconOffsetX");
            var runtimeIconOffsetY = ResolveDoubleVariable(runtimeDoor!, "renderIconOffsetY");
            var runtimeRotation = ResolveDoubleVariable(runtimeDoor!, "renderRotationDegrees", "imageRotationDegrees");

            var deltaX = runtimeDrawX - designerDrawX;
            var deltaY = runtimeDrawY - designerDrawY;

            reportLines.Add(
                $"{doorName}: variant={variantName}; designerDraw=({designerDrawX:0.###},{designerDrawY:0.###}); "
                + $"runtimeDraw=({runtimeDrawX:0.###},{runtimeDrawY:0.###}); delta=({deltaX:0.###},{deltaY:0.###}); "
                + $"runtimeAnchor=({runtimeAnchorX:0.###},{runtimeAnchorY:0.###}); "
                + $"runtimeIconOffset=({runtimeIconOffsetX:0.###},{runtimeIconOffsetY:0.###}); "
                + $"runtimeRotation={runtimeRotation:0.###}; "
                + $"previewCompare={previewVm.PlacementComparisonSnapshot}");
        }

        Assert.Equal(4, reportLines.Count);
        foreach (var line in reportLines)
        {
            _output.WriteLine(line);
        }
    }

    [Fact]
    public void WorkshopTutorialAtrium_Doors_DesignerVsRuntime_NormalizedFrameDeltaReport()
    {
        var root = FindRepositoryRoot();
        var authoredProjectPath = Path.Combine(root, "Samples", "WorkshopTutorial", "WorkshopTutorial.sbe.json");
        var cleanProjectPath = Path.Combine(root, "Samples", "WorkshopTutorial", "GameRuntimeJson", "WorkshopTutorial.sbr.runtime.json");

        var authoredProject = LoadAuthoredProject(authoredProjectPath);
        var authoredAtrium = FindRoomByName(authoredProject, "Atrium");
        var gridCellSize = Math.Max(1, authoredProject.RoomDesignerGridCellSize);

        var manager = CreateGameManager();
        var loadResult = Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager).LoadGameRuntimeProject(cleanProjectPath);
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));
        var debugger = Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager);

        _ = manager.ProcessCommand(new HostRequestContext(), "look", includeTechnicalDiagnostics: true);

        var runtimeAtrium = debugger.CurrentSession?.CurrentRoom;
        Assert.NotNull(runtimeAtrium);

        var doorNames = new[] { "Door_N", "Door_E", "Door_S", "Door_W" };
        var reportLines = new List<string>();

        foreach (var doorName in doorNames)
        {
            var authoredDoor = authoredAtrium.GameObjects.FirstOrDefault(obj =>
                string.Equals(obj.Name, doorName, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(authoredDoor);

            var runtimeDoor = runtimeAtrium!.Children.FirstOrDefault(node =>
                node.Kind == ScopeNodeKind.GameObject
                && string.Equals(node.Name, doorName, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(runtimeDoor);

            var variantName = ResolveDoorVariantName(authoredDoor!);
            var previewVm = new RoomDesignerPreviewObjectViewModel(authoredDoor!);
            previewVm.SelectedPreviewVariantName = variantName;

            var designerAnchorX = authoredDoor!.PositionX;
            var designerAnchorY = authoredDoor.PositionY;
            var designerIconOffsetX = previewVm.ImageLocalAlignmentOffsetX;
            var designerIconOffsetY = previewVm.ImageLocalAlignmentOffsetY;
            var designerDrawX = designerAnchorX + designerIconOffsetX;
            var designerDrawY = designerAnchorY + designerIconOffsetY;

            var runtimeAnchorX = ResolveDoubleVariable(runtimeDoor!, "renderAnchorX");
            var runtimeAnchorY = ResolveDoubleVariable(runtimeDoor!, "renderAnchorY");
            var runtimeIconOffsetX = ResolveDoubleVariable(runtimeDoor!, "renderIconOffsetX");
            var runtimeIconOffsetY = ResolveDoubleVariable(runtimeDoor!, "renderIconOffsetY");
            var runtimeRotation = ResolveDoubleVariable(runtimeDoor!, "renderRotationDegrees", "imageRotationDegrees");
            var runtimeDrawX = ResolveDoubleVariable(runtimeDoor!, "renderPositionX");
            var runtimeDrawY = ResolveDoubleVariable(runtimeDoor!, "renderPositionY");

            AssertFinite(runtimeAnchorX, $"Missing renderAnchorX for {doorName}");
            AssertFinite(runtimeAnchorY, $"Missing renderAnchorY for {doorName}");
            AssertFinite(runtimeIconOffsetX, $"Missing renderIconOffsetX for {doorName}");
            AssertFinite(runtimeIconOffsetY, $"Missing renderIconOffsetY for {doorName}");
            AssertFinite(runtimeRotation, $"Missing renderRotationDegrees/imageRotationDegrees for {doorName}");
            AssertFinite(runtimeDrawX, $"Missing renderPositionX for {doorName}");
            AssertFinite(runtimeDrawY, $"Missing renderPositionY for {doorName}");

            var anchorDeltaX = runtimeAnchorX - designerAnchorX;
            var anchorDeltaY = runtimeAnchorY - designerAnchorY;
            var iconDeltaX = runtimeIconOffsetX - designerIconOffsetX;
            var iconDeltaY = runtimeIconOffsetY - designerIconOffsetY;
            var drawDeltaX = runtimeDrawX - designerDrawX;
            var drawDeltaY = runtimeDrawY - designerDrawY;

            var localAnchorDelta = RotateVectorDegrees(anchorDeltaX, anchorDeltaY, -runtimeRotation);
            var localIconDelta = RotateVectorDegrees(iconDeltaX, iconDeltaY, -runtimeRotation);

            reportLines.Add(
                $"{doorName}: variant={variantName}; rot={runtimeRotation:0.###}; "
                + $"designerAnchor=({designerAnchorX:0.###},{designerAnchorY:0.###}); "
                + $"runtimeAnchor=({runtimeAnchorX:0.###},{runtimeAnchorY:0.###}); "
                + $"designerIconOffset=({designerIconOffsetX:0.###},{designerIconOffsetY:0.###}); "
                + $"runtimeIconOffset=({runtimeIconOffsetX:0.###},{runtimeIconOffsetY:0.###}); "
                + $"anchorDeltaWorld=({anchorDeltaX:0.###},{anchorDeltaY:0.###}); "
                + $"anchorDeltaCells=({(anchorDeltaX / gridCellSize):0.###},{(anchorDeltaY / gridCellSize):0.###}); "
                + $"anchorDeltaLocal=({localAnchorDelta.X:0.###},{localAnchorDelta.Y:0.###}); "
                + $"anchorDeltaLocalCells=({(localAnchorDelta.X / gridCellSize):0.###},{(localAnchorDelta.Y / gridCellSize):0.###}); "
                + $"iconDeltaWorld=({iconDeltaX:0.###},{iconDeltaY:0.###}); "
                + $"iconDeltaLocal=({localIconDelta.X:0.###},{localIconDelta.Y:0.###}); "
                + $"drawDeltaWorld=({drawDeltaX:0.###},{drawDeltaY:0.###}); "
                + $"previewCompare={previewVm.PlacementComparisonSnapshot}");
        }

        Assert.Equal(4, reportLines.Count);
        foreach (var line in reportLines)
        {
            _output.WriteLine(line);
        }
    }

    private static string ResolveDoorVariantName(GameObject door)
    {
        if (door.IsOpenDefaultValue)
        {
            return door.ImageVariants.Any(v => string.Equals(v.VariantName, "Open", StringComparison.OrdinalIgnoreCase))
                ? "Open"
                : string.Empty;
        }

        return door.ImageVariants.Any(v => string.Equals(v.VariantName, "Closed", StringComparison.OrdinalIgnoreCase))
            ? "Closed"
            : string.Empty;
    }

    private static double ResolveDoubleVariable(GameStateScopeNode node, params string[] variableNames)
    {
        foreach (var variableName in variableNames)
        {
            if (node.Variables.TryGetVariable(variableName, out var runtime)
                && double.TryParse(runtime.Value?.Trim(), out var parsed)
                && double.IsFinite(parsed))
            {
                return parsed;
            }
        }

        return double.NaN;
    }

    private static (double X, double Y) RotateVectorDegrees(double x, double y, double degrees)
    {
        var radians = degrees * (Math.PI / 180d);
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return ((x * cos) - (y * sin), (x * sin) + (y * cos));
    }

    private static void AssertFinite(double value, string message)
    {
        Assert.True(double.IsFinite(value), message);
    }

    private static Room FindRoomByName(ProjectModel project, string roomName)
    {
        foreach (var planet in project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                foreach (var area in country.Areas)
                {
                    var room = area.Rooms.FirstOrDefault(r => string.Equals(r.Name, roomName, StringComparison.OrdinalIgnoreCase));
                    if (room is not null)
                    {
                        return room;
                    }
                }
            }
        }

        throw new InvalidOperationException($"Unable to resolve room '{roomName}'.");
    }

    private static ProjectModel LoadAuthoredProject(string authoredProjectPath)
    {
        var jsonService = new JsonExportService();
        var project = jsonService.TryLoadProjectModel(authoredProjectPath);
        if (project is null)
        {
            throw new InvalidOperationException($"Unable to load authored project '{authoredProjectPath}'.");
        }

        ScopeHierarchy.AttachParents(project);
        return project;
    }

    private static IHostRuntimeCommandProcessorClient CreateGameManager()
    {
        IGameCommandPreprocessorService preprocessor = new GameCommandPreprocessorService();
        IActionScriptEvaluationService evaluator = new ActionScriptEvaluationService();
        IRuntimeCommandProcessorService processor = new GameCommandProcessorService(evaluator, preprocessor);
        IGameProjectRuntimeLoaderService loader = new GameProjectRuntimeLoaderService();
        IRuntimeLoadedGameLoader runtimeLoadedGameLoader = new CleanProjectRuntimeLoadedGameLoader(loader);
        return new Storyboard.Shared.GameManager.GameManager(runtimeLoadedGameLoader, processor);
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
