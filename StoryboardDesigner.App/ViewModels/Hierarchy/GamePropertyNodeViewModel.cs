using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class GamePropertyNodeViewModel : HierarchyNodeViewModel
{
    private bool _isShared;
    private int _sharedEndpointCount;

    public GamePropertyNodeViewModel(GamePropertyDefinition variable, PropertyResolutionScope scope, GamePropertiesContainerNodeViewModel parent)
        : base(variable.Name, parent)
    {
        Variable = variable;
        Scope = scope;
        VariablesContainer = parent;
        NodeTypeLabel = "Variable";
    }

    public GamePropertyDefinition Variable { get; }
    public PropertyResolutionScope Scope { get; }
    public GamePropertiesContainerNodeViewModel VariablesContainer { get; }

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
        Variable.Name = newName;
    }

    protected override string BuildNodeDetailsToolTip()
    {
        return Variable.DefaultValue ?? string.Empty;
    }

    public override bool ShouldShowValidationSummaryToolTip => true;

    protected override bool IncludeValidationDetailsInToolTip => true;
}
