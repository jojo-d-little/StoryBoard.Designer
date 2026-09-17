using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Services;

public interface ITreeContextInteractionService
{
    bool TryGetNewVariableInput(string initialName, GamePropertyLifetime initialLifetime, string initialDefaultValue, GamePropertyValueRestriction initialValueRestriction, out string variableName, out GamePropertyLifetime lifetime, out string defaultValue, out GamePropertyValueRestriction valueRestriction);
    void ShowVariableScopeReview(string scopeLabel, IList<GamePropertyDefinition> variables);
    void ShowSharedPropertyRelationships(
        string propertyPath,
        IReadOnlyList<SharedPropertyRelationshipReviewItem> relationships,
        Action? createSharedRelationshipAction = null,
        Func<SharedPropertyRelationshipReviewItem, bool>? removeSharedRelationshipAction = null,
        Func<IReadOnlyList<SharedPropertyRelationshipReviewItem>>? reloadRelationships = null,
        Guid? sharedVariableId = null,
        string? sharedVariableDisplayName = null,
        Func<Guid, string, bool>? renameSharedVariableAction = null,
        Action? openSharedVariablesManagerAction = null,
        string? shareStateCallout = null);
    void ShowSharedVariablesManager(
        IReadOnlyList<SharedVariableManagerListItem> items,
        Guid? selectedSharedVariableId = null,
        Func<Guid, string, bool>? renameSharedVariableAction = null,
        Func<Guid, bool>? dropEmptySharedVariableAction = null,
        Func<IReadOnlyList<SharedVariableManagerListItem>>? reloadItemsAction = null);
    bool TryChooseVariable(
        IReadOnlyList<GamePropertyChoiceItem> choices,
        PropertyResolutionScope initializationScope,
        string title,
        out string selectedValue,
        string? initialSelectedValue = null);
    bool EditScopedTokenList(
        string scopeLabel,
        string tokenKind,
        IList<string> values,
        IReadOnlyList<string> inheritedValues,
        IList<DirectionalTraversalMapping>? directionalMappings = null);
    bool EditScopedProcedureOwnership(
        string scopeLabel,
        IList<Guid> procedureIds,
        IReadOnlyList<ProcedureDefinition> availableProcedures);
    bool EditScopedProcedureDefinitions(
        string scopeLabel,
        IList<Guid> procedureIds,
        IList<ProcedureDefinition> allProcedures,
        IReadOnlyList<MaterializeSourceObjectChoiceItem> objectChoices,
        IReadOnlyList<GamePropertyChoiceItem> variableChoices);
    bool EditProcedureDefinitions(IList<ProcedureDefinition> procedures);
    bool EditValidationIgnoredRuleList(
        string scopeLabel,
        IList<string> values,
        IReadOnlyList<string> inheritedValues,
        IReadOnlyList<ValidationRuleCatalogItem> knownRules);
    bool TryEditGlobalSettings(
        GlobalSettingsEditRequest initialValues,
        IReadOnlyList<string> startingPlanetOptions,
        IReadOnlyList<string> playerCharacterObjectOptions,
        IReadOnlyList<SoundEffectLibraryEntry> soundEffectLibraryEntries,
        IReadOnlyList<string> soundEffectCategorySuggestions,
        out GlobalSettingsEditRequest updatedValues,
        out IReadOnlyList<SoundEffectLibraryEntry> updatedSoundEffectLibraryEntries);
    bool TryEditPlanetSettings(PlanetSettingsEditRequest initialValues, IReadOnlyList<string> startingCountryOptions, out PlanetSettingsEditRequest updatedValues);
    bool TryEditCountrySettings(CountrySettingsEditRequest initialValues, IReadOnlyList<string> startingAreaOptions, out CountrySettingsEditRequest updatedValues);
    bool TryEditRoomSettings(RoomSettingsEditRequest initialValues, out RoomSettingsEditRequest updatedValues);
    bool TryEditPhaseNode(
        PhaseNodeEditRequest initialValues,
        IReadOnlyList<SoundEffectLibraryEntry> soundEffectOptions,
        IReadOnlyList<PhaseTextPresentationCueOption> textCueOptions,
        IReadOnlyList<string> ambientTimerKeyOptions,
        out PhaseNodeEditRequest updatedValues);
    bool TryEditSoundEffectLibrary(
        IReadOnlyList<SoundEffectLibraryEntry> initialEntries,
        IReadOnlyList<string> categorySuggestions,
        out IReadOnlyList<SoundEffectLibraryEntry> updatedEntries);
    bool TryEditObjectBasicProperties(ObjectBasicPropertiesEditRequest initialValues, out ObjectBasicPropertiesEditRequest updatedValues);
    bool TryEditAreaBasicProperties(AreaBasicPropertiesEditRequest initialValues, IReadOnlyList<AreaStartingRoomOption> startingRoomOptions, out AreaBasicPropertiesEditRequest updatedValues);
    bool TrySelectObjectTemplate(IReadOnlyList<GameObject> templates, out GameObject? selectedTemplate);
    bool TrySelectRoomTemplate(IReadOnlyList<Room> templates, out Room? selectedTemplate);
    bool TryGetTemplateNameFromObject(string initialName, out string templateName);
    bool TrySelectBaseObjectPromotionScope(
        IReadOnlyList<BaseObjectPromotionScopeOption> options,
        out BaseObjectPromotionScopeKind selectedScopeKind);
    bool TrySelectQuantifiableObjectPlacement(IReadOnlyList<QuantifiableObjectPlacementCandidate> candidates, out QuantifiableObjectPlacementSelection? selection);
    bool TryReviewTraversalWizard(TraversalWizardDialogRequest request, out TraversalWizardDialogResult result);
}
