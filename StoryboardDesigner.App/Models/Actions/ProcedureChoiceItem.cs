namespace StoryboardDesigner.App.Models;

public sealed class ProcedureChoiceItem
{
    public Guid ProcedureId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string OwnerLabel { get; init; } = string.Empty;
    public string ScopePath { get; init; } = string.Empty;
}
