using System.Text.Json.Serialization;

namespace StoryboardDesigner.App.Models;

public sealed class ProjectModel : ScopeNodeBase
{
    public const double DefaultStackScaleStep = Storyboard.Shared.Config.StackScalePolicy.DefaultStackScaleStep;
    public const double DefaultMinStackScale = Storyboard.Shared.Config.StackScalePolicy.DefaultMinStackScale;

    private int _autoSaveSeconds = 60;
    private int _roomImageCanvasWidth = 800;
    private int _roomImageCanvasHeight = 600;
    private int _roomDesignerGridCellSize = 40;
    private double _stackScaleStepDefault = DefaultStackScaleStep;
    private double _minStackScaleDefault = DefaultMinStackScale;
    private double? _simulatorReplaySpeed;
    private List<GameObject> _globalScopeGameObjects = new();
    private List<GamePropertyDefinition> _globalScopeVariables = new();
    private List<CommandAction> _globalScopeAvailableActions = new();
    private List<SoundEffectLibraryEntry> _globalScopeSoundEffectLibraryEntries = new();
    private List<string> _globalScopeIgnoredValidationRuleIds = new();
    private string _globalScopeName = "Global Objects";
    private string _globalScopeProducerNotes = string.Empty;
    private readonly ObjectTemplatesScopeNode _templatesNode;
    private readonly RoomTemplatesScopeNode _roomTemplatesNode;
    private readonly BaseObjectsScopeNode _baseObjectsNode;
    private List<PhaseNode> _phaseBooks = new();

    public ProjectModel()
    {
        _templatesNode = new ObjectTemplatesScopeNode(this);
        _roomTemplatesNode = new RoomTemplatesScopeNode(this);
        _baseObjectsNode = new BaseObjectsScopeNode(this);
    }

    public override ScopeNodeKind ScopeKind => ScopeNodeKind.Global;
    public override string ScopeName => "Global";
    public override IEnumerable<string> ScopeTokens => new[] { "global" };
    public override IEnumerable<IScopedAwareNode> ChildScopes => BuildChildScopes();

    public string Name { get; set; } = "Storyboard Project";
    public string GameDisplayName { get; set; } = string.Empty;
    public string GameSummary { get; set; } = string.Empty;
    public List<string> GamePreviewImages { get; set; } = new();
    public int AutoSaveSeconds
    {
        get => _autoSaveSeconds;
        set => _autoSaveSeconds = value >= 0 ? value : 60;
    }

    public int RoomImageCanvasWidth
    {
        get => _roomImageCanvasWidth;
        set => _roomImageCanvasWidth = value > 0 ? value : 800;
    }

    public int RoomImageCanvasHeight
    {
        get => _roomImageCanvasHeight;
        set => _roomImageCanvasHeight = value > 0 ? value : 600;
    }

    public int RoomDesignerGridCellSize
    {
        get => _roomDesignerGridCellSize;
        set => _roomDesignerGridCellSize = value > 0 ? value : 40;
    }

    public double StackScaleStepDefault
    {
        get => _stackScaleStepDefault;
        set => _stackScaleStepDefault = Storyboard.Shared.Config.StackScalePolicy.NormalizeStackScaleStepOrDefault(value);
    }

    public double MinStackScaleDefault
    {
        get => _minStackScaleDefault;
        set => _minStackScaleDefault = Storyboard.Shared.Config.StackScalePolicy.NormalizeMinStackScaleOrDefault(value);
    }

    public List<string> CommandVerbs { get; set; } = new();
    public List<string> Directionals { get; set; } = new();
    public override List<string> IgnoredValidationRuleIds
    {
        get => GlobalIgnoredValidationRuleIds;
        set => GlobalIgnoredValidationRuleIds = value ?? new List<string>();
    }
    public List<string> GlobalIgnoredValidationRuleIds
    {
        get => GlobalScopeIgnoredValidationRuleIds;
        set => GlobalScopeIgnoredValidationRuleIds = value ?? new List<string>();
    }
    public List<string> ObjectTemplatesIgnoredValidationRuleIds { get; set; } = new();
    public List<string> RoomTemplatesIgnoredValidationRuleIds { get; set; } = new();
    public List<string> BaseObjectsIgnoredValidationRuleIds { get; set; } = new();
    public List<DirectionalTraversalMapping> DirectionalTraversalMappings { get; set; } = new();
    public string SimulatorReplayFilePath { get; set; } = string.Empty;
    public double? SimulatorReplaySpeed
    {
        get => _simulatorReplaySpeed;
        set => _simulatorReplaySpeed = value is null || value > 0 ? value : null;
    }

    public string StartingPlanetName { get; set; } = string.Empty;
    public string PlayerCharacterObjectName { get; set; } = string.Empty;
    public bool HideEmptyConfiguration { get; set; }
    public AreaAdjacencyMode DefaultTraversalMode { get; set; } = AreaAdjacencyMode.EightDirectional;
    public List<Guid> ProcedureIds { get; set; } = new();
    public List<ProcedureDefinition> Procedures { get; set; } = new();
    public List<GamePropertyDefinition> GlobalVariables { get; set; } = new();
    public List<SharedVariableDefinition> SharedVariables { get; set; } = new();

    // Canonical neutral-name facade for global-scope authored content.
    // Marked ignored to preserve current persisted JSON shape during migration.
    [JsonIgnore]
    public ProjectGlobalScopeContent GlobalScope => new(this);

    internal List<GameObject> GlobalScopeGameObjects
    {
        get => _globalScopeGameObjects;
        set => _globalScopeGameObjects = value ?? new List<GameObject>();
    }

    internal List<GamePropertyDefinition> GlobalScopeGameProperties
    {
        get => _globalScopeVariables;
        set => _globalScopeVariables = value ?? new List<GamePropertyDefinition>();
    }

    internal List<CommandAction> GlobalScopeAvailableActions
    {
        get => _globalScopeAvailableActions;
        set => _globalScopeAvailableActions = value ?? new List<CommandAction>();
    }

    internal List<SoundEffectLibraryEntry> GlobalScopeSoundEffectLibraryEntries
    {
        get => _globalScopeSoundEffectLibraryEntries;
        set => _globalScopeSoundEffectLibraryEntries = value ?? new List<SoundEffectLibraryEntry>();
    }

    internal List<string> GlobalScopeIgnoredValidationRuleIds
    {
        get => _globalScopeIgnoredValidationRuleIds;
        set => _globalScopeIgnoredValidationRuleIds = value ?? new List<string>();
    }

    internal string GlobalScopeDisplayName
    {
        get => _globalScopeName;
        set => _globalScopeName = value ?? string.Empty;
    }

    internal string GlobalScopeDisplayProducerNotes
    {
        get => _globalScopeProducerNotes;
        set => _globalScopeProducerNotes = value ?? string.Empty;
    }

    // Legacy compatibility aliases. Keep these routed to canonical global-scope
    // storage until cutover gates permit removing old member names.
    public List<GameObject> GameObjects
    {
        get => GlobalScopeGameObjects;
        set => GlobalScopeGameObjects = value;
    }

    public List<GamePropertyDefinition> GlobalObjectVariables
    {
        get => GlobalScopeGameProperties;
        set => GlobalScopeGameProperties = value;
    }

    public List<CommandAction> GlobalObjectAvailableActions
    {
        get => GlobalScopeAvailableActions;
        set => GlobalScopeAvailableActions = value;
    }

    public List<string> GlobalObjectIgnoredValidationRuleIds
    {
        get => GlobalScopeIgnoredValidationRuleIds;
        set => GlobalScopeIgnoredValidationRuleIds = value;
    }

    public string GlobalObjectScopeName
    {
        get => GlobalScopeDisplayName;
        set => GlobalScopeDisplayName = value;
    }

    public string GlobalObjectScopeProducerNotes
    {
        get => GlobalScopeDisplayProducerNotes;
        set => GlobalScopeDisplayProducerNotes = value;
    }

    public List<GameObject> ObjectTemplates { get; set; } = new();
    public List<Room> RoomTemplates { get; set; } = new();
    public List<GameObject> BaseObjects { get; set; } = new();
    public ProjectUiState UiState { get; set; } = new();
    public List<Planet> Planets { get; set; } = new();
    public List<PhaseNode> PhaseBooks
    {
        get => _phaseBooks;
        set => _phaseBooks = value ?? new List<PhaseNode>();
    }
    public Guid? StartingPhasePageId { get; set; }

    private IEnumerable<IScopedAwareNode> BuildChildScopes()
    {
        foreach (var gameObject in GlobalScopeGameObjects)
        {
            yield return gameObject;
        }

        yield return _templatesNode;
        yield return _roomTemplatesNode;
        yield return _baseObjectsNode;

        foreach (var phaseBook in PhaseBooks)
        {
            yield return phaseBook;
        }

        foreach (var planet in Planets)
        {
            yield return planet;
        }
    }

    protected override bool CanAcceptChild(IScopedAwareNode child)
    {
        return child is GameObject
            || child.ScopeKind == ScopeNodeKind.Book
            || child.ScopeKind is ScopeNodeKind.Planet or ScopeNodeKind.Templates or ScopeNodeKind.RoomTemplates;
    }

    protected override bool TryAddChildCore(IScopedAwareNode child)
    {
        switch (child)
        {
            case GameObject gameObject:
                if (GlobalScopeGameObjects.Contains(gameObject))
                {
                    return true;
                }

                GlobalScopeGameObjects.Add(gameObject);
                return true;
            case Planet planet:
                if (Planets.Contains(planet))
                {
                    return true;
                }

                Planets.Add(planet);
                return true;
            case PhaseNode phaseNode when phaseNode.Tier == PhaseTier.Book:
                if (PhaseBooks.Contains(phaseNode))
                {
                    return true;
                }

                PhaseBooks.Add(phaseNode);
                return true;
            case ObjectTemplatesScopeNode templatesNode:
                return ReferenceEquals(_templatesNode, templatesNode);
            case RoomTemplatesScopeNode roomTemplatesNode:
                return ReferenceEquals(_roomTemplatesNode, roomTemplatesNode);
            case BaseObjectsScopeNode baseObjectsNode:
                return ReferenceEquals(_baseObjectsNode, baseObjectsNode);
            default:
                return false;
        }
    }

    protected override bool TryRemoveChildCore(IScopedAwareNode child)
    {
        if (child is GameObject gameObject)
        {
            return GlobalScopeGameObjects.Remove(gameObject);
        }

        if (child is Planet planet)
        {
            return Planets.Remove(planet);
        }

        if (child is PhaseNode phaseNode)
        {
            return PhaseBooks.Remove(phaseNode);
        }

        return false;
    }
}

