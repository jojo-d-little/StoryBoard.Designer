namespace StoryboardDesigner.App.Models;

public sealed class Planet : ScopeNodeBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Planet";
    public bool HideEmptyConfiguration { get; set; }
    public string ProducerNotes { get; set; } = string.Empty;
    public string StartingCountryName { get; set; } = string.Empty;
    public List<GamePropertyDefinition> Variables { get; set; } = new();
    public List<string> AdditionalVerbs { get; set; } = new();
    public List<string> AdditionalDirectionals { get; set; } = new();
    public List<DirectionalTraversalMapping> AdditionalDirectionalTraversalMappings { get; set; } = new();
    public List<CommandAction> AvailableActions { get; set; } = new();
    public List<SoundEffectLibraryEntry> SoundEffectLibraryEntries { get; set; } = new();
    public List<string> BaseObjectsIgnoredValidationRuleIds { get; set; } = new();
    public List<GameObject> BaseObjects { get; set; } = new();
    public List<GameObject> GameObjects { get; set; } = new();
    public List<Country> Countries { get; set; } = new();

    public override ScopeNodeKind ScopeKind => ScopeNodeKind.Planet;
    public override string ScopeName => Name;
    public override IEnumerable<string> ScopeTokens => string.IsNullOrWhiteSpace(Name)
        ? Array.Empty<string>()
        : new[] { Name };
    public override IEnumerable<IScopedAwareNode> ChildScopes =>
        GameObjects.Cast<IScopedAwareNode>().Concat(Countries);

    protected override bool CanAcceptChild(IScopedAwareNode child)
    {
        return child.ScopeKind is ScopeNodeKind.Country or ScopeNodeKind.GameObject;
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

        if (child is not Country country)
        {
            return false;
        }

        if (Countries.Contains(country))
        {
            return true;
        }

        Countries.Add(country);
        return true;
    }

    protected override bool TryRemoveChildCore(IScopedAwareNode child)
    {
        if (child is GameObject obj)
        {
            return GameObjects.Remove(obj);
        }

        return child is Country country && Countries.Remove(country);
    }
}
