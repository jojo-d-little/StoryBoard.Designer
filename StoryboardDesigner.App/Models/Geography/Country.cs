namespace StoryboardDesigner.App.Models;

public sealed class Country : ScopeNodeBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Country";
    public bool HideEmptyConfiguration { get; set; }
    public string ProducerNotes { get; set; } = string.Empty;
    public string StartingAreaName { get; set; } = string.Empty;
    public List<GamePropertyDefinition> Variables { get; set; } = new();
    public List<string> AdditionalVerbs { get; set; } = new();
    public List<string> AdditionalDirectionals { get; set; } = new();
    public List<DirectionalTraversalMapping> AdditionalDirectionalTraversalMappings { get; set; } = new();
    public List<CommandAction> AvailableActions { get; set; } = new();
    public List<SoundEffectLibraryEntry> SoundEffectLibraryEntries { get; set; } = new();
    public List<string> BaseObjectsIgnoredValidationRuleIds { get; set; } = new();
    public List<GameObject> BaseObjects { get; set; } = new();
    public List<GameObject> GameObjects { get; set; } = new();
    public List<Area> Areas { get; set; } = new();

    public override ScopeNodeKind ScopeKind => ScopeNodeKind.Country;
    public override string ScopeName => Name;
    public override IEnumerable<string> ScopeTokens => string.IsNullOrWhiteSpace(Name)
        ? Array.Empty<string>()
        : new[] { Name };
    public override IEnumerable<IScopedAwareNode> ChildScopes =>
        GameObjects.Cast<IScopedAwareNode>().Concat(Areas);

    protected override bool CanAcceptChild(IScopedAwareNode child)
    {
        return child.ScopeKind is ScopeNodeKind.Area or ScopeNodeKind.GameObject;
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

        if (child is not Area area)
        {
            return false;
        }

        if (Areas.Contains(area))
        {
            return true;
        }

        Areas.Add(area);
        return true;
    }

    protected override bool TryRemoveChildCore(IScopedAwareNode child)
    {
        if (child is GameObject obj)
        {
            return GameObjects.Remove(obj);
        }

        return child is Area area && Areas.Remove(area);
    }
}
