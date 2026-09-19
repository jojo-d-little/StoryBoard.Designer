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
    IReadOnlyList<GamePropertyChoiceItem>? ImageVariantChooserVariableChoices = null,
    PropertyResolutionScope ImageVariantChooserVariableScope = PropertyResolutionScope.Object,
    bool IsMovable = false,
    bool IsMovableDefaultValue = true,
    RuntimeObjectSpatialTypes SpatialType = RuntimeObjectSpatialTypes.SolidObject,
    int StackGroup = 0,
    int FootprintWidthCells = 1,
    int FootprintHeightCells = 1,
    string FootprintOrientation = "N",
    string HeadingDirection = "N",
    int ObjectHeightUnits = 1,
    int HeightInRoom = 0,
    double? StackScaleStepOverride = null,
    double? MinStackScaleOverride = null,
    int ProjectRoomGridCellSize = 40,
    int ProjectRoomCanvasWidth = 800,
    int ProjectRoomCanvasHeight = 600);
