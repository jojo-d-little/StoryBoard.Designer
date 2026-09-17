using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public sealed class TraversalWizardDirectionChoice
{
    public required Direction Direction { get; init; }
    public required bool HasImmediateNeighbor { get; init; }
    public required bool HasExistingTraversal { get; init; }
    public required bool IncludeTraversal { get; init; }
    public required bool DoorEnabled { get; init; }
    public required TraversalWizardDoorBehaviorOption DoorBehavior { get; init; }
    public required TraversalWizardDoorDefaultStateOption DoorState { get; init; }
    public required bool DoorLockedWhenClosed { get; init; }
    public Guid? DoorTemplateId { get; init; }
    public string StatusText { get; init; } = string.Empty;
}
