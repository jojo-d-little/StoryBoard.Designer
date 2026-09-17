using Storyboard.Shared.RuntimeContracts.Dtos;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopedTimerDefinitionsNodeViewModel : HierarchyNodeViewModel
{
    public ScopedTimerDefinitionsNodeViewModel(PropertyResolutionScope scope, IList<RuntimeTimerDefinitionDto> timerDefinitions, HierarchyNodeViewModel parent)
        : base("Timer Definitions", parent)
    {
        Scope = scope;
        TimerDefinitions = timerDefinitions;
        NodeTypeLabel = "Timer Definitions";
    }

    public PropertyResolutionScope Scope { get; }

    public IList<RuntimeTimerDefinitionDto> TimerDefinitions { get; }

    protected override void RenameModel(string newName)
    {
    }
}
