using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class GamePropertyNodeViewModel : HierarchyNodeViewModel
{
    private bool _isShared;
    private int _sharedEndpointCount;
    private string? _effectiveValue;

    public GamePropertyNodeViewModel(GamePropertyDefinition variable, PropertyResolutionScope scope, GamePropertiesContainerNodeViewModel parent, GameObject? definitionOwner = null, bool isInherited = false, bool isBaseOwned = false, Guid? baseDefinitionId = null, bool hasValueOverride = false)
        : base(variable.Name, parent)
    {
        Variable = variable;
        Scope = scope;
        VariablesContainer = parent;
        DefinitionOwner = definitionOwner ?? parent.ScopeOwner as GameObject;
        IsInherited = isInherited;
        IsBaseOwned = isBaseOwned;
        BaseDefinitionId = baseDefinitionId;
        HasValueOverride = hasValueOverride;
        NodeTypeLabel = "Variable";
    }

    public GamePropertyDefinition Variable { get; private set; }
    public PropertyResolutionScope Scope { get; }
    public GamePropertiesContainerNodeViewModel VariablesContainer { get; }
    public GameObject? DefinitionOwner { get; }
    public bool IsInherited { get; private set; }
    public bool IsBaseOwned { get; }
    public Guid? BaseDefinitionId { get; }
    public bool HasValueOverride { get; private set; }
    public bool ShowOrdinaryPropertyIcon => !IsBaseOwned;
    public bool ShowInheritedOwnershipIcon => IsBaseOwned && !HasValueOverride;
    public bool ShowLocalCopyOwnershipIcon => IsBaseOwned && HasValueOverride;
    public string OwnershipToolTip => !IsBaseOwned
        ? string.Empty
        : HasValueOverride
            ? $"Local value/sharing copy of Base Object '{DefinitionOwner?.Name ?? "Object"}'. Name and type remain Base-owned."
            : $"Inherited from Base Object '{DefinitionOwner?.Name ?? "Object"}'.";
    public string ComputedIndicatorToolTip => "Computed property; its value is evaluated from its computation.";
    public bool IsComputed => Variable.Computation is not null;
    public bool IsGeneratedEnvironmentalProperty => Variable.IsGeneratedEnvironmentalProperty;
    public string DisplayValue => Variable.GeneratedPreviewValue ?? _effectiveValue ?? Variable.DefaultValue;

    public void UpdateEffectiveValue(string value)
    {
        _effectiveValue = value;
        HasValueOverride = true;
        OnPropertyChanged(nameof(DisplayValue));
        RefreshOwnershipState();
    }

    public void RebindVariable(GamePropertyDefinition variable, bool hasValueOverride)
    {
        Variable = variable;
        HasValueOverride = hasValueOverride;
        _effectiveValue = null;
        EditableName = variable.Name;
        OnPropertyChanged(nameof(Variable));
        OnPropertyChanged(nameof(DisplayValue));
        RefreshOwnershipState();
        RefreshComputedState();
    }

    public void ResetEffectiveValue(string value)
    {
        _effectiveValue = value;
        HasValueOverride = false;
        OnPropertyChanged(nameof(DisplayValue));
        RefreshOwnershipState();
    }

    private void RefreshOwnershipState()
    {
        OnPropertyChanged(nameof(HasValueOverride));
        OnPropertyChanged(nameof(ShowOrdinaryPropertyIcon));
        OnPropertyChanged(nameof(ShowInheritedOwnershipIcon));
        OnPropertyChanged(nameof(ShowLocalCopyOwnershipIcon));
        OnPropertyChanged(nameof(OwnershipToolTip));
    }

    public void RefreshComputedState()
    {
        OnPropertyChanged(nameof(IsComputed));
    }

    public bool IsShared
    {
        get => _isShared;
        private set
        {
            if (_isShared == value)
            {
                return;
            }

            _isShared = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SharedIndicatorToolTip));
        }
    }

    public int SharedEndpointCount
    {
        get => _sharedEndpointCount;
        private set
        {
            if (_sharedEndpointCount == value)
            {
                return;
            }

            _sharedEndpointCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SharedIndicatorToolTip));
        }
    }

    public string SharedIndicatorToolTip => SharedEndpointCount <= 0
        ? "Shared"
        : $"Shared with {SharedEndpointCount} endpoint{(SharedEndpointCount == 1 ? string.Empty : "s")}";

    public void SetSharedState(int sharedEndpointCount)
    {
        SharedEndpointCount = Math.Max(0, sharedEndpointCount);
        IsShared = SharedEndpointCount > 0;
    }

    protected override void RenameModel(string newName)
    {
        if (Variable.IsGeneratedEnvironmentalProperty)
        {
            SetEditableNameWithoutModelRename(Variable.Name);
            return;
        }
        Variable.Name = newName;
    }

    protected override string BuildNodeDetailsToolTip()
    {
        return Variable.IsGeneratedEnvironmentalProperty
            ? Variable.Computation is null
                ? $"Effective phase environment value: {DisplayValue}. Edit phase time or weather settings to change it."
                : $"Read-only computed phase property; current preview: {DisplayValue}. Edit phase time or weather settings to change it."
            : DisplayValue ?? string.Empty;
    }

    public override bool ShouldShowValidationSummaryToolTip => true;

    protected override bool IncludeValidationDetailsInToolTip => true;
}
