namespace StoryboardDesigner.App.Models;

public sealed class Area : ScopeNodeBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Area";
    public bool HideEmptyConfiguration { get; set; }
    public string ProducerNotes { get; set; } = string.Empty;
    public AreaAdjacencyMode AdjacencyMode { get; set; } = AreaAdjacencyMode.EightDirectional;
    public AreaRoomDropBehavior RoomDropBehavior { get; set; } = AreaRoomDropBehavior.KeepDisconnected;
    public AreaAdjacencyMode? TraversalModeOverride { get; set; }
    public Guid? StartingRoomId { get; set; }
    public List<GamePropertyDefinition> Variables { get; set; } = new();
    public List<string> AdditionalVerbs { get; set; } = new();
    public List<string> AdditionalDirectionals { get; set; } = new();
    public List<DirectionalTraversalMapping> AdditionalDirectionalTraversalMappings { get; set; } = new();
    public List<CommandAction> AvailableActions { get; set; } = new();
    public List<SoundEffectLibraryEntry> SoundEffectLibraryEntries { get; set; } = new();
    public List<string> BaseObjectsIgnoredValidationRuleIds { get; set; } = new();
    public List<GameObject> BaseObjects { get; set; } = new();
    public List<GameObject> GameObjects { get; set; } = new();
    public List<Room> Rooms { get; set; } = new();
    public List<RoomLink> Links { get; set; } = new();
    public List<TraversalConnection> TraversalConnections { get; set; } = new();
    public List<AreaRoomPlacement> RoomPlacements { get; set; } = new();

    public override ScopeNodeKind ScopeKind => ScopeNodeKind.Area;
    public override string ScopeName => Name;
    public override IEnumerable<string> ScopeTokens => string.IsNullOrWhiteSpace(Name)
        ? Array.Empty<string>()
        : new[] { Name };
    public override IEnumerable<IScopedAwareNode> ChildScopes =>
        GameObjects.Cast<IScopedAwareNode>().Concat(Rooms);

    protected override bool CanAcceptChild(IScopedAwareNode child)
    {
        return child.ScopeKind is ScopeNodeKind.Room or ScopeNodeKind.GameObject;
    }

    protected override bool TryAddChildCore(IScopedAwareNode child)
    {
        if (child is GameObject obj)
        {
            if (GameObjects.Contains(obj))
            {
                return true;
            }

            GameObjects.Add(obj);
            return true;
        }

        if (child is not Room room)
        {
            return false;
        }

        if (Rooms.Contains(room))
        {
            return true;
        }

        Rooms.Add(room);
        return true;
    }

    protected override bool TryRemoveChildCore(IScopedAwareNode child)
    {
        if (child is GameObject obj)
        {
            return GameObjects.Remove(obj);
        }

        return child is Room room && Rooms.Remove(room);
    }
}
