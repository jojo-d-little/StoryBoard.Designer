using Storyboard.Shared.GameServices;
using Storyboard.Shared.HostContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class AreaMapMultiCellTraversalRuntimeSmokeTests
{
    [Fact]
    public void ExportedNorthLink_EntersMultiCellRoomAndCanReturnSouth()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var hallway = new Room { Name = "North Hallway", NameInGame = "North Hallway" };
            var foyer = new Room { Name = "Elevator Foyer", NameInGame = "Elevator Foyer" };
            var area = new Area
            {
                Name = "Area",
                AdditionalDirectionals = ["north", "south"],
                AdditionalDirectionalTraversalMappings =
                [
                    new DirectionalTraversalMapping { Token = "north", TraversalDirection = Direction10.North },
                    new DirectionalTraversalMapping { Token = "south", TraversalDirection = Direction10.South }
                ],
                AvailableActions =
                [
                    new CommandAction
                    {
                        Name = "GoAdjacent",
                        ActionType = CommandActionType.NavigateToAdjacent,
                        Verbs = ["go"]
                    }
                ],
                Rooms = [hallway, foyer],
                StartingRoomId = hallway.Id,
                RoomPlacements =
                [
                    new AreaRoomPlacement { RoomId = hallway.Id, X = 384, Y = 304 },
                    new AreaRoomPlacement { RoomId = foyer.Id, X = 204, Y = 164, ColumnSpan = 2 }
                ],
                TraversalConnections =
                [
                    new TraversalConnection
                    {
                        RoomAId = hallway.Id,
                        RoomBId = foyer.Id,
                        BaseTraversalDirectionFromA = Direction10.North,
                        TraversalAccessMode = TraversalAccessMode.TwoWay
                    }
                ]
            };
            var project = new ProjectModel
            {
                Name = "Elevator Foyer Traversal",
                CommandVerbs = ["go"],
                Planets = [new Planet { Name = "Planet", Countries =
                    [new Country { Name = "Country", Areas = [area] }] }]
            };
            var projectFilePath = Path.Combine(tempRoot, "Foyer.sbe.json");
            var runtimePath = new JsonExportService().ExportCleanProjectV1(projectFilePath, project);

            IGameCommandPreprocessorService preprocessor = new GameCommandPreprocessorService();
            IActionScriptEvaluationService evaluator = new ActionScriptEvaluationService();
            IRuntimeCommandProcessorService processor = new GameCommandProcessorService(evaluator, preprocessor);
            IGameProjectRuntimeLoaderService loader = new GameProjectRuntimeLoaderService();
            IRuntimeLoadedGameLoader loadedGameLoader = new CleanProjectRuntimeLoadedGameLoader(loader);
            IHostRuntimeCommandProcessorClient manager = new Storyboard.Shared.GameManager.GameManager(loadedGameLoader, processor);
            var debugger = Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager);
            var loadResult = debugger.LoadGameRuntimeProject(runtimePath);
            Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));
            Assert.Equal(hallway.Id, debugger.CurrentSession?.CurrentRoom?.ScopeNodeId);
            Assert.Equal(2, debugger.CurrentSession?.CurrentArea?.TraversalLegs.Count);

            var north = manager.ProcessCommand(new HostRequestContext(), "go north", includeTechnicalDiagnostics: true);
            Assert.True(north.Success, north.ResultCode + " | " + string.Join(" | ", north.Diagnostics));
            Assert.Equal(foyer.Id, debugger.CurrentSession?.CurrentRoom?.ScopeNodeId);

            var south = manager.ProcessCommand(new HostRequestContext(), "go south", includeTechnicalDiagnostics: true);
            Assert.True(south.Success, string.Join(" | ", south.Diagnostics));
            Assert.Equal(hallway.Id, debugger.CurrentSession?.CurrentRoom?.ScopeNodeId);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }
}
