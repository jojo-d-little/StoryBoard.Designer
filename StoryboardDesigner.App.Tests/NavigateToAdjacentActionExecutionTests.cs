using Storyboard.Shared.GameServices;
using Storyboard.Shared.GameServices.Actions;
using Storyboard.Shared.GameStateData;
using Storyboard.Shared.RuntimeContracts;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Tests;

public sealed class NavigateToAdjacentActionExecutionTests
{
    [Fact]
    public void Process_GoToAdjacentRoom_MovesToResolvedDestination()
    {
        var snapshot = CreateSnapshot(new[]
        {
            new RuntimeTraversalLegDescriptor(
                SourceRoomId: RoomAId,
                DestinationRoomId: RoomBId,
                Direction: Direction.North,
                DefaultIsPassable: true)
        });

        var (processor, session) = CreateProcessor(snapshot);

        var result = processor.Process(new RuntimeCommandProcessingRequest
        {
            CommandText = "go to workshop",
            Session = session
        });

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics));
        Assert.Contains("moved", result.OutputLines, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Workshop", session.CurrentRoom?.Name);
    }

    [Fact]
    public void Process_GoToAdjacentRoom_DoorClosed_ReturnsDoorClosedOutcome()
    {
        var doorId = Guid.NewGuid();

        var snapshot = CreateSnapshot(
            new[]
            {
                new RuntimeTraversalLegDescriptor(
                    SourceRoomId: RoomAId,
                    DestinationRoomId: RoomBId,
                    Direction: Direction.North,
                    DefaultIsPassable: false,
                    OpenableObjectId: doorId)
            },
            new[]
            {
                new RuntimeScopeNodeDescriptor(
                    ScopeKind: ScopeNodeKind.GameObject,
                    Name: "Door",
                    ScopeNameInGame: "Door",
                    ScopeNodeId: doorId,
                    VariableDefinitions: new[]
                    {
                        new RuntimePropertyDefinition("isOpen", "false", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse)
                    },
                    ScopeTokens: new[] { "door" },
                    AdditionalVerbs: Array.Empty<string>(),
                    AdditionalDirectionals: Array.Empty<string>(),
                    AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
                    CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
                    Children: Array.Empty<RuntimeScopeNodeDescriptor>())
            });

        var (processor, session) = CreateProcessor(snapshot);

        var result = processor.Process(new RuntimeCommandProcessingRequest
        {
            CommandText = "go to workshop",
            Session = session
        });

        Assert.False(result.Success);
        Assert.Contains("door closed", result.OutputLines, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Entry", session.CurrentRoom?.Name);
    }

    [Fact]
    public void Process_GoToAdjacentRoom_LockedDoorBlocksThenOpensViaSharedVariableLinkage()
    {
        var doorId = Guid.NewGuid();
        var sharedPassableId = Guid.NewGuid();

        var snapshot = CreateSnapshot(
            new[]
            {
                new RuntimeTraversalLegDescriptor(
                    SourceRoomId: RoomAId,
                    DestinationRoomId: RoomBId,
                    Direction: Direction.North,
                    DefaultIsPassable: false,
                    OpenableObjectId: doorId,
                    SharedVariableId: sharedPassableId)
            },
            new[]
            {
                new RuntimeScopeNodeDescriptor(
                    ScopeKind: ScopeNodeKind.GameObject,
                    Name: "Door",
                    ScopeNameInGame: "Door",
                    ScopeNodeId: doorId,
                    VariableDefinitions: new[]
                    {
                        new RuntimePropertyDefinition(
                            "isOpen",
                            "false",
                            GamePropertyLifetime.Singleton,
                            GamePropertyValueRestriction.TrueFalse,
                            sharedPassableId)
                    },
                    ScopeTokens: new[] { "door" },
                    AdditionalVerbs: Array.Empty<string>(),
                    AdditionalDirectionals: Array.Empty<string>(),
                    AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
                    CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
                    Children: Array.Empty<RuntimeScopeNodeDescriptor>())
            });

        var (processor, session) = CreateProcessor(snapshot);

        var blocked = processor.Process(new RuntimeCommandProcessingRequest
        {
            CommandText = "go to workshop",
            Session = session
        });

        Assert.False(blocked.Success);
        Assert.Contains("door closed", blocked.OutputLines, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Entry", session.CurrentRoom?.Name);

        var opened = session.TrySetVariable("Door.isOpen", "true");
        Assert.True(opened);

        var moved = processor.Process(new RuntimeCommandProcessingRequest
        {
            CommandText = "go to workshop",
            Session = session
        });

        Assert.True(moved.Success, string.Join(" | ", moved.Diagnostics));
        Assert.Contains("moved", moved.OutputLines, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Workshop", session.CurrentRoom?.Name);
    }

    [Fact]
    public void Process_GoToAdjacentRoom_NotPassable_ReturnsTraversalNotPassableOutcome()
    {
        var snapshot = CreateSnapshot(new[]
        {
            new RuntimeTraversalLegDescriptor(
                SourceRoomId: RoomAId,
                DestinationRoomId: RoomBId,
                Direction: Direction.North,
                DefaultIsPassable: false)
        });

        var (processor, session) = CreateProcessor(snapshot);

        var result = processor.Process(new RuntimeCommandProcessingRequest
        {
            CommandText = "go to workshop",
            Session = session
        });

        Assert.False(result.Success);
        Assert.Contains("not passable", result.OutputLines, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Entry", session.CurrentRoom?.Name);
    }

    [Fact]
    public void Process_GoToAdjacentRoom_LegGamePropertyIsPassable_TakesPrecedenceOverLegacyDefault()
    {
        var snapshot = CreateSnapshot(new[]
        {
            new RuntimeTraversalLegDescriptor(
                SourceRoomId: RoomAId,
                DestinationRoomId: RoomBId,
                Direction: Direction.North,
                DefaultIsPassable: true,
                GameProperties: new[]
                {
                    new RuntimePropertyDefinition(
                        Name: "isPassable",
                        DefaultValue: "false",
                        Lifetime: GamePropertyLifetime.Singleton,
                        ValueRestriction: GamePropertyValueRestriction.TrueFalse)
                })
        });

        var (processor, session) = CreateProcessor(snapshot);

        var result = processor.Process(new RuntimeCommandProcessingRequest
        {
            CommandText = "go to workshop",
            Session = session
        });

        Assert.False(result.Success);
        Assert.Contains("not passable", result.OutputLines, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Entry", session.CurrentRoom?.Name);
    }

    [Fact]
    public void Process_GoToAdjacentRoom_AmbiguousTraversals_ReturnsTraversalAmbiguousOutcome()
    {
        var snapshot = CreateSnapshot(new[]
        {
            new RuntimeTraversalLegDescriptor(
                SourceRoomId: RoomAId,
                DestinationRoomId: RoomBId,
                Direction: Direction.North,
                DefaultIsPassable: true),
            new RuntimeTraversalLegDescriptor(
                SourceRoomId: RoomAId,
                DestinationRoomId: RoomBId,
                Direction: Direction.East,
                DefaultIsPassable: true)
        });

        var (processor, session) = CreateProcessor(snapshot);

        var result = processor.Process(new RuntimeCommandProcessingRequest
        {
            CommandText = "go to workshop",
            Session = session
        });

        Assert.False(result.Success);
        Assert.Contains("ambiguous", result.OutputLines, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Entry", session.CurrentRoom?.Name);
    }

    [Fact]
    public void Process_GoNorth_UsesDirectionalFallback_WhenDestinationMissing()
    {
        var snapshot = CreateSnapshot(new[]
        {
            new RuntimeTraversalLegDescriptor(
                SourceRoomId: RoomAId,
                DestinationRoomId: RoomBId,
                Direction: Direction.North,
                DefaultIsPassable: true)
        });

        var (processor, session) = CreateProcessor(snapshot);

        var result = processor.Process(new RuntimeCommandProcessingRequest
        {
            CommandText = "go north",
            Session = session
        });

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics));
        Assert.Contains("moved", result.OutputLines, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Workshop", session.CurrentRoom?.Name);
    }

    private static (GameCommandProcessorService Processor, GameStateSession Session) CreateProcessor(RuntimeGameWorldSnapshot snapshot)
    {
        var session = GameStateSession.Create(snapshot);
        var processor = new GameCommandProcessorService(new ActionScriptEvaluationService(), new GameCommandPreprocessorService());
        return (processor, session);
    }

    private static RuntimeGameWorldSnapshot CreateSnapshot(
        IReadOnlyList<RuntimeTraversalLegDescriptor> traversalLegs,
        IReadOnlyList<RuntimeScopeNodeDescriptor>? roomAChildren = null)
    {
        var navigateAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "GoAdjacent",
            ActionType = CommandActionType.NavigateToAdjacent,
            NoVerbLinkage = false,
            Verbs = new[] { "go" },
            DirectionQualifierText = string.Empty,
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Moved"] = "moved",
                ["DoorClosed"] = "door closed",
                ["TraversalNotPassable"] = "not passable",
                ["TraversalAmbiguous"] = "ambiguous",
                ["UnknownDestination"] = "unknown destination",
                ["DirectionRequired"] = "direction required"
            },
            NavigatePayload = new RuntimeNavigateActionPayload()
        };

        var roomA = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Entry",
            ScopeNameInGame: "Entry",
            ScopeNodeId: RoomAId,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "entry" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: new[] { navigateAction },
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: roomAChildren ?? Array.Empty<RuntimeScopeNodeDescriptor>());

        var roomB = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Workshop",
            ScopeNameInGame: "Workshop",
            ScopeNodeId: RoomBId,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "workshop" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "AreaOne",
            ScopeNameInGame: "AreaOne",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "areaone" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: new[] { "north", "south", "east", "west" },
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { roomA, roomB },
            AdditionalDirectionalTraversalMappings: new[]
            {
                new RuntimeDirectionalTraversalMapping("north", Direction10.North),
                new RuntimeDirectionalTraversalMapping("south", Direction10.South),
                new RuntimeDirectionalTraversalMapping("east", Direction10.East),
                new RuntimeDirectionalTraversalMapping("west", Direction10.West)
            },
            TraversalLegs: traversalLegs);

        var country = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Country,
            Name: "CountryOne",
            ScopeNameInGame: "CountryOne",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "countryone" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { area });

        var planet = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Planet,
            Name: "PlanetOne",
            ScopeNameInGame: "PlanetOne",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "planetone" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { country });

        return new RuntimeGameWorldSnapshot(
            ProjectName: "NavigateToAdjacentTests",
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: new[] { "go" },
            GlobalDirectionals: new[] { "north", "south", "east", "west" },
            PlayerObjects: Array.Empty<RuntimeScopeNodeDescriptor>(),
            Planets: new[] { planet },
            StartingRoomId: RoomAId,
            GlobalDirectionalTraversalMappings: new[]
            {
                new RuntimeDirectionalTraversalMapping("north", Direction10.North),
                new RuntimeDirectionalTraversalMapping("south", Direction10.South),
                new RuntimeDirectionalTraversalMapping("east", Direction10.East),
                new RuntimeDirectionalTraversalMapping("west", Direction10.West)
            },
            ProjectRoomGridCellSize: 40);
    }

    private static readonly Guid RoomAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RoomBId = Guid.Parse("22222222-2222-2222-2222-222222222222");
}


