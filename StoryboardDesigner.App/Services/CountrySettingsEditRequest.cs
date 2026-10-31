namespace StoryboardDesigner.App.Services;

using StoryboardDesigner.App.Models;

public sealed record CountrySettingsEditRequest(string Name, string ProducerNotes, string StartingAreaName, List<EnvironmentalEffectDefinition>? EnvironmentalEffects = null,
    Func<Storyboard.Shared.DesignerContracts.ProjectGamePropertyComputationV2Dto?, Storyboard.Shared.DesignerContracts.ProjectGamePropertyComputationV2Dto?>? EnvironmentalEffectComputationEditor = null,
    IReadOnlyList<SharedVariableChoiceOption>? EnvironmentalEffectSharedVariableOptions = null,
    GamePropertyPickerContext? EnvironmentalEffectVariablePickerContext = null);
