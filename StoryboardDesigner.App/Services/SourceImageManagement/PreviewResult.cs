namespace StoryboardDesigner.App.Services;

public sealed class PreviewResult
{
    public List<PreviewItem> Items { get; } = new();

    public int ActionableCount => Items.Count(static item => item.ApplyAction is not null);

    public string BuildSummary(SourceImageManagementMode mode)
    {
        var total = Items.Count;
        var actionable = ActionableCount;
        return $"Mode={mode}; References={total}; Actionable={actionable}.";
    }
}
