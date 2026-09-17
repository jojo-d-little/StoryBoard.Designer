using System.Windows;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;
using StoryboardDesigner.App.Views;

namespace StoryboardDesigner.App.Services;

public sealed class TreeContextInteractionService : ITreeContextInteractionService
{
    public bool TryGetNewVariableInput(string initialName, GamePropertyLifetime initialLifetime, string initialDefaultValue, GamePropertyValueRestriction initialValueRestriction, out string variableName, out GamePropertyLifetime lifetime, out string defaultValue, out GamePropertyValueRestriction valueRestriction)
    {
        var dialog = new VariableEditorDialog(initialName, initialLifetime, initialDefaultValue, initialValueRestriction)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            variableName = initialName;
            lifetime = initialLifetime;
            defaultValue = initialDefaultValue;
            valueRestriction = initialValueRestriction;
            return false;
        }

        variableName = dialog.VariableName;
        lifetime = dialog.Lifetime;
        defaultValue = dialog.DefaultValue;
        valueRestriction = dialog.ValueRestriction;
        return true;
    }

    public void ShowVariableScopeReview(string scopeLabel, IList<GamePropertyDefinition> variables)
    {
        var dialog = new VariableScopeReviewDialog(scopeLabel, variables)
        {
            Owner = GetOwnerWindow()
        };

        dialog.ShowDialog();
    }

    public void ShowSharedPropertyRelationships(
        string propertyPath,
        IReadOnlyList<SharedPropertyRelationshipReviewItem> relationships,
        Action? createSharedRelationshipAction = null,
        Func<SharedPropertyRelationshipReviewItem, bool>? removeSharedRelationshipAction = null,
        Func<IReadOnlyList<SharedPropertyRelationshipReviewItem>>? reloadRelationships = null,
        Guid? sharedVariableId = null,
        string? sharedVariableDisplayName = null,
        Func<Guid, string, bool>? renameSharedVariableAction = null,
        Action? openSharedVariablesManagerAction = null,
        string? shareStateCallout = null)
    {
        var owner = GetOwnerWindow();
        var dialog = new SharedPropertyRelationshipsDialog(
            propertyPath,
            relationships,
            createSharedRelationshipAction,
            removeSharedRelationshipAction,
            reloadRelationships,
            sharedVariableId,
            sharedVariableDisplayName,
            renameSharedVariableAction,
            openSharedVariablesManagerAction,
            shareStateCallout)
        {
            Owner = owner
        };

        dialog.ShowDialog();

        if (owner is MainWindow mainWindow)
        {
            mainWindow.RestoreHierarchyFocusToSelection();
        }
    }

    public void ShowSharedVariablesManager(
        IReadOnlyList<SharedVariableManagerListItem> items,
        Guid? selectedSharedVariableId = null,
        Func<Guid, string, bool>? renameSharedVariableAction = null,
        Func<Guid, bool>? dropEmptySharedVariableAction = null,
        Func<IReadOnlyList<SharedVariableManagerListItem>>? reloadItemsAction = null)
    {
        var dialog = new SharedVariablesManagerDialog(
            items,
            selectedSharedVariableId,
            renameSharedVariableAction,
            dropEmptySharedVariableAction,
            reloadItemsAction)
        {
            Owner = GetOwnerWindow()
        };

        dialog.ShowDialog();
    }

    public bool TryChooseVariable(
        IReadOnlyList<GamePropertyChoiceItem> choices,
        PropertyResolutionScope initializationScope,
        string title,
        out string selectedValue,
        string? initialSelectedValue = null)
    {
        var dialog = new VariableChooserDialog(choices, initializationScope, title, initialSelectedValue)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.SelectedValue))
        {
            selectedValue = string.Empty;
            return false;
        }

        selectedValue = dialog.SelectedValue;
        return true;
    }

    public bool EditScopedTokenList(
        string scopeLabel,
        string tokenKind,
        IList<string> values,
        IReadOnlyList<string> inheritedValues,
        IList<DirectionalTraversalMapping>? directionalMappings = null)
    {
        var dialog = new ScopedTokenListDialog(scopeLabel, tokenKind, values, inheritedValues, directionalMappings)
        {
            Owner = GetOwnerWindow()
        };

        return dialog.ShowDialog() == true;
    }

    public bool EditScopedProcedureOwnership(
        string scopeLabel,
        IList<Guid> procedureIds,
        IReadOnlyList<ProcedureDefinition> availableProcedures)
    {
        var dialog = new ScopedProcedureOwnershipDialog(scopeLabel, procedureIds, availableProcedures)
        {
            Owner = GetOwnerWindow()
        };

        return dialog.ShowDialog() == true;
    }

    public bool EditProcedureDefinitions(IList<ProcedureDefinition> procedures)
    {
        var dialog = new ProcedureDesignerDialog(procedures)
        {
            Owner = GetOwnerWindow()
        };

        return dialog.ShowDialog() == true;
    }

    public bool EditScopedProcedureDefinitions(
        string scopeLabel,
        IList<Guid> procedureIds,
        IList<ProcedureDefinition> allProcedures,
        IReadOnlyList<MaterializeSourceObjectChoiceItem> objectChoices,
        IReadOnlyList<GamePropertyChoiceItem> variableChoices)
    {
        var dialog = new ProcedureDesignerDialog(scopeLabel, procedureIds, allProcedures, objectChoices, variableChoices)
        {
            Owner = GetOwnerWindow()
        };

        return dialog.ShowDialog() == true;
    }

    public bool EditValidationIgnoredRuleList(
        string scopeLabel,
        IList<string> values,
        IReadOnlyList<string> inheritedValues,
        IReadOnlyList<ValidationRuleCatalogItem> knownRules)
    {
        var dialog = new ValidationIgnoredRulesDialog(scopeLabel, values, inheritedValues, knownRules)
        {
            Owner = GetOwnerWindow()
        };

        return dialog.ShowDialog() == true;
    }

    public bool TryEditGlobalSettings(
        GlobalSettingsEditRequest initialValues,
        IReadOnlyList<string> startingPlanetOptions,
        IReadOnlyList<string> playerCharacterObjectOptions,
        IReadOnlyList<SoundEffectLibraryEntry> soundEffectLibraryEntries,
        IReadOnlyList<string> soundEffectCategorySuggestions,
        out GlobalSettingsEditRequest updatedValues,
        out IReadOnlyList<SoundEffectLibraryEntry> updatedSoundEffectLibraryEntries)
    {
        var dialog = new GlobalSettingsDialog(initialValues, startingPlanetOptions, playerCharacterObjectOptions, soundEffectLibraryEntries, soundEffectCategorySuggestions)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            updatedValues = initialValues;
            updatedSoundEffectLibraryEntries = soundEffectLibraryEntries.ToList();
            return false;
        }

        updatedValues = dialog.Values;
        updatedSoundEffectLibraryEntries = dialog.SoundEffectLibraryEntries.ToList();
        return true;
    }

    public bool TryEditPlanetSettings(PlanetSettingsEditRequest initialValues, IReadOnlyList<string> startingCountryOptions, out PlanetSettingsEditRequest updatedValues)
    {
        var dialog = new PlanetSettingsDialog(initialValues, startingCountryOptions)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            updatedValues = initialValues;
            return false;
        }

        updatedValues = dialog.Values;
        return true;
    }

    public bool TryEditCountrySettings(CountrySettingsEditRequest initialValues, IReadOnlyList<string> startingAreaOptions, out CountrySettingsEditRequest updatedValues)
    {
        var dialog = new CountrySettingsDialog(initialValues, startingAreaOptions)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            updatedValues = initialValues;
            return false;
        }

        updatedValues = dialog.Values;
        return true;
    }

    public bool TryEditRoomSettings(RoomSettingsEditRequest initialValues, out RoomSettingsEditRequest updatedValues)
    {
        var dialog = new RoomSettingsDialog(initialValues)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            updatedValues = initialValues;
            return false;
        }

        updatedValues = dialog.Values;
        return true;
    }

    public bool TryEditPhaseNode(
        PhaseNodeEditRequest initialValues,
        IReadOnlyList<SoundEffectLibraryEntry> soundEffectOptions,
        IReadOnlyList<PhaseTextPresentationCueOption> textCueOptions,
        IReadOnlyList<string> ambientTimerKeyOptions,
        out PhaseNodeEditRequest updatedValues)
    {
        var dialog = new PhaseNodeSettingsDialog(initialValues, soundEffectOptions, textCueOptions, ambientTimerKeyOptions)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            updatedValues = initialValues;
            return false;
        }

        updatedValues = dialog.Values;
        return true;
    }

    public bool TryEditSoundEffectLibrary(
        IReadOnlyList<SoundEffectLibraryEntry> initialEntries,
        IReadOnlyList<string> categorySuggestions,
        out IReadOnlyList<SoundEffectLibraryEntry> updatedEntries)
    {
        var dialog = new SoundEffectLibraryDialog(initialEntries, categorySuggestions)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            updatedEntries = initialEntries.ToList();
            return false;
        }

        updatedEntries = dialog.Entries;
        return true;
    }

    public bool TryEditObjectBasicProperties(ObjectBasicPropertiesEditRequest initialValues, out ObjectBasicPropertiesEditRequest updatedValues)
    {
        var dialog = new GameObjectSettingsDialog(initialValues)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            updatedValues = initialValues;
            return false;
        }

        updatedValues = dialog.Values;
        return true;
    }

    public bool TryEditAreaBasicProperties(AreaBasicPropertiesEditRequest initialValues, IReadOnlyList<AreaStartingRoomOption> startingRoomOptions, out AreaBasicPropertiesEditRequest updatedValues)
    {
        var dialog = new AreaPropertiesDialog(initialValues, startingRoomOptions)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            updatedValues = initialValues;
            return false;
        }

        updatedValues = dialog.Values;
        return true;
    }

    public bool TrySelectObjectTemplate(IReadOnlyList<GameObject> templates, out GameObject? selectedTemplate)
    {
        var dialog = new ObjectTemplatePickerDialog(templates)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            selectedTemplate = null;
            return false;
        }

        selectedTemplate = dialog.SelectedTemplate;
        return true;
    }

    public bool TrySelectRoomTemplate(IReadOnlyList<Room> templates, out Room? selectedTemplate)
    {
        var dialog = new RoomTemplatePickerDialog(templates)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            selectedTemplate = null;
            return false;
        }

        selectedTemplate = dialog.SelectedTemplate;
        return true;
    }

    public bool TryGetTemplateNameFromObject(string initialName, out string templateName)
    {
        var dialog = new TemplateNameDialog(initialName)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            templateName = initialName;
            return false;
        }

        templateName = dialog.TemplateName;
        return true;
    }

    public bool TrySelectBaseObjectPromotionScope(
        IReadOnlyList<BaseObjectPromotionScopeOption> options,
        out BaseObjectPromotionScopeKind selectedScopeKind)
    {
        var dialog = new BaseObjectPromotionScopeDialog(options)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            selectedScopeKind = BaseObjectPromotionScopeKind.Area;
            return false;
        }

        selectedScopeKind = dialog.SelectedScopeKind;
        return true;
    }

    public bool TrySelectQuantifiableObjectPlacement(IReadOnlyList<QuantifiableObjectPlacementCandidate> candidates, out QuantifiableObjectPlacementSelection? selection)
    {
        var dialog = new QuantifiableObjectPlacementDialog(candidates)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            selection = null;
            return false;
        }

        selection = dialog.Selection;
        return selection is not null;
    }

    public bool TryReviewTraversalWizard(TraversalWizardDialogRequest request, out TraversalWizardDialogResult result)
    {
        var dialog = new TraversalWizardDialog(request)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            result = TraversalWizardDialogResult.Empty;
            return false;
        }

        result = dialog.Result;
        return true;
    }

    private static Window? GetOwnerWindow()
    {
        var activeWindow = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        return activeWindow ?? System.Windows.Application.Current?.MainWindow;
    }
}
