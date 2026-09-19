using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Views;

public partial class GameObjectSettingsDialog : Window
{
    private readonly IReadOnlyList<GameObjectSelectionOption> _availableCompositePartOptions;
    private readonly IReadOnlyList<GameObjectSelectionOption> _availableLockKeyOptions;
    private string _fullImagePath = string.Empty;
    private double _imageRotationDegrees;
    private List<ObjectImageVariant> _imageVariants = new();
    private string _imageVariantChooserScript = string.Empty;
    private readonly IReadOnlyList<string> _imageVariantChooserReferenceTokens;
    private readonly IReadOnlyList<GamePropertyChoiceItem> _imageVariantChooserVariableChoices;
    private readonly PropertyResolutionScope _imageVariantChooserVariableScope;
    private readonly string _projectFilePath;
    private readonly string _preferredImageSourceBucket;
    private bool _isInitializingMovementUi = true;
    private bool _isMovable;
    private bool _isMovableDefaultValue = true;
    private RuntimeObjectSpatialTypes _spatialType = RuntimeObjectSpatialTypes.SolidObject;
    private int _stackOrder;
    private int _footprintWidthCells = 1;
    private int _footprintHeightCells = 1;
    private string _footprintOrientation = "N";
    private string _headingDirection = "N";
    private int _objectHeightUnits = 1;
    private int _heightInRoom;
    private double? _stackScaleStepOverride;
    private double? _minStackScaleOverride;
    private ObjectMovementRestrictions? _movementRestrictions;
    private bool _movementRestrictionsLockedByLink;
    private readonly int _projectRoomGridCellSize = 40;
    private readonly int _projectRoomCanvasWidth = 800;
    private readonly int _projectRoomCanvasHeight = 600;
    private LockOperationRequirements _lockOperationRequirements = new();
    private CompositeObjectSettingsEditRequest _compositeSettings = new(
        IsCompositeReversible: false,
        CompositePartRequirementMode: "AllRequired",
        CompositeMinimumRequiredPartCount: 1,
        CompositeRequiredPartObjectIds: new List<Guid>(),
        AvailablePartOptions: Array.Empty<GameObjectSelectionOption>(),
        CompositeRequiredParts: new List<CompositePartRequirement>());

    public GameObjectSettingsDialog(ObjectBasicPropertiesEditRequest initialValues)
    {
        InitializeComponent();
        ObjectNameTextBox.Text = initialValues.Name;
        ObjectNameInGameTextBox.Text = initialValues.NameInGame;
        ObjectNameSynonymsTextBox.Text = NormalizeSynonymsText(initialValues.ObjectNameSynonyms);
        ObjectProducerNotesTextBox.Text = initialValues.ProducerNotes;
        InventoriableCheckBox.IsChecked = initialValues.IsInventoriable;
        InventoryPointsTextBox.Text = SanitizePoints(initialValues.InventoryPointsDefaultValue).ToString();
        ContainerCheckBox.IsChecked = initialValues.IsContainer;
        ContainerPointsTextBox.Text = SanitizePoints(initialValues.ContainerPointsDefaultValue).ToString();
        CapacityPointShareDividerCheckBox.IsChecked = initialValues.IsContainer && initialValues.IsCapacityPointShareDividerEnabled;
        CapacityPointShareDividerTextBox.Text = SanitizePoints(initialValues.CapacityPointShareDividerDefaultValue).ToString();
        OpenableCheckBox.IsChecked = initialValues.IsOpenable;
        SetComboBoxBoolean(OpenDefaultValueComboBox, initialValues.IsOpenDefaultValue);
        LockableCheckBox.IsChecked = initialValues.IsLockable;
        SetComboBoxBoolean(LockedDefaultValueComboBox, initialValues.IsLockedDefaultValue);
        ActivatableCheckBox.IsChecked = initialValues.IsActivatable;
        SetComboBoxBoolean(ActiveDefaultValueComboBox, initialValues.IsActiveDefaultValue);
        HidableCheckBox.IsChecked = initialValues.IsHidable;
        SetComboBoxBoolean(HiddenDefaultValueComboBox, initialValues.IsHiddenDefaultValue);
        QuantifiableCheckBox.IsChecked = initialValues.IsQuantifiable;
        QuantityTextBox.Text = SanitizePoints(initialValues.Quantity).ToString();
        SetQuantifiableDistributionMode(initialValues.QuantifiablePlacementDistributionMode);
        _isMovable = initialValues.IsMovable;
        _isMovableDefaultValue = initialValues.IsMovableDefaultValue;
        _spatialType = initialValues.SpatialType;
        MovableCheckBox.IsChecked = _isMovable;
        SetComboBoxBoolean(MovableDefaultValueComboBox, _isMovableDefaultValue);
        _stackOrder = initialValues.StackGroup < 0 ? 0 : initialValues.StackGroup;
        _footprintWidthCells = SanitizePoints(initialValues.FootprintWidthCells);
        _footprintHeightCells = SanitizePoints(initialValues.FootprintHeightCells);
        _footprintOrientation = NormalizeCardinalDirection(initialValues.FootprintOrientation);
        _headingDirection = NormalizeHeadingDirection(initialValues.HeadingDirection);
        _objectHeightUnits = initialValues.ObjectHeightUnits < 0 ? 0 : initialValues.ObjectHeightUnits;
        _heightInRoom = initialValues.HeightInRoom < 0 ? 0 : initialValues.HeightInRoom;
        _stackScaleStepOverride = SanitizeOptionalFinite(initialValues.StackScaleStepOverride);
        _minStackScaleOverride = SanitizeOptionalFinite(initialValues.MinStackScaleOverride);
        _movementRestrictions = CloneMovementRestrictions(initialValues.MovementRestrictions);
        _projectRoomGridCellSize = initialValues.ProjectRoomGridCellSize > 0 ? initialValues.ProjectRoomGridCellSize : 40;
        _projectRoomCanvasWidth = initialValues.ProjectRoomCanvasWidth > 0 ? initialValues.ProjectRoomCanvasWidth : 800;
        _projectRoomCanvasHeight = initialValues.ProjectRoomCanvasHeight > 0 ? initialValues.ProjectRoomCanvasHeight : 600;
        _availableCompositePartOptions = initialValues.AvailableCompositePartOptions ?? Array.Empty<GameObjectSelectionOption>();
        _availableLockKeyOptions = initialValues.AvailableLockKeyOptions ?? _availableCompositePartOptions;
        _compositeSettings = new CompositeObjectSettingsEditRequest(
            initialValues.IsCompositeReversible,
            initialValues.CompositePartRequirementMode,
            SanitizePoints(initialValues.CompositeMinimumRequiredPartCount),
            initialValues.CompositeRequiredPartObjectIds.ToList(),
            _availableCompositePartOptions,
            NormalizeCompositeRequiredParts(initialValues.CompositeRequiredParts));
        _lockOperationRequirements = CloneLockOperationRequirements(initialValues.LockOperationRequirements);
        CompositableCheckBox.IsChecked = initialValues.IsCompositeTarget;
        ObjectDescriptionTextBox.Text = initialValues.Description;
        _imageVariants = NormalizeImageVariants(initialValues.ImageVariants, initialValues.FullImagePath);
        _imageVariantChooserScript = initialValues.ImageVariantChooserScript;
        _imageVariantChooserReferenceTokens = initialValues.ImageVariantChooserReferenceTokens ?? Array.Empty<string>();
        _imageVariantChooserVariableChoices = initialValues.ImageVariantChooserVariableChoices ?? Array.Empty<GamePropertyChoiceItem>();
        _imageVariantChooserVariableScope = initialValues.ImageVariantChooserVariableScope;
        _fullImagePath = ResolveDefaultVariantPath(_imageVariants, initialValues.FullImagePath);
        _imageRotationDegrees = initialValues.ImageRotationDegrees;
        _projectFilePath = initialValues.ProjectFilePath;
        _preferredImageSourceBucket = initialValues.PreferredImageSourceBucket;
        EnsureDefaultVariantState();
        RefreshImageSummaryAndValidationHint();
        var notice = initialValues.LinkedEditNotice?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(notice))
        {
            LinkedEditNoticeTextBlock.Text = notice;
            LinkedEditNoticeBorder.Visibility = Visibility.Visible;
            ApplyLinkedInstanceFieldLocks();
        }

        var linkedBaseObjectName = initialValues.LinkedBaseObjectName?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(linkedBaseObjectName))
        {
            LinkedBaseObjectNameTextBox.Text = linkedBaseObjectName;
            LinkedBaseObjectNameBorder.Visibility = Visibility.Visible;
        }

        _isInitializingMovementUi = false;
        RefreshCapacityPointShareDividerInputs();
        UpdateCompositeSummary();
        UpdateLockRequirementsSummary();
        RefreshMovementRestrictionsUiState();
    }

    private void ApplyLinkedInstanceFieldLocks()
    {
        // Linked instances can only edit local overrides such as Name, NameInGame, Description, and Quantity.
        ObjectProducerNotesTextBox.IsEnabled = false;
        ObjectNameSynonymsTextBox.IsEnabled = false;

        InventoriableCheckBox.IsEnabled = false;
        InventoryPointsTextBox.IsEnabled = false;

        ContainerCheckBox.IsEnabled = false;
        ContainerPointsTextBox.IsEnabled = false;
        CapacityPointShareDividerCheckBox.IsEnabled = false;
        CapacityPointShareDividerTextBox.IsEnabled = false;

        OpenableCheckBox.IsEnabled = false;
        OpenDefaultValueComboBox.IsEnabled = false;
        LockableCheckBox.IsEnabled = false;
        LockedDefaultValueComboBox.IsEnabled = false;
        ActivatableCheckBox.IsEnabled = false;
        ActiveDefaultValueComboBox.IsEnabled = false;
        HidableCheckBox.IsEnabled = false;
        HiddenDefaultValueComboBox.IsEnabled = false;
        MovableCheckBox.IsEnabled = false;
        MovableDefaultValueComboBox.IsEnabled = false;

        QuantifiableCheckBox.IsEnabled = false;
        QuantifiableDistributionModeComboBox.IsEnabled = false;

        CompositableCheckBox.IsEnabled = false;
        ManageCompositeButton.IsEnabled = false;
        ManageLockRequirementsButton.IsEnabled = false;
        ManageImageVariantsButton.IsEnabled = false;
        _movementRestrictionsLockedByLink = true;
        ManageMovementRestrictionsButton.IsEnabled = false;
    }

    public ObjectBasicPropertiesEditRequest Values
    {
        get
        {
            EnsureDefaultVariantState();
            _fullImagePath = ResolveDefaultVariantPath(_imageVariants, _fullImagePath);

            return new ObjectBasicPropertiesEditRequest(
                ObjectNameTextBox.Text.Trim(),
                ObjectNameInGameTextBox.Text.Trim(),
                ObjectProducerNotesTextBox.Text,
                InventoriableCheckBox.IsChecked == true,
                ParseInventoryPointsText(),
                ContainerCheckBox.IsChecked == true,
                ParseContainerPointsText(),
                ContainerCheckBox.IsChecked == true && CapacityPointShareDividerCheckBox.IsChecked == true,
                ParseCapacityPointShareDividerText(),
                OpenableCheckBox.IsChecked == true,
                GetComboBoxBoolean(OpenDefaultValueComboBox),
                LockableCheckBox.IsChecked == true,
                GetComboBoxBoolean(LockedDefaultValueComboBox),
                ActivatableCheckBox.IsChecked == true,
                GetComboBoxBoolean(ActiveDefaultValueComboBox),
                HidableCheckBox.IsChecked == true,
                GetComboBoxBoolean(HiddenDefaultValueComboBox),
                QuantifiableCheckBox.IsChecked == true,
                ParseQuantityText(),
                GetQuantifiableDistributionMode(),
                CompositableCheckBox.IsChecked == true,
                _compositeSettings.IsCompositeReversible,
                _compositeSettings.CompositePartRequirementMode,
                _compositeSettings.CompositeMinimumRequiredPartCount,
                _compositeSettings.CompositeRequiredPartObjectIds,
                _availableCompositePartOptions,
                ObjectDescriptionTextBox.Text,
                _fullImagePath,
                _imageRotationDegrees,
                _projectFilePath,
                _preferredImageSourceBucket,
                ImageVariantChooserReferenceTokens: _imageVariantChooserReferenceTokens,
                ImageVariantChooserVariableChoices: _imageVariantChooserVariableChoices,
                ImageVariantChooserVariableScope: _imageVariantChooserVariableScope,
                ImageVariants: _imageVariants,
                ImageVariantChooserScript: _imageVariantChooserScript,
                LockOperationRequirements: CloneLockOperationRequirements(_lockOperationRequirements),
                AvailableLockKeyOptions: _availableLockKeyOptions,
                CompositeRequiredParts: NormalizeCompositeRequiredParts(_compositeSettings.CompositeRequiredParts),
                ObjectNameSynonyms: NormalizeSynonymsText(ObjectNameSynonymsTextBox.Text),
                IsMovable: _isMovable,
                IsMovableDefaultValue: _isMovableDefaultValue,
                SpatialType: _spatialType,
                StackGroup: _spatialType == RuntimeObjectSpatialTypes.PassiveObject ? 0 : _stackOrder,
                FootprintWidthCells: _footprintWidthCells,
                FootprintHeightCells: _footprintHeightCells,
                FootprintOrientation: _footprintOrientation,
                HeadingDirection: _headingDirection,
                ObjectHeightUnits: _objectHeightUnits,
                HeightInRoom: _heightInRoom,
                StackScaleStepOverride: _stackScaleStepOverride,
                MinStackScaleOverride: _minStackScaleOverride,
                MovementRestrictions: CloneMovementRestrictions(_movementRestrictions),
                ProjectRoomGridCellSize: _projectRoomGridCellSize,
                ProjectRoomCanvasWidth: _projectRoomCanvasWidth,
                ProjectRoomCanvasHeight: _projectRoomCanvasHeight,
                LinkedBaseObjectName: LinkedBaseObjectNameTextBox.Text.Trim());
        }
    }

    private static string NormalizeSynonymsText(string? raw)
    {
        return string.Join(", ",
            (raw ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(static token => token.Trim())
                .Where(static token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static LockOperationRequirements CloneLockOperationRequirements(LockOperationRequirements? source)
    {
        var requirements = source ?? new LockOperationRequirements();
        return new LockOperationRequirements
        {
            UnlockKeyRequirements = (requirements.UnlockKeyRequirements ?? new List<LockKeyRequirement>())
                .Where(static requirement => requirement.RequiredObjectId != Guid.Empty)
                .Select(static requirement => new LockKeyRequirement
                {
                    RequiredObjectId = requirement.RequiredObjectId,
                    RequiredQuantity = requirement.RequiredQuantity < 1 ? 1 : requirement.RequiredQuantity,
                    MatchKind = requirement.MatchKind,
                    MatchValue = requirement.MatchValue,
                    SatisfactionMode = requirement.SatisfactionMode,
                    ConsumptionPolicy = requirement.ConsumptionPolicy,
                    IsOptional = requirement.IsOptional,
                    VariableRequirements = (requirement.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                        .Where(static item => !string.IsNullOrWhiteSpace(item.VariableName))
                        .Select(static item => new LockParticipantVariableRequirement
                        {
                            VariableName = item.VariableName.Trim(),
                            Operator = item.Operator,
                            ExpectedValue = item.ExpectedValue,
                            QuantityEvaluationMode = item.QuantityEvaluationMode
                        })
                        .ToList()
                })
                .ToList(),
            RequireKeyForLockOperation = requirements.RequireKeyForLockOperation,
            UseUnlockKeysForLockOperation = requirements.UseUnlockKeysForLockOperation,
            LockKeyRequirements = (requirements.LockKeyRequirements ?? new List<LockKeyRequirement>())
                .Where(static requirement => requirement.RequiredObjectId != Guid.Empty)
                .Select(static requirement => new LockKeyRequirement
                {
                    RequiredObjectId = requirement.RequiredObjectId,
                    RequiredQuantity = requirement.RequiredQuantity < 1 ? 1 : requirement.RequiredQuantity,
                    MatchKind = requirement.MatchKind,
                    MatchValue = requirement.MatchValue,
                    SatisfactionMode = requirement.SatisfactionMode,
                    ConsumptionPolicy = requirement.ConsumptionPolicy,
                    IsOptional = requirement.IsOptional,
                    VariableRequirements = (requirement.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                        .Where(static item => !string.IsNullOrWhiteSpace(item.VariableName))
                        .Select(static item => new LockParticipantVariableRequirement
                        {
                            VariableName = item.VariableName.Trim(),
                            Operator = item.Operator,
                            ExpectedValue = item.ExpectedValue,
                            QuantityEvaluationMode = item.QuantityEvaluationMode
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    private void ManageImageVariantsButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ObjectImageVariantsDialog(new ObjectImageVariantsEditRequest(
            _imageVariants,
            _imageVariantChooserScript,
            _imageRotationDegrees,
            _projectFilePath,
            _preferredImageSourceBucket,
            _imageVariantChooserReferenceTokens,
            _imageVariantChooserVariableChoices,
            _imageVariantChooserVariableScope,
            IsMovable: _isMovable,
            IsMovableDefaultValue: _isMovableDefaultValue,
            SpatialType: _spatialType,
            StackGroup: _stackOrder,
            FootprintWidthCells: _footprintWidthCells,
            FootprintHeightCells: _footprintHeightCells,
            FootprintOrientation: _footprintOrientation,
            HeadingDirection: _headingDirection,
            ObjectHeightUnits: _objectHeightUnits,
            HeightInRoom: _heightInRoom,
            StackScaleStepOverride: _stackScaleStepOverride,
            MinStackScaleOverride: _minStackScaleOverride,
            ProjectRoomGridCellSize: _projectRoomGridCellSize,
            ProjectRoomCanvasWidth: _projectRoomCanvasWidth,
            ProjectRoomCanvasHeight: _projectRoomCanvasHeight))
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _imageVariants = NormalizeImageVariants(dialog.ImageVariants, _fullImagePath);
        _imageVariantChooserScript = dialog.ImageVariantChooserScript;
        _imageRotationDegrees = dialog.ImageRotationDegrees;
        _isMovable = dialog.IsMovable;
        _isMovableDefaultValue = dialog.IsMovableDefaultValue;
        _spatialType = dialog.SpatialType;
        MovableCheckBox.IsChecked = _isMovable;
        SetComboBoxBoolean(MovableDefaultValueComboBox, _isMovableDefaultValue);
        _stackOrder = dialog.StackGroup;
        _footprintWidthCells = dialog.FootprintWidthCells;
        _footprintHeightCells = dialog.FootprintHeightCells;
        _footprintOrientation = dialog.FootprintOrientation;
        _headingDirection = dialog.HeadingDirection;
        _objectHeightUnits = dialog.ObjectHeightUnits;
        _heightInRoom = dialog.HeightInRoom;
        _stackScaleStepOverride = dialog.StackScaleStepOverride;
        _minStackScaleOverride = dialog.MinStackScaleOverride;
        _fullImagePath = ResolveDefaultVariantPath(_imageVariants, _fullImagePath);
        RefreshImageSummaryAndValidationHint();
    }

    private static List<ObjectImageVariant> NormalizeImageVariants(IReadOnlyList<ObjectImageVariant>? variants, string legacyFullPath)
    {
        const double normalizedScale = 1;
        var normalized = (variants ?? Array.Empty<ObjectImageVariant>())
            .Where(static variant => variant is not null && !string.IsNullOrWhiteSpace(variant.VariantName))
            .Select(variant => new ObjectImageVariant
            {
                VariantName = variant.VariantName.Trim(),
                FullImagePath = variant.FullImagePath?.Trim() ?? string.Empty,
                ImageLocalAlignmentRotationDegrees = double.IsFinite(variant.ImageLocalAlignmentRotationDegrees) ? variant.ImageLocalAlignmentRotationDegrees : 0,
                ImageLocalAlignmentOffsetX = double.IsFinite(variant.ImageLocalAlignmentOffsetX) ? variant.ImageLocalAlignmentOffsetX : 0,
                ImageLocalAlignmentOffsetY = double.IsFinite(variant.ImageLocalAlignmentOffsetY) ? variant.ImageLocalAlignmentOffsetY : 0,
                ImageScale = variant.ImageScale <= 0 ? 1 : variant.ImageScale,
                IsDefault = variant.IsDefault
            })
            .GroupBy(static variant => variant.VariantName, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();

        if (normalized.Count == 0 && !string.IsNullOrWhiteSpace(legacyFullPath))
        {
            normalized.Add(new ObjectImageVariant
            {
                VariantName = "default",
                FullImagePath = legacyFullPath.Trim(),
                ImageLocalAlignmentRotationDegrees = 0,
                ImageLocalAlignmentOffsetX = 0,
                ImageLocalAlignmentOffsetY = 0,
                ImageScale = normalizedScale,
                IsDefault = true
            });
            return normalized;
        }

        var defaultIndex = normalized.FindIndex(static variant => variant.IsDefault);
        if (defaultIndex >= 0)
        {
            for (var index = 0; index < normalized.Count; index++)
            {
                normalized[index].IsDefault = index == defaultIndex;
            }

            return normalized;
        }

        if (normalized.Count == 1)
        {
            normalized[0].IsDefault = true;
        }

        return normalized;
    }

    private static string ResolveDefaultVariantPath(IReadOnlyList<ObjectImageVariant> variants, string fallbackPath)
    {
        var explicitDefault = variants.FirstOrDefault(static variant => variant.IsDefault);
        if (explicitDefault is not null && !string.IsNullOrWhiteSpace(explicitDefault.FullImagePath))
        {
            return explicitDefault.FullImagePath;
        }

        if (variants.Count == 1 && !string.IsNullOrWhiteSpace(variants[0].FullImagePath))
        {
            return variants[0].FullImagePath;
        }

        var firstPath = variants.FirstOrDefault(static variant => !string.IsNullOrWhiteSpace(variant.FullImagePath))?.FullImagePath;
        if (!string.IsNullOrWhiteSpace(firstPath))
        {
            return firstPath;
        }

        return fallbackPath;
    }

    private void UpdateDefaultVariantPath(string path)
    {
        var normalizedPath = path?.Trim() ?? string.Empty;
        var explicitDefault = _imageVariants.FirstOrDefault(static variant => variant.IsDefault);
        if (explicitDefault is not null)
        {
            explicitDefault.FullImagePath = normalizedPath;
            return;
        }

        if (_imageVariants.Count == 1)
        {
            _imageVariants[0].IsDefault = true;
            _imageVariants[0].FullImagePath = normalizedPath;
            return;
        }

        _imageVariants.Insert(0, new ObjectImageVariant
        {
            VariantName = "default",
            FullImagePath = normalizedPath,
            ImageLocalAlignmentRotationDegrees = 0,
            ImageLocalAlignmentOffsetX = 0,
            ImageLocalAlignmentOffsetY = 0,
            ImageScale = 1,
            IsDefault = true
        });
    }

    private void EnsureDefaultVariantState()
    {
        if (_imageVariants.Count == 0)
        {
            _imageVariants.Add(new ObjectImageVariant
            {
                VariantName = "default",
                FullImagePath = _fullImagePath,
                IsDefault = true
            });
            return;
        }

        var defaultIndex = _imageVariants.FindIndex(static variant => variant.IsDefault);
        if (defaultIndex < 0)
        {
            _imageVariants[0].IsDefault = true;
            return;
        }

        for (var index = 0; index < _imageVariants.Count; index++)
        {
            _imageVariants[index].IsDefault = index == defaultIndex;
        }
    }

    private void RefreshImageSummaryAndValidationHint()
    {
        EnsureDefaultVariantState();
        _fullImagePath = ResolveDefaultVariantPath(_imageVariants, _fullImagePath);

        UpdateChooserValidationHint();
    }

    private void UpdateChooserValidationHint()
    {
        var chooser = _imageVariantChooserScript ?? string.Empty;
        var availableNames = new HashSet<string>(_imageVariants.Select(static variant => variant.VariantName), StringComparer.OrdinalIgnoreCase);
        var referencedNames = ExtractReferencedVariantNames(chooser);
        var unknown = referencedNames.Where(name => !availableNames.Contains(name)).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();

        if (unknown.Count == 0)
        {
            ImageVariantValidationHintTextBlock.Visibility = Visibility.Collapsed;
            ImageVariantValidationHintTextBlock.Text = string.Empty;
            return;
        }

        ImageVariantValidationHintTextBlock.Text = $"Chooser references unknown variants: {string.Join(", ", unknown)}.";
        ImageVariantValidationHintTextBlock.Visibility = Visibility.Visible;
    }

    private static HashSet<string> ExtractReferencedVariantNames(string chooser)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(chooser))
        {
            return names;
        }

        foreach (Match match in Regex.Matches(chooser, "'([^'\\r\\n]+)'") )
        {
            var candidate = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                names.Add(candidate);
            }
        }

        foreach (Match match in Regex.Matches(chooser, "\"([^\"\\r\\n]+)\"") )
        {
            var candidate = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                names.Add(candidate);
            }
        }

        return names;
    }

    private static bool GetComboBoxBoolean(System.Windows.Controls.ComboBox comboBox)
    {
        if (comboBox.SelectedItem is not System.Windows.Controls.ComboBoxItem selectedItem)
        {
            return false;
        }

        return string.Equals(selectedItem.Content?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetComboBoxBoolean(System.Windows.Controls.ComboBox comboBox, bool value)
    {
        comboBox.SelectedIndex = value ? 1 : 0;
    }

    private int ParseInventoryPointsText()
    {
        if (!int.TryParse(InventoryPointsTextBox.Text.Trim(), out var parsedValue))
        {
            return 1;
        }

        return SanitizePoints(parsedValue);
    }

    private int ParseContainerPointsText()
    {
        if (!int.TryParse(ContainerPointsTextBox.Text.Trim(), out var parsedValue))
        {
            return 1;
        }

        return SanitizePoints(parsedValue);
    }

    private int ParseCapacityPointShareDividerText()
    {
        if (!int.TryParse(CapacityPointShareDividerTextBox.Text.Trim(), out var parsedValue))
        {
            return 1;
        }

        return SanitizePoints(parsedValue);
    }

    private int ParseQuantityText()
    {
        if (!int.TryParse(QuantityTextBox.Text.Trim(), out var parsedValue))
        {
            return 1;
        }

        return SanitizePoints(parsedValue);
    }

    private string GetQuantifiableDistributionMode()
    {
        return QuantifiableDistributionModeComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem
            ? selectedItem.Content?.ToString()?.Trim() ?? "GroupedStack"
            : "GroupedStack";
    }

    private void SetQuantifiableDistributionMode(string mode)
    {
        var normalized = string.IsNullOrWhiteSpace(mode) ? "GroupedStack" : mode.Trim();
        foreach (var item in QuantifiableDistributionModeComboBox.Items.OfType<System.Windows.Controls.ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                QuantifiableDistributionModeComboBox.SelectedItem = item;
                return;
            }
        }

        QuantifiableDistributionModeComboBox.SelectedIndex = 0;
    }

    private static int SanitizePoints(int value)
    {
        return value < 1 ? 1 : value;
    }

    private void InventoryPointsTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void InventoryPointsTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(System.Windows.DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.SourceDataObject.GetData(System.Windows.DataFormats.Text) as string;
        if (string.IsNullOrWhiteSpace(pastedText) || !pastedText.All(char.IsDigit))
        {
            e.CancelCommand();
        }
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ObjectNameTextBox.Text))
        {
            System.Windows.MessageBox.Show(this, "Please enter an object name.", "Object Properties", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!int.TryParse(InventoryPointsTextBox.Text.Trim(), out var parsedInventoryPoints) || parsedInventoryPoints < 1)
        {
            System.Windows.MessageBox.Show(this, "Inventory points must be a number greater than or equal to 1.", "Object Properties", MessageBoxButton.OK, MessageBoxImage.Information);
            InventoryPointsTextBox.Text = "1";
            InventoryPointsTextBox.Focus();
            InventoryPointsTextBox.SelectAll();
            return;
        }

        if (!int.TryParse(ContainerPointsTextBox.Text.Trim(), out var parsedContainerPoints) || parsedContainerPoints < 1)
        {
            System.Windows.MessageBox.Show(this, "Container points must be a number greater than or equal to 1.", "Object Properties", MessageBoxButton.OK, MessageBoxImage.Information);
            ContainerPointsTextBox.Text = "1";
            ContainerPointsTextBox.Focus();
            ContainerPointsTextBox.SelectAll();
            return;
        }

        if (ContainerCheckBox.IsChecked == true
            && CapacityPointShareDividerCheckBox.IsChecked == true
            && (!int.TryParse(CapacityPointShareDividerTextBox.Text.Trim(), out var parsedCapacityPointShareDivider)
                || parsedCapacityPointShareDivider < 1))
        {
            System.Windows.MessageBox.Show(this, "Capacity point share divider must be a number greater than or equal to 1.", "Object Properties", MessageBoxButton.OK, MessageBoxImage.Information);
            CapacityPointShareDividerTextBox.Text = "1";
            CapacityPointShareDividerTextBox.Focus();
            CapacityPointShareDividerTextBox.SelectAll();
            return;
        }

        if (QuantifiableCheckBox.IsChecked == true
            && (!int.TryParse(QuantityTextBox.Text.Trim(), out var parsedQuantity) || parsedQuantity < 1))
        {
            System.Windows.MessageBox.Show(this, "Quantity must be a number greater than or equal to 1.", "Object Properties", MessageBoxButton.OK, MessageBoxImage.Information);
            QuantityTextBox.Text = "1";
            QuantityTextBox.Focus();
            QuantityTextBox.SelectAll();
            return;
        }

        if (CompositableCheckBox.IsChecked == true)
        {
            if (!ValidateCompositeSettings())
            {
                return;
            }
        }

        if (LockableCheckBox.IsChecked == true
            && _lockOperationRequirements.RequireKeyForLockOperation
            && _lockOperationRequirements.LockKeyRequirements.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "Lock key requirement is enabled, but no lock keys are configured. Click Manage to add lock requirements.", "Object Properties", MessageBoxButton.OK, MessageBoxImage.Information);
            ManageLockRequirementsButton.Focus();
            return;
        }

        DialogResult = true;
    }

    private void ContainerCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        RefreshCapacityPointShareDividerInputs();
    }

    private void CapacityPointShareDividerCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        RefreshCapacityPointShareDividerInputs();
    }

    private void MovableCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializingMovementUi)
        {
            return;
        }

        _isMovable = MovableCheckBox.IsChecked == true;
        _isMovableDefaultValue = GetComboBoxBoolean(MovableDefaultValueComboBox);

        if (!_isMovable)
        {
            _isMovableDefaultValue = false;
            SetComboBoxBoolean(MovableDefaultValueComboBox, false);
        }

        RefreshMovementRestrictionsUiState();
    }

    private void ManageMovementRestrictionsButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ObjectMovementRestrictionsDialog(CloneMovementRestrictions(_movementRestrictions))
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var updatedMovementRestrictions = CloneMovementRestrictions(dialog.Value);
        _movementRestrictions = updatedMovementRestrictions is not null && updatedMovementRestrictions.HasAnyRule()
            ? updatedMovementRestrictions
            : null;
        RefreshMovementRestrictionsUiState();
    }

    private void RefreshMovementRestrictionsUiState()
    {
        if (_isInitializingMovementUi
            || ManageMovementRestrictionsButton is null)
        {
            return;
        }

        var canEdit = _isMovable && !_movementRestrictionsLockedByLink;
        ManageMovementRestrictionsButton.IsEnabled = canEdit;
    }

    private static ObjectMovementRestrictions? CloneMovementRestrictions(ObjectMovementRestrictions? source)
    {
        if (source is null)
        {
            return null;
        }

        return new ObjectMovementRestrictions
        {
            MultiLegMaxTotalDistanceCells = source.MultiLegMaxTotalDistanceCells,
            FirstUnstacked = CloneMovementRestrictionCategory(source.FirstUnstacked),
            FirstStacked = CloneMovementRestrictionCategory(source.FirstStacked),
            SubsequentUnstacked = CloneMovementRestrictionCategory(source.SubsequentUnstacked),
            SubsequentStacked = CloneMovementRestrictionCategory(source.SubsequentStacked)
        };
    }

    private static ObjectMovementRestrictionCategory CloneMovementRestrictionCategory(ObjectMovementRestrictionCategory source)
    {
        return new ObjectMovementRestrictionCategory
        {
            N = CloneMovementRestrictionRule(source.N),
            NE = CloneMovementRestrictionRule(source.NE),
            E = CloneMovementRestrictionRule(source.E),
            SE = CloneMovementRestrictionRule(source.SE),
            S = CloneMovementRestrictionRule(source.S),
            SW = CloneMovementRestrictionRule(source.SW),
            W = CloneMovementRestrictionRule(source.W),
            NW = CloneMovementRestrictionRule(source.NW)
        };
    }

    private static ObjectMovementRestrictionRule CloneMovementRestrictionRule(ObjectMovementRestrictionRule source)
    {
        return new ObjectMovementRestrictionRule
        {
            MaxDistance = source.MaxDistance,
            AllowJumpOver = source.AllowJumpOver
        };
    }

    private void RefreshCapacityPointShareDividerInputs()
    {
        var containerEnabled = ContainerCheckBox.IsChecked == true;
        CapacityPointShareDividerCheckBox.IsEnabled = containerEnabled;
        if (!containerEnabled)
        {
            CapacityPointShareDividerCheckBox.IsChecked = false;
        }

        CapacityPointShareDividerTextBox.IsEnabled = containerEnabled && CapacityPointShareDividerCheckBox.IsChecked == true;
    }

    private void ManageLockRequirementsButton_OnClick(object sender, RoutedEventArgs e)
    {
        var clone = CloneLockOperationRequirements(_lockOperationRequirements);
        var dialog = new LockOperationRequirementsDialog(new LockOperationRequirementsEditRequest(
            UnlockKeyRequirements: clone.UnlockKeyRequirements,
            RequireKeyForLockOperation: clone.RequireKeyForLockOperation,
            UseUnlockKeysForLockOperation: clone.UseUnlockKeysForLockOperation,
            LockKeyRequirements: clone.LockKeyRequirements,
            AvailableKeyOptions: _availableLockKeyOptions))
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _lockOperationRequirements = CloneLockOperationRequirements(dialog.Values);
        UpdateLockRequirementsSummary();
    }

    private void LockableCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        UpdateLockRequirementsSummary();
    }

    private void ManageCompositeButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new CompositeObjectSettingsDialog(_compositeSettings)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _compositeSettings = dialog.Values;
        UpdateCompositeSummary();
    }

    private void CompositableCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        UpdateCompositeSummary();
    }

    private bool ValidateCompositeSettings()
    {
        var partCount = GetCompositeRequiredPartCount(_compositeSettings.CompositeRequiredParts);
        if (partCount == 0)
        {
            System.Windows.MessageBox.Show(this, "Compositable objects require at least one part. Click Manage to configure composite settings.", "Object Properties", MessageBoxButton.OK, MessageBoxImage.Information);
            ManageCompositeButton.Focus();
            return false;
        }

        return true;
    }

    private void UpdateCompositeSummary()
    {
        CompositeSummaryTextBlock.Text = CompositableCheckBox.IsChecked == true
            ? BuildCompositeSummary()
            : "Composite settings are disabled.";
    }

    private string BuildCompositeSummary()
    {
        var count = GetCompositeRequiredPartCount(_compositeSettings.CompositeRequiredParts);
        var reversible = _compositeSettings.IsCompositeReversible ? "Reversible" : "Not reversible";
        return $"Parts: {count}. {reversible}.";
    }

    private static List<CompositePartRequirement> NormalizeCompositeRequiredParts(
        IReadOnlyList<CompositePartRequirement>? parts)
    {
        return (parts ?? Array.Empty<CompositePartRequirement>())
            .Where(static part => part.PartObjectId != Guid.Empty)
            .Select(static part => new CompositePartRequirement
            {
                PartObjectId = part.PartObjectId,
                PartObjectName = part.PartObjectName,
                RequiredQuantity = part.RequiredQuantity < 1 ? 1 : part.RequiredQuantity,
                MatchKind = part.MatchKind,
                MatchValue = part.MatchValue,
                SatisfactionMode = part.SatisfactionMode,
                ConsumptionPolicy = part.ConsumptionPolicy,
                OptionalPart = part.OptionalPart,
                VariableRequirements = (part.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                    .Where(static requirement => !string.IsNullOrWhiteSpace(requirement.VariableName))
                    .Select(static requirement => new LockParticipantVariableRequirement
                    {
                        VariableName = requirement.VariableName.Trim(),
                        Operator = requirement.Operator,
                        ExpectedValue = requirement.ExpectedValue,
                        QuantityEvaluationMode = requirement.QuantityEvaluationMode
                    })
                    .ToList()
            })
            .ToList();
    }

    private static int GetCompositeRequiredPartCount(IReadOnlyList<CompositePartRequirement>? parts)
    {
        return (parts ?? Array.Empty<CompositePartRequirement>())
            .Where(static part => part.PartObjectId != Guid.Empty)
            .Sum(static part => Math.Max(1, part.RequiredQuantity));
    }

    private void UpdateLockRequirementsSummary()
    {
        LockRequirementsSummaryTextBlock.Text = LockableCheckBox.IsChecked == true
            ? BuildLockRequirementsSummary()
            : "Lock requirements are disabled.";
    }

    private string BuildLockRequirementsSummary()
    {
        var unlockCount = _lockOperationRequirements.UnlockKeyRequirements.Count;

        if (!_lockOperationRequirements.RequireKeyForLockOperation)
        {
            return $"Unlock keys: {unlockCount}. Lock keys not required.";
        }

        var lockCount = _lockOperationRequirements.LockKeyRequirements.Count;
        if (_lockOperationRequirements.UseUnlockKeysForLockOperation)
        {
            return $"Unlock keys: {unlockCount}. Lock keys: same as unlock.";
        }

        return $"Unlock keys: {unlockCount}. Lock keys: {lockCount}.";
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void MovableDefaultValueComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingMovementUi)
        {
            return;
        }

        _isMovableDefaultValue = GetComboBoxBoolean(MovableDefaultValueComboBox);
        if (_isMovableDefaultValue && MovableCheckBox.IsChecked != true)
        {
            MovableCheckBox.IsChecked = true;
            _isMovable = true;
        }
    }

    private static string NormalizeCardinalDirection(string? direction)
    {
        var value = direction?.Trim().ToUpperInvariant();
        return value is "N" or "E" or "S" or "W"
            ? value
            : "N";
    }

    private static string NormalizeHeadingDirection(string? direction)
    {
        var value = direction?.Trim().ToUpperInvariant();
        return value is "N" or "NE" or "E" or "SE" or "S" or "SW" or "W" or "NW"
            ? value
            : "N";
    }

    private static double? SanitizeOptionalFinite(double? value)
    {
        return value.HasValue && double.IsFinite(value.Value)
            ? value
            : null;
    }
}
