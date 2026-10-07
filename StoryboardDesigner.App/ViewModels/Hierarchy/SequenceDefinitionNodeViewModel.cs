using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class SequenceDefinitionNodeViewModel : HierarchyNodeViewModel
{
    public SequenceDefinitionNodeViewModel(
        ScopeNodeBase owner,
        ProjectSequenceDefinitionDto definition,
        HierarchyNodeViewModel parent)
        : base(definition.DisplayName, parent)
    {
        Owner = owner;
        Definition = definition;
        NodeTypeLabel = "Sequence";
    }

    public ScopeNodeBase Owner { get; }
    public ProjectSequenceDefinitionDto Definition { get; }

    protected override void RenameModel(string newName)
    {
        Definition.DisplayName = newName;
    }
}
