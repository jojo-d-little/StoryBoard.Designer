using Storyboard.Shared.RuntimeContracts.Dtos;

namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopedTimerDefinitionEntryNodeViewModel : HierarchyNodeViewModel
{
    public ScopedTimerDefinitionEntryNodeViewModel(RuntimeTimerDefinitionDto entry, ScopedTimerDefinitionsNodeViewModel parent)
        : base(ResolveDisplayName(entry), parent)
    {
        Entry = entry;
        ParentTimerDefinitionsNode = parent;
        NodeTypeLabel = "Timer Definition";
    }

    public RuntimeTimerDefinitionDto Entry { get; }

    public ScopedTimerDefinitionsNodeViewModel ParentTimerDefinitionsNode { get; }

    private static string ResolveDisplayName(RuntimeTimerDefinitionDto entry)
    {
        var timerKey = entry.TimerKey?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(timerKey) ? "(Missing Timer Key)" : timerKey;
    }

    protected override void RenameModel(string newName)
    {
    }
}
