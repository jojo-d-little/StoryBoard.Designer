using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;
using Storyboard.Shared.GameStateData;

namespace StoryboardDesigner.App.Services;

public interface IMainWindowDialogWorkflowService
{
    bool EditScopedActions(
        string scopeLabel,
        PropertyResolutionScope variableScope,
        string scopeEntityName,
        IList<CommandAction> actions,
        IReadOnlyList<CommandAction> additionalLinkTargetActions,
        IReadOnlyList<ContainerTargetChoiceItem> containerTargetChoices,
        IReadOnlyList<MaterializeSourceObjectChoiceItem> materializeSourceObjectChoices,
        IReadOnlyList<ProcedureChoiceItem> procedureChoices,
        IReadOnlyList<CompositeRecipeChoiceItem> compositeRecipeChoices,
        IReadOnlyList<SoundEffectChoiceItem> soundEffectChoices,
        IReadOnlyList<GamePropertyChoiceItem> variableChoices,
        IReadOnlyList<string> echoReferenceTokens,
        IReadOnlyList<string> timerKeySuggestions,
        IReadOnlyList<string> verbSuggestions,
        IReadOnlyList<string> directionalSuggestions,
        Func<IReadOnlyList<CommandAction>, IReadOnlyList<ProjectValidationIssue>>? validateWithPipeline = null);

    bool EditSingleScopedAction(
        CommandAction action,
        PropertyResolutionScope variableScope,
        IReadOnlyList<GamePropertyChoiceItem> variableChoices,
        IReadOnlyList<string> echoReferenceTokens,
        IReadOnlyList<ContainerTargetChoiceItem> containerTargetChoices,
        IReadOnlyList<MaterializeSourceObjectChoiceItem> materializeSourceObjectChoices,
        IReadOnlyList<ProcedureChoiceItem> procedureChoices,
        IReadOnlyList<CompositeRecipeChoiceItem> compositeRecipeChoices,
        IReadOnlyList<SoundEffectChoiceItem> soundEffectChoices,
        IReadOnlyList<string> timerKeySuggestions,
        IReadOnlyList<CommandAction> availableActions);
}
