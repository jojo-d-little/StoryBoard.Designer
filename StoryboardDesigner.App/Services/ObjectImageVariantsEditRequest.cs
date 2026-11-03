using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Services;

public sealed record ObjectImageVariantsEditRequest(
    IReadOnlyList<ObjectImageVariant> ImageVariants,
    string ImageVariantChooserScript,
    double ImageRotationDegrees,
    string ProjectFilePath,
    string PreferredImageSourceBucket,
    IReadOnlyList<string>? ImageVariantChooserReferenceTokens = null,
    GamePropertyPickerContext? ImageVariantChooserPickerContext = null,
    bool IsMovable = false,
    bool IsMovableDefaultValue = true,
    RuntimeObjectSpatialTypes SpatialType = RuntimeObjectSpatialTypes.SolidObject,
    int StackGroup = 0,
    int FootprintWidthCells = 1,
    int FootprintHeightCells = 1,
    string FootprintOrientation = "N",
    string FootprintCornerStyle = "rounded",
    string HeadingDirection = "N",
    int ObjectHeightUnits = 1,
    int HeightInRoom = 0,
    double? StackScaleStepOverride = null,
    double? MinStackScaleOverride = null,
    int ProjectRoomGridCellSize = 40,
    int ProjectRoomCanvasWidth = 800,
    int ProjectRoomCanvasHeight = 600,
    PointLight? PointLight = null,
    LightOcclusion? LightOcclusion = null,
    bool IsPointLightEnabledDefaultValue = true,
    PointLightDefaults? PointLightDefaults = null,
    ParticleEffectAttachment? ParticleEffect = null,
    GamePropertyPickerContext? ParticleEffectVariablePickerContext = null,
    Func<string, string, GamePropertyDefinition?>? ParticleEffectNewComputedVariableFactory = null,
    IReadOnlyList<GamePropertyDefinition>? ParticleEffectNewVariables = null,
    IReadOnlyList<StructuralPartPlacementEditItem>? StructuralPartPlacements = null,
    string StructuralPartRenderOrderParentName = "Parent",
    bool StructuralPartRenderOrderIsReadOnly = false,
    double ImageTransparency = 0);
