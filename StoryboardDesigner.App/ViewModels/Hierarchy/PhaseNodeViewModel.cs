using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class PhaseNodeViewModel : HierarchyNodeViewModel
{
    public PhaseNodeViewModel(PhaseNode phaseNode, HierarchyNodeViewModel parent, bool isStartPage)
        : base(ResolveEditableName(phaseNode), parent)
    {
        PhaseNode = phaseNode;
        IsStartPage = isStartPage;
        NodeTypeLabel = isStartPage
            ? $"{phaseNode.Tier} (Start)"
            : phaseNode.Tier.ToString();
    }

    public PhaseNode PhaseNode { get; }
    public bool IsStartPage { get; }

    protected override void RenameModel(string newName)
    {
        PhaseNode.DisplayName = newName;
    }

    private static string ResolveEditableName(PhaseNode phaseNode)
    {
        if (!string.IsNullOrWhiteSpace(phaseNode.DisplayName))
        {
            return phaseNode.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(phaseNode.PhaseKey))
        {
            return phaseNode.PhaseKey;
        }

        return phaseNode.Tier.ToString();
    }
}
