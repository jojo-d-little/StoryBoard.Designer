using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;
namespace StoryboardDesigner.App.Services;

public interface IMainWindowDialogWorkflowService
{
    bool EditScopedActions(
        string scopeLabel,
        PropertyResolutionScope variableScope,
        string scopeEntityName,
        IList<CommandAction> actions,
        IReadOnlyList<LinkedActionTargetChoice> additionalLinkTargetActions,
        IReadOnlyList<ContainerTargetChoiceItem> containerTargetChoices,
        IReadOnlyList<MaterializeSourceObjectChoiceItem> materializeSourceObjectChoices,
        IReadOnlyList<ProcedureChoiceItem> procedureChoices,
        IReadOnlyList<CompositeRecipeChoiceItem> compositeRecipeChoices,
        IReadOnlyList<SoundEffectChoiceItem> soundEffectChoices,
        IReadOnlyList<SequenceDefinitionChoiceItem> sequenceDefinitionChoices,
        GamePropertyPickerContext pickerContext,
        IReadOnlyList<string> timerKeySuggestions,
        IReadOnlyList<string> verbSuggestions,
        IReadOnlyList<string> directionalSuggestions,
        Func<IReadOnlyList<CommandAction>, IReadOnlyList<ProjectValidationIssue>>? validateWithPipeline = null,
        Guid? defaultCurrentObjectId = null);

    bool EditSingleScopedAction(
        CommandAction action,
        PropertyResolutionScope variableScope,
        GamePropertyPickerContext pickerContext,
        IReadOnlyList<ContainerTargetChoiceItem> containerTargetChoices,
        IReadOnlyList<MaterializeSourceObjectChoiceItem> materializeSourceObjectChoices,
        IReadOnlyList<ProcedureChoiceItem> procedureChoices,
        IReadOnlyList<CompositeRecipeChoiceItem> compositeRecipeChoices,
        IReadOnlyList<SoundEffectChoiceItem> soundEffectChoices,
        IReadOnlyList<string> timerKeySuggestions,
        IReadOnlyList<SequenceDefinitionChoiceItem> sequenceDefinitionChoices,
        IReadOnlyList<CommandAction> availableActions,
        Guid? defaultCurrentObjectId = null,
        IReadOnlyList<string>? verbSuggestions = null,
        IReadOnlyList<string>? objectSetKeySuggestions = null);

    bool EditLinkedActionFlow(
        string scopeLabel,
        CommandAction action,
        IList<CommandAction> editableActions,
        IReadOnlyList<LinkedActionTargetChoice> externalActionChoices,
        PropertyResolutionScope variableScope,
        GamePropertyPickerContext pickerContext,
        IReadOnlyList<ContainerTargetChoiceItem> containerTargetChoices,
        IReadOnlyList<MaterializeSourceObjectChoiceItem> materializeSourceObjectChoices,
        IReadOnlyList<ProcedureChoiceItem> procedureChoices,
        IReadOnlyList<CompositeRecipeChoiceItem> compositeRecipeChoices,
        IReadOnlyList<SoundEffectChoiceItem> soundEffectChoices,
        IReadOnlyList<string> timerKeySuggestions,
        IReadOnlyList<SequenceDefinitionChoiceItem> sequenceDefinitionChoices,
        IReadOnlyList<string>? verbSuggestions = null);
}
