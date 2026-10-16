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
        IReadOnlyList<(CommandAction Action, string ScopeName, LinkedActionTargetGroup Group)> additionalLinkTargetActions,
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
        Guid? defaultCurrentObjectId = null)
    {
        var dialog = new ScopedActionsDialog(scopeLabel, variableScope, scopeEntityName, actions, additionalLinkTargetActions, containerTargetChoices, materializeSourceObjectChoices, procedureChoices, compositeRecipeChoices, soundEffectChoices, sequenceDefinitionChoices, pickerContext, timerKeySuggestions, verbSuggestions, directionalSuggestions, validateWithPipeline, defaultCurrentObjectId)
        {
            Owner = GetOwnerWindow()
        };

        return dialog.ShowDialog() == true;
    }

    public bool EditSingleScopedAction(
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
        Guid? defaultCurrentObjectId = null)
    {
        var dialog = new RoomActionEditorDialog(
            action,
            variableScope,
            pickerContext,
            containerTargetChoices,
            materializeSourceObjectChoices,
            procedureChoices,
            compositeRecipeChoices,
            soundEffectChoices,
            timerKeySuggestions,
            availableActions,
            sequenceDefinitionChoices,
            defaultCurrentObjectId)
        {
            Owner = GetOwnerWindow()
        };

        return dialog.ShowDialog() == true;
    }

    public bool EditLinkedActionFlow(
        string scopeLabel,
        CommandAction action,
        IList<CommandAction> editableActions,
        IReadOnlyList<(CommandAction Action, string ScopeName, LinkedActionTargetGroup Group)> externalActionChoices,
        PropertyResolutionScope variableScope,
        GamePropertyPickerContext pickerContext,
        IReadOnlyList<ContainerTargetChoiceItem> containerTargetChoices,
        IReadOnlyList<MaterializeSourceObjectChoiceItem> materializeSourceObjectChoices,
        IReadOnlyList<ProcedureChoiceItem> procedureChoices,
        IReadOnlyList<CompositeRecipeChoiceItem> compositeRecipeChoices,
        IReadOnlyList<SoundEffectChoiceItem> soundEffectChoices,
        IReadOnlyList<string> timerKeySuggestions,
        IReadOnlyList<SequenceDefinitionChoiceItem> sequenceDefinitionChoices)
    {
        var dialog = new LinkedActionTreeEditorDialog(
            scopeLabel,
            action,
            editableActions,
            pickerContext,
            externalActionChoices,
            variableScope,
            containerTargetChoices,
            materializeSourceObjectChoices,
            procedureChoices,
            compositeRecipeChoices,
            soundEffectChoices,
            timerKeySuggestions,
            sequenceDefinitionChoices)
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
