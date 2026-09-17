using System.Windows;
using Storyboard.Shared.GameServices;
using Storyboard.Shared.GameStateData;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;
using StoryboardDesigner.App.Views;

namespace StoryboardDesigner.App.Services;

public sealed class MainWindowDialogWorkflowService : IMainWindowDialogWorkflowService
{
    public bool EditScopedActions(
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
        Func<IReadOnlyList<CommandAction>, IReadOnlyList<ProjectValidationIssue>>? validateWithPipeline = null)
    {
        var dialog = new ScopedActionsDialog(scopeLabel, variableScope, scopeEntityName, actions, additionalLinkTargetActions, containerTargetChoices, materializeSourceObjectChoices, procedureChoices, compositeRecipeChoices, soundEffectChoices, variableChoices, echoReferenceTokens, timerKeySuggestions, verbSuggestions, directionalSuggestions, validateWithPipeline)
        {
            Owner = GetOwnerWindow()
        };

        return dialog.ShowDialog() == true;
    }

    public bool EditSingleScopedAction(
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
        IReadOnlyList<CommandAction> availableActions)
    {
        var dialog = new RoomActionEditorDialog(
            action,
            variableScope,
            variableChoices,
            echoReferenceTokens,
            containerTargetChoices,
            materializeSourceObjectChoices,
            procedureChoices,
            compositeRecipeChoices,
            soundEffectChoices,
            timerKeySuggestions,
            availableActions)
        {
            Owner = GetOwnerWindow()
        };

        return dialog.ShowDialog() == true;
    }

    private static Window? GetOwnerWindow()
    {
        var activeWindow = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        return activeWindow ?? System.Windows.Application.Current?.MainWindow;
    }
}
