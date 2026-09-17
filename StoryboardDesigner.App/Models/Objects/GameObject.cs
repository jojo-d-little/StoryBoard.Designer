using System.ComponentModel;
using System.Runtime.CompilerServices;
using Storyboard.Shared.Config;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Models;

public sealed class GameObject : ScopeNodeBase, INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _objectType = string.Empty;
    private string _nameInGame = string.Empty;
    private string _producerNotes = string.Empty;
    private bool _isInventoriable;
    private bool _isContainer;
    private bool _isOpenable;
    private bool _isLockable;
    private bool _isActivatable;
    private bool _isHidable;
    private bool _isMovable;
    private bool _isMovableDefaultValue = true;
    private RuntimeObjectSpatialTypes _spatialType = RuntimeObjectSpatialTypes.SolidObject;
    private bool _isCapacityPointShareDividerEnabled;
    private bool _isOpenDefaultValue;
    private bool _isLockedDefaultValue;
    private bool _isActiveDefaultValue;
    private bool _isHiddenDefaultValue;
    private bool _isQuantifiable;
    private bool _isCompositeTarget;
    private bool _isCompositeReversible;
    private int _inventoryPointsDefaultValue = 1;
    private int _containerPointsDefaultValue = 1;
    private int _capacityPointShareDividerDefaultValue = 1;
    private int _stackOrder;
    private int _footprintWidthCells = 1;
    private int _footprintHeightCells = 1;
    private int _objectHeightUnits = 1;
    private int _heightInRoom;
    private int _authoredBaseHeightInRoom;
    private int _quantity = 1;
    private int _compositeMinimumRequiredPartCount = 1;
    private string _footprintOrientation = "N";
    private string _headingDirection = "N";
    private string _quantifiablePlacementDistributionMode = "GroupedStack";
    private string _compositePartRequirementMode = "AllRequired";
    private string _description = string.Empty;
    private string _imageVariantChooserScript = string.Empty;
    private List<ObjectImageVariant> _imageVariants = new();
    private double _imageRotationDegrees;
    private double _positionX;
    private double _positionY;
    private int _renderZOrder;
    private int _authoredRenderOrder;
    private bool _isHeightPinned;
    private List<string> _occupiedCellIds = new();
    private string _occupancyDerivationSourceEcho = string.Empty;
    private double? _stackScaleStepOverride;
    private double? _minStackScaleOverride;
    private ObjectMovementRestrictions? _movementRestrictions;
    private bool _includeInPreview = true;
    private Guid? _linkedBaseObjectId;
    private bool _linkActionsToBaseObject;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            var priorName = _name;
            _name = value;

            // Keep ObjectType aligned with Name until explicitly customized.
            if (string.IsNullOrWhiteSpace(_objectType)
                || string.Equals(_objectType, priorName, StringComparison.Ordinal))
            {
                _objectType = value ?? string.Empty;
                OnPropertyChanged(nameof(ObjectType));
            }

            OnPropertyChanged();
        }
    }

    public string ObjectType
    {
        get => string.IsNullOrWhiteSpace(_objectType) ? Name : _objectType;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                normalized = Name;
            }

            if (string.Equals(ObjectType, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _objectType = normalized;
            OnPropertyChanged();
        }
    }

    public string ProducerNotes
    {
        get => _producerNotes;
        set
        {
            if (_producerNotes == value)
            {
                return;
            }

            _producerNotes = value;
            OnPropertyChanged();
        }
    }

    public string NameInGame
    {
        get => _nameInGame;
        set
        {
            if (_nameInGame == value)
            {
                return;
            }

            _nameInGame = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasNameInGame));
            OnPropertyChanged(nameof(ScopeNameInGame));
        }
    }

    public bool HasNameInGame => !string.IsNullOrWhiteSpace(NameInGame);

    public bool IsInventoriable
    {
        get => _isInventoriable;
        set
        {
            if (_isInventoriable == value)
            {
                return;
            }

            _isInventoriable = value;

            if (_isInventoriable && !_isMovable)
            {
                _isMovable = true;
                _isMovableDefaultValue = true;
                OnPropertyChanged(nameof(IsMovable));
                OnPropertyChanged(nameof(IsMovableDefaultValue));
            }

            ApplyFeatureVariableContract();

            OnPropertyChanged();
        }
    }

    public bool IsMovable
    {
        get => _isMovable;
        set
        {
            if (_isMovable == value)
            {
                return;
            }

            _isMovable = value;
            ApplyFeatureVariableContract();
            OnPropertyChanged();
        }
    }

    public bool IsMovableDefaultValue
    {
        get => _isMovableDefaultValue;
        set
        {
            if (_isMovableDefaultValue == value)
            {
                return;
            }

            _isMovableDefaultValue = value;
            ApplyFeatureVariableContract();
            OnPropertyChanged();
        }
    }

    public int StackGroup
    {
        get => _stackOrder;
        set
        {
            if (SpatialType == RuntimeObjectSpatialTypes.PassiveObject
                && value != 0)
            {
                value = 0;
            }

            var sanitized = value < 0 ? 0 : value;
            if (_stackOrder == sanitized)
            {
                return;
            }

            _stackOrder = sanitized;
            OnPropertyChanged();
        }
    }

    public RuntimeObjectSpatialTypes SpatialType
    {
        get => _spatialType;
        set
        {
            var normalized = value;
            if (!Enum.IsDefined(normalized))
            {
                normalized = RuntimeObjectSpatialTypes.SolidObject;
            }

            if (_spatialType == normalized)
            {
                return;
            }

            _spatialType = normalized;
            if (_spatialType == RuntimeObjectSpatialTypes.PassiveObject
                && _stackOrder != 0)
            {
                _stackOrder = 0;
                OnPropertyChanged(nameof(StackGroup));
                OnPropertyChanged(nameof(StackOrder));
            }

            OnPropertyChanged();
        }
    }

    public int StackOrder
    {
        get => StackGroup;
        set => StackGroup = value;
    }

    public int FootprintWidthCells
    {
        get => _footprintWidthCells;
        set
        {
            var sanitized = value < 1 ? 1 : value;
            if (_footprintWidthCells == sanitized)
            {
                return;
            }

            _footprintWidthCells = sanitized;
            OnPropertyChanged();
        }
    }

    public int FootprintHeightCells
    {
        get => _footprintHeightCells;
        set
        {
            var sanitized = value < 1 ? 1 : value;
            if (_footprintHeightCells == sanitized)
            {
                return;
            }

            _footprintHeightCells = sanitized;
            OnPropertyChanged();
        }
    }

    public string FootprintOrientation
    {
        get => _footprintOrientation;
        set
        {
            var normalized = NormalizeCardinalDirection(value);
            if (string.Equals(_footprintOrientation, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _footprintOrientation = normalized;
            OnPropertyChanged();
        }
    }

    public string HeadingDirection
    {
        get => _headingDirection;
        set
        {
            var normalized = NormalizeHeadingDirection(value);
            if (string.Equals(_headingDirection, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _headingDirection = normalized;
            OnPropertyChanged();
        }
    }

    public int ObjectHeightUnits
    {
        get => _objectHeightUnits;
        set
        {
            var sanitized = value < 0 ? 0 : value;
            if (_objectHeightUnits == sanitized)
            {
                return;
            }

            _objectHeightUnits = sanitized;
            OnPropertyChanged();
        }
    }

    public int HeightInRoom
    {
        get => _heightInRoom;
        set
        {
            var sanitized = value < 0 ? 0 : value;
            if (_heightInRoom == sanitized)
            {
                return;
            }

            _heightInRoom = sanitized;
            OnPropertyChanged();
        }
    }

    public int AuthoredBaseHeightInRoom
    {
        get => _authoredBaseHeightInRoom;
        set
        {
            var sanitized = value < 0 ? 0 : value;
            if (_authoredBaseHeightInRoom == sanitized)
            {
                return;
            }

            _authoredBaseHeightInRoom = sanitized;
            OnPropertyChanged();
        }
    }

    public bool IsHeightPinned
    {
        get => _isHeightPinned;
        set
        {
            if (_isHeightPinned == value)
            {
                return;
            }

            _isHeightPinned = value;
            OnPropertyChanged();
        }
    }

    public List<string> OccupiedCellIds
    {
        get => _occupiedCellIds;
        set
        {
            var normalized = (value ?? new List<string>())
                .Where(static cell => !string.IsNullOrWhiteSpace(cell))
                .Select(static cell => cell.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (_occupiedCellIds.SequenceEqual(normalized, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            _occupiedCellIds = normalized;
            OnPropertyChanged();
        }
    }

    public string OccupancyDerivationSourceEcho
    {
        get => _occupancyDerivationSourceEcho;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_occupancyDerivationSourceEcho, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _occupancyDerivationSourceEcho = normalized;
            OnPropertyChanged();
        }
    }

    public double? StackScaleStepOverride
    {
        get => _stackScaleStepOverride;
        set
        {
            var sanitized = value.HasValue && double.IsFinite(value.Value)
                ? StackScalePolicy.ClampStackScaleStep(value.Value)
                : (double?)null;
            if (Nullable.Equals(_stackScaleStepOverride, sanitized))
            {
                return;
            }

            _stackScaleStepOverride = sanitized;
            OnPropertyChanged();
        }
    }

    public double? MinStackScaleOverride
    {
        get => _minStackScaleOverride;
        set
        {
            var sanitized = value.HasValue && double.IsFinite(value.Value)
                ? StackScalePolicy.ClampMinStackScale(value.Value)
                : (double?)null;
            if (Nullable.Equals(_minStackScaleOverride, sanitized))
            {
                return;
            }

            _minStackScaleOverride = sanitized;
            OnPropertyChanged();
        }
    }

    public ObjectMovementRestrictions? MovementRestrictions
    {
        get => _movementRestrictions;
        set
        {
            if (ReferenceEquals(_movementRestrictions, value))
            {
                return;
            }

            _movementRestrictions = value;
            OnPropertyChanged();
        }
    }

    public int InventoryPointsDefaultValue
    {
        get => _inventoryPointsDefaultValue;
        set
        {
            var sanitizedValue = value < 1 ? 1 : value;
            if (_inventoryPointsDefaultValue == sanitizedValue)
            {
                return;
            }

            _inventoryPointsDefaultValue = sanitizedValue;
            ApplyFeatureVariableContract();

            OnPropertyChanged();
        }
    }

    public bool IsContainer
    {
        get => _isContainer;
        set
        {
            if (_isContainer == value)
            {
                return;
            }

            _isContainer = value;
            if (!_isContainer)
            {
                _isCapacityPointShareDividerEnabled = false;
                OnPropertyChanged(nameof(IsCapacityPointShareDividerEnabled));
            }

            ApplyFeatureVariableContract();

            OnPropertyChanged();
        }
    }

    public int ContainerPointsDefaultValue
    {
        get => _containerPointsDefaultValue;
        set
        {
            var sanitizedValue = value < 1 ? 1 : value;
            if (_containerPointsDefaultValue == sanitizedValue)
            {
                return;
            }

            _containerPointsDefaultValue = sanitizedValue;
            ApplyFeatureVariableContract();

            OnPropertyChanged();
        }
    }

    public bool IsCapacityPointShareDividerEnabled
    {
        get => _isCapacityPointShareDividerEnabled;
        set
        {
            var normalized = IsContainer && value;
            if (_isCapacityPointShareDividerEnabled == normalized)
            {
                return;
            }

            _isCapacityPointShareDividerEnabled = normalized;
            ApplyFeatureVariableContract();

            OnPropertyChanged();
        }
    }

    public int CapacityPointShareDividerDefaultValue
    {
        get => _capacityPointShareDividerDefaultValue;
        set
        {
            var sanitizedValue = value < 1 ? 1 : value;
            if (_capacityPointShareDividerDefaultValue == sanitizedValue)
            {
                return;
            }

            _capacityPointShareDividerDefaultValue = sanitizedValue;
            ApplyFeatureVariableContract();

            OnPropertyChanged();
        }
    }

    public bool IsOpenable
    {
        get => _isOpenable;
        set
        {
            if (_isOpenable == value)
            {
                return;
            }

            _isOpenable = value;
            ApplyFeatureVariableContract();

            OnPropertyChanged();
        }
    }

    public bool IsLockable
    {
        get => _isLockable;
        set
        {
            if (_isLockable == value)
            {
                return;
            }

            _isLockable = value;
            ApplyFeatureVariableContract();

            OnPropertyChanged();
        }
    }

    public bool IsActivatable
    {
        get => _isActivatable;
        set
        {
            if (_isActivatable == value)
            {
                return;
            }

            _isActivatable = value;
            ApplyFeatureVariableContract();

            OnPropertyChanged();
        }
    }

    public bool IsHidable
    {
        get => _isHidable;
        set
        {
            if (_isHidable == value)
            {
                return;
            }

            _isHidable = value;
            ApplyFeatureVariableContract();

            OnPropertyChanged();
        }
    }

    public bool IsOpenDefaultValue
    {
        get => _isOpenDefaultValue;
        set
        {
            if (_isOpenDefaultValue == value)
            {
                return;
            }

            _isOpenDefaultValue = value;
            ApplyFeatureVariableContract();

            OnPropertyChanged();
        }
    }

    public bool IsLockedDefaultValue
    {
        get => _isLockedDefaultValue;
        set
        {
            if (_isLockedDefaultValue == value)
            {
                return;
            }

            _isLockedDefaultValue = value;
            ApplyFeatureVariableContract();

            OnPropertyChanged();
        }
    }

    public bool IsActiveDefaultValue
    {
        get => _isActiveDefaultValue;
        set
        {
            if (_isActiveDefaultValue == value)
            {
                return;
            }

            _isActiveDefaultValue = value;
            ApplyFeatureVariableContract();

            OnPropertyChanged();
        }
    }

    public bool IsHiddenDefaultValue
    {
        get => _isHiddenDefaultValue;
        set
        {
            if (_isHiddenDefaultValue == value)
            {
                return;
            }

            _isHiddenDefaultValue = value;
            ApplyFeatureVariableContract();

            OnPropertyChanged();
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (_description == value)
            {
                return;
            }

            _description = value;
            OnPropertyChanged();
        }
    }

    public string ImageVariantChooserScript
    {
        get => _imageVariantChooserScript;
        set
        {
            if (_imageVariantChooserScript == value)
            {
                return;
            }

            _imageVariantChooserScript = value;
            OnPropertyChanged();
        }
    }

    public double ImageRotationDegrees
    {
        get => _imageRotationDegrees;
        set
        {
            if (Math.Abs(_imageRotationDegrees - value) < 0.0001)
            {
                return;
            }

            _imageRotationDegrees = value;
            OnPropertyChanged();
        }
    }

    public double PositionX
    {
        get => _positionX;
        set
        {
            var sanitized = double.IsFinite(value) ? value : 0;
            if (Math.Abs(_positionX - sanitized) < 0.0001)
            {
                return;
            }

            _positionX = sanitized;
            OnPropertyChanged();
        }
    }

    public double PositionY
    {
        get => _positionY;
        set
        {
            var sanitized = double.IsFinite(value) ? value : 0;
            if (Math.Abs(_positionY - sanitized) < 0.0001)
            {
                return;
            }

            _positionY = sanitized;
            OnPropertyChanged();
        }
    }

    public bool IncludeInPreview
    {
        get => _includeInPreview;
        set
        {
            if (_includeInPreview == value)
            {
                return;
            }

            _includeInPreview = value;
            OnPropertyChanged();
        }
    }

    public int RenderZOrder
    {
        get => _renderZOrder;
        set
        {
            if (_renderZOrder == value)
            {
                return;
            }

            _renderZOrder = value;
            OnPropertyChanged();
        }
    }

    public int AuthoredRenderOrder
    {
        get => _authoredRenderOrder;
        set
        {
            var sanitized = value < 0 ? 0 : value;
            if (_authoredRenderOrder == sanitized)
            {
                return;
            }

            _authoredRenderOrder = sanitized;
            OnPropertyChanged();
        }
    }

    public Guid? LinkedBaseObjectId
    {
        get => _linkedBaseObjectId;
        set
        {
            if (_linkedBaseObjectId == value)
            {
                return;
            }

            _linkedBaseObjectId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsRoomInstance));
        }
    }

    public bool LinkActionsToBaseObject
    {
        get => _linkActionsToBaseObject;
        set
        {
            if (_linkActionsToBaseObject == value)
            {
                return;
            }

            _linkActionsToBaseObject = value;
            OnPropertyChanged();
        }
    }

    public bool IsRoomInstance => LinkedBaseObjectId.HasValue;

    public bool IsQuantifiable
    {
        get => _isQuantifiable;
        set
        {
            if (_isQuantifiable == value)
            {
                return;
            }

            _isQuantifiable = value;
            ApplyFeatureVariableContract();
            OnPropertyChanged();
        }
    }

    public int Quantity
    {
        get => _quantity;
        set
        {
            var sanitizedValue = value < 1 ? 1 : value;
            if (_quantity == sanitizedValue)
            {
                return;
            }

            _quantity = sanitizedValue;
            ApplyFeatureVariableContract();
            OnPropertyChanged();
        }
    }

    public string QuantifiablePlacementDistributionMode
    {
        get => _quantifiablePlacementDistributionMode;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "GroupedStack" : value.Trim();
            if (string.Equals(_quantifiablePlacementDistributionMode, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _quantifiablePlacementDistributionMode = normalized;
            OnPropertyChanged();
        }
    }

    public bool IsCompositeTarget
    {
        get => _isCompositeTarget;
        set
        {
            if (_isCompositeTarget == value)
            {
                return;
            }

            _isCompositeTarget = value;

            if (_isCompositeTarget)
            {
                if (CompositeRecipeId == Guid.Empty)
                {
                    CompositeRecipeId = Guid.NewGuid();
                }
            }
            else
            {
                CompositeRecipeId = Guid.Empty;
            }

            OnPropertyChanged();
        }
    }

    public bool IsCompositeReversible
    {
        get => _isCompositeReversible;
        set
        {
            if (_isCompositeReversible == value)
            {
                return;
            }

            _isCompositeReversible = value;
            OnPropertyChanged();
        }
    }

    public string CompositePartRequirementMode
    {
        get => _compositePartRequirementMode;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "AllRequired" : value.Trim();
            if (string.Equals(_compositePartRequirementMode, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _compositePartRequirementMode = normalized;
            OnPropertyChanged();
        }
    }

    public int CompositeMinimumRequiredPartCount
    {
        get => _compositeMinimumRequiredPartCount;
        set
        {
            var sanitized = value < 1 ? 1 : value;
            if (_compositeMinimumRequiredPartCount == sanitized)
            {
                return;
            }

            _compositeMinimumRequiredPartCount = sanitized;
            OnPropertyChanged();
        }
    }

    public bool HideEmptyConfiguration { get; set; }

    public List<string> Commands { get; set; } = new();
    public List<string> NameSynonyms { get; set; } = new();
    public List<GamePropertyDefinition> Variables { get; set; } = new();
    public List<string> AdditionalVerbs { get; set; } = new();
    public List<string> AdditionalDirectionals { get; set; } = new();
    public List<DirectionalTraversalMapping> AdditionalDirectionalTraversalMappings { get; set; } = new();
    public List<CommandAction> AvailableActions { get; set; } = new();
    public List<SoundEffectLibraryEntry> SoundEffectLibraryEntries { get; set; } = new();
    public List<Guid> ProcedureIds { get; set; } = new();
    public Guid ObjectId { get; set; } = Guid.NewGuid();
    public Guid CompositeRecipeId { get; set; } = Guid.Empty;
    public List<CompositePartRequirement> CompositeRequiredParts { get; set; } = new();
    public LockOperationRequirements LockOperationRequirements { get; set; } = new();
    public Dictionary<string, string?> InstanceOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ObjectImageVariant> ImageVariants
    {
        get => _imageVariants;
        set
        {
            if (ReferenceEquals(_imageVariants, value))
            {
                return;
            }

            _imageVariants = value ?? new List<ObjectImageVariant>();
            OnPropertyChanged();
        }
    }
    public List<GameObject> ContainedObjects { get; set; } = new();

    public ObjectImageVariant? ResolveDefaultImageVariant()
    {
        var explicitDefault = ImageVariants.FirstOrDefault(static variant => variant.IsDefault);
        if (explicitDefault is not null)
        {
            return explicitDefault;
        }

        if (ImageVariants.Count == 1)
        {
            return ImageVariants[0];
        }

        return ImageVariants.FirstOrDefault();
    }

    public ObjectImageVariant? ResolveImageVariant(string? variantName)
    {
        if (!string.IsNullOrWhiteSpace(variantName))
        {
            var named = ImageVariants.FirstOrDefault(variant => string.Equals(variant.VariantName, variantName, StringComparison.OrdinalIgnoreCase));
            if (named is not null)
            {
                return named;
            }
        }

        return ResolveDefaultImageVariant();
    }

    public string ResolveDefaultImagePath()
    {
        var variantPath = ResolveDefaultImageVariant()?.FullImagePath?.Trim();
        if (!string.IsNullOrWhiteSpace(variantPath))
        {
            return variantPath;
        }

        return string.Empty;
    }

    public string ResolveImagePath(string? variantName)
    {
        var variantPath = ResolveImageVariant(variantName)?.FullImagePath?.Trim();
        if (!string.IsNullOrWhiteSpace(variantPath))
        {
            return variantPath;
        }

        return ResolveDefaultImagePath();
    }

    public double ResolveImageScale(string? variantName)
    {
        var variantScale = ResolveImageVariant(variantName)?.ImageScale ?? 1;
        return variantScale <= 0 ? 1 : variantScale;
    }

    public double ResolveImageLocalAlignmentRotationDegrees(string? variantName)
    {
        var variantRotation = ResolveImageVariant(variantName)?.ImageLocalAlignmentRotationDegrees ?? 0;
        return double.IsFinite(variantRotation) ? variantRotation : 0;
    }

    public double ResolveImageLocalAlignmentOffsetX(string? variantName)
    {
        var variantOffsetX = ResolveImageVariant(variantName)?.ImageLocalAlignmentOffsetX ?? 0;
        return double.IsFinite(variantOffsetX) ? variantOffsetX : 0;
    }

    public double ResolveImageLocalAlignmentOffsetY(string? variantName)
    {
        var variantOffsetY = ResolveImageVariant(variantName)?.ImageLocalAlignmentOffsetY ?? 0;
        return double.IsFinite(variantOffsetY) ? variantOffsetY : 0;
    }

    public override ScopeNodeKind ScopeKind => ScopeNodeKind.GameObject;
    public override string ScopeName => Name ?? string.Empty;
    public override string ScopeNameInGame
    {
        get
        {
            var inGame = NameInGame?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(inGame) ? ScopeName : inGame;
        }
    }
    public override IEnumerable<string> ScopeTokens
    {
        get
        {
            foreach (var synonym in NameSynonyms
                         .Where(static synonym => !string.IsNullOrWhiteSpace(synonym))
                         .Select(static synonym => synonym.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                yield return synonym;
            }

            var name = Name?.Trim() ?? string.Empty;
            var nameInGame = NameInGame?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(nameInGame))
            {
                yield return nameInGame;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                yield return name;
            }
        }
    }

    public override IEnumerable<IScopedAwareNode> ChildScopes => ContainedObjects;

    protected override bool CanAcceptChild(IScopedAwareNode child)
    {
        return child.ScopeKind == ScopeNodeKind.GameObject;
    }

    protected override bool TryAddChildCore(IScopedAwareNode child)
    {
        if (child is not GameObject obj)
        {
            return false;
        }

        if (ContainedObjects.Contains(obj))
        {
            return true;
        }

        ContainedObjects.Add(obj);
        return true;
    }

    protected override bool TryRemoveChildCore(IScopedAwareNode child)
    {
        return child is GameObject obj && ContainedObjects.Remove(obj);
    }

    public void ApplyFeatureVariableContract()
    {
        EnsureVariableState("isMovable", IsMovable, IsMovableDefaultValue, GamePropertyValueRestriction.TrueFalse);
        EnsureVariableState("isContainer", IsContainer, true, GamePropertyValueRestriction.TrueFalse);
        EnsureNumericVariableState("inventoryPoints", IsInventoriable, InventoryPointsDefaultValue, GamePropertyValueRestriction.Numeric);
        EnsureNumericVariableState("containerPoints", IsContainer, ContainerPointsDefaultValue, GamePropertyValueRestriction.Numeric);
        EnsureNumericVariableState("containerPointsRemaining", IsContainer, ContainerPointsDefaultValue, GamePropertyValueRestriction.Numeric);
        EnsureNumericVariableState("capacityPointShareDivider", IsContainer && IsCapacityPointShareDividerEnabled, CapacityPointShareDividerDefaultValue, GamePropertyValueRestriction.Numeric);
        EnsureVariableState("isOpen", IsOpenable, IsOpenDefaultValue, GamePropertyValueRestriction.TrueFalse);
        EnsureVariableState("isLocked", IsLockable, IsLockedDefaultValue, GamePropertyValueRestriction.TrueFalse);
        EnsureVariableState("isActive", IsActivatable, IsActiveDefaultValue, GamePropertyValueRestriction.TrueFalse);
        EnsureVariableState("isHidden", IsHidable, IsHiddenDefaultValue, GamePropertyValueRestriction.TrueFalse);
        EnsureVariableState("isQuantifiable", IsQuantifiable, true, GamePropertyValueRestriction.TrueFalse);
        EnsureNumericVariableState("quantity", IsQuantifiable, Quantity, GamePropertyValueRestriction.Numeric);
    }

    public void InitializeFeatureFlagsFromKnownVariables()
    {
        if (TryGetKnownBooleanVariable("isMovable", out var isMovableDefault))
        {
            _isMovable = true;
            _isMovableDefaultValue = isMovableDefault;
        }

        if (TryGetKnownNumericVariable("inventoryPoints", out var inventoryPointsDefault))
        {
            _isInventoriable = true;
            _inventoryPointsDefaultValue = inventoryPointsDefault;

            if (!_isMovable)
            {
                _isMovable = true;
                _isMovableDefaultValue = true;
            }
        }

        if (TryGetKnownNumericVariable("containerPoints", out var containerPointsDefault))
        {
            _isContainer = true;
            _containerPointsDefaultValue = containerPointsDefault;
        }

        if (TryGetKnownNumericVariable("capacityPointShareDivider", out var capacityPointShareDividerDefault))
        {
            _isContainer = true;
            _isCapacityPointShareDividerEnabled = true;
            _capacityPointShareDividerDefaultValue = capacityPointShareDividerDefault;
        }

        if (TryGetKnownBooleanVariable("isOpen", out var isOpenDefault))
        {
            _isOpenable = true;
            _isOpenDefaultValue = isOpenDefault;
        }

        if (TryGetKnownBooleanVariable("isLocked", out var isLockedDefault))
        {
            _isLockable = true;
            _isLockedDefaultValue = isLockedDefault;
        }

        if (TryGetKnownBooleanVariable("isActive", out var isActiveDefault))
        {
            _isActivatable = true;
            _isActiveDefaultValue = isActiveDefault;
        }

        if (TryGetKnownBooleanVariable("isHidden", out var isHiddenDefault))
        {
            _isHidable = true;
            _isHiddenDefaultValue = isHiddenDefault;
        }

        if (TryGetKnownBooleanVariable("isQuantifiable", out var isQuantifiableDefault))
        {
            _isQuantifiable = isQuantifiableDefault;
        }

        if (TryGetKnownNumericVariable("quantity", out var quantityDefault))
        {
            _quantity = quantityDefault;
            _isQuantifiable = true;
        }
    }

    private bool TryGetKnownNumericVariable(string variableName, out int defaultValue)
    {
        defaultValue = 1;

        var existing = Variables.FirstOrDefault(v => string.Equals(v.Name, variableName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return false;
        }

        if (!int.TryParse(existing.DefaultValue?.Trim(), out var parsedValue))
        {
            defaultValue = 1;
            return true;
        }

        defaultValue = parsedValue < 1 ? 1 : parsedValue;
        return true;
    }

    private bool TryGetKnownBooleanVariable(string variableName, out bool defaultValue)
    {
        defaultValue = false;

        var existing = Variables.FirstOrDefault(v => string.Equals(v.Name, variableName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return false;
        }

        defaultValue = string.Equals(existing.DefaultValue?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static string NormalizeCardinalDirection(string? direction)
    {
        return direction?.Trim().ToUpperInvariant() switch
        {
            "N" => "N",
            "E" => "E",
            "S" => "S",
            "W" => "W",
            _ => "N"
        };
    }

    private static string NormalizeHeadingDirection(string? direction)
    {
        return direction?.Trim().ToUpperInvariant() switch
        {
            "N" => "N",
            "NE" => "NE",
            "E" => "E",
            "SE" => "SE",
            "S" => "S",
            "SW" => "SW",
            "W" => "W",
            "NW" => "NW",
            _ => "N"
        };
    }

    private void EnsureVariableState(string name, bool isEnabled, bool defaultValue, GamePropertyValueRestriction valueRestriction)
    {
        var existing = Variables.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

        if (!isEnabled)
        {
            if (existing is not null)
            {
                Variables.Remove(existing);
            }

            return;
        }

        if (existing is not null)
        {
            existing.DefaultValue = defaultValue ? "true" : "false";
            existing.ValueRestriction = valueRestriction;
            return;
        }

        Variables.Add(new GamePropertyDefinition
        {
            Name = name,
            DefaultValue = defaultValue ? "true" : "false",
            ValueRestriction = valueRestriction
        });
    }

    private void EnsureNumericVariableState(string name, bool isEnabled, int defaultValue, GamePropertyValueRestriction valueRestriction)
    {
        var existing = Variables.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

        if (!isEnabled)
        {
            if (existing is not null)
            {
                Variables.Remove(existing);
            }

            return;
        }

        var sanitizedDefaultValue = defaultValue < 1 ? 1 : defaultValue;

        if (existing is not null)
        {
            existing.DefaultValue = sanitizedDefaultValue.ToString();
            existing.ValueRestriction = valueRestriction;
            return;
        }

        Variables.Add(new GamePropertyDefinition
        {
            Name = name,
            DefaultValue = sanitizedDefaultValue.ToString(),
            ValueRestriction = valueRestriction
        });
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
