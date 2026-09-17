using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public sealed class TraversalWizardDirectionSeed
{
    public required Direction Direction { get; init; }
    public required string DirectionLabel { get; init; }
    public string DestinationRoomName { get; init; } = string.Empty;
    public bool HasImmediateNeighbor { get; init; }
    public bool HasExistingTraversal { get; init; }
    public bool CanToggleSelection { get; init; }
    public bool DefaultIncludeTraversal { get; init; }
    public bool DefaultDoorEnabled { get; init; } = true;
    public TraversalWizardDoorBehaviorOption DefaultDoorBehavior { get; init; } = TraversalWizardDoorBehaviorOption.Together;
    public TraversalWizardDoorDefaultStateOption DefaultDoorState { get; init; } = TraversalWizardDoorDefaultStateOption.Closed;
    public bool DefaultDoorLockedWhenClosed { get; init; } = true;
    public Guid? DefaultDoorTemplateId { get; init; }
    public string StatusText { get; init; } = string.Empty;
}
