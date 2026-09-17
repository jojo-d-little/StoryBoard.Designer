namespace StoryboardDesigner.App.Models;

public sealed class PhaseNode : ScopeNodeBase
{
    private string _displayName = string.Empty;
    private string _phaseKey = string.Empty;
    private readonly List<PhaseNode> _children = new();

    public Guid Id { get; set; } = Guid.NewGuid();
    public PhaseTier Tier { get; set; } = PhaseTier.Book;

    public string PhaseKey
    {
        get => _phaseKey;
        set => _phaseKey = value?.Trim() ?? string.Empty;
    }

    public string DisplayName
    {
        get => _displayName;
        set => _displayName = value?.Trim() ?? string.Empty;
    }

    public string? Title { get; set; }
    public string? TitlePresentationCueEffectKey { get; set; }
    public string? Prologue { get; set; }
    public string? ProloguePresentationCueEffectKey { get; set; }
    public string? Narrative { get; set; }
    public string? NarrativePresentationCueEffectKey { get; set; }
    public Guid? PhaseAmbientSoundEffectId { get; set; }
    public string? PhaseAmbientTimerKey { get; set; }
    public PhaseAmbienceMode? PhaseAmbienceMode { get; set; }
    public List<GamePropertyDefinition> Variables { get; set; } = new();
    public List<CommandAction> AvailableActions { get; set; } = new();
    public List<string> AdditionalVerbs { get; set; } = new();
    public List<string> AdditionalDirectionals { get; set; } = new();
    public List<DirectionalTraversalMapping> DirectionalTraversalMappings { get; set; } = new();
    public List<SoundEffectLibraryEntry> SoundEffectLibraryEntries { get; set; } = new();

    public List<PhaseNode> Children => _children;

    public override ScopeNodeKind ScopeKind => Tier switch
    {
        PhaseTier.Book => ScopeNodeKind.Book,
        PhaseTier.Chapter => ScopeNodeKind.Chapter,
        _ => ScopeNodeKind.Page
    };

    public override string ScopeName => string.IsNullOrWhiteSpace(DisplayName)
        ? PhaseKey
        : DisplayName;

    public override IEnumerable<string> ScopeTokens
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PhaseKey))
            {
                return Array.Empty<string>();
            }

            return new[] { PhaseKey };
        }
    }

    public override IEnumerable<IScopedAwareNode> ChildScopes => _children;

    protected override bool CanAcceptChild(IScopedAwareNode child)
    {
        if (child is not PhaseNode phaseChild)
        {
            return false;
        }

        return Tier switch
        {
            PhaseTier.Book => phaseChild.Tier == PhaseTier.Chapter,
            PhaseTier.Chapter => phaseChild.Tier == PhaseTier.Page,
            _ => false
        };
    }

    protected override bool TryAddChildCore(IScopedAwareNode child)
    {
        if (child is not PhaseNode phaseChild)
        {
            return false;
        }

        if (_children.Contains(phaseChild))
        {
            return true;
        }

        _children.Add(phaseChild);
        return true;
    }

    protected override bool TryRemoveChildCore(IScopedAwareNode child)
    {
        return child is PhaseNode phaseChild && _children.Remove(phaseChild);
    }
}