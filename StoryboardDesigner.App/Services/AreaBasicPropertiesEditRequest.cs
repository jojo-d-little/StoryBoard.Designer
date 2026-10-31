using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public sealed record AreaBasicPropertiesEditRequest(
    string Name,
    string ProducerNotes,
    AreaAdjacencyMode AdjacencyMode,
    AreaRoomDropBehavior RoomDropBehavior,
    Guid? StartingRoomId,
    List<EnvironmentalEffectDefinition>? EnvironmentalEffects = null,
    Func<Storyboard.Shared.DesignerContracts.ProjectGamePropertyComputationV2Dto?, Storyboard.Shared.DesignerContracts.ProjectGamePropertyComputationV2Dto?>? EnvironmentalEffectComputationEditor = null,
    IReadOnlyList<SharedVariableChoiceOption>? EnvironmentalEffectSharedVariableOptions = null,
    GamePropertyPickerContext? EnvironmentalEffectVariablePickerContext = null);
