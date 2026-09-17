using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public sealed class TraversalWizardDialogRequest
{
    public required string SourceRoomName { get; init; }
    public required string AreaName { get; init; }
    public required AreaAdjacencyMode EffectiveTraversalMode { get; init; }
    public required IReadOnlyList<TraversalWizardDoorTemplateOption> DoorTemplateOptions { get; init; }
    public required IReadOnlyList<TraversalWizardDirectionSeed> Rows { get; init; }
}
