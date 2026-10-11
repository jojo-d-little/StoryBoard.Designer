using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;
using StoryboardDesigner.App.Views;
using Storyboard.Shared.GameServices.Spatial;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StoryboardDesigner.App.Tests;

public sealed class RoomDesignerPreviewObjectViewModelRotationOffsetTests
{
    [Fact]
    public void LinkedPreview_UsesLiveBaseAppearanceAndKeepsPlacementEditsLocal()
    {
        var baseObject = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            FootprintWidthCells = 3,
            FootprintHeightCells = 2,
            IncludeInPreview = true
        };
        var instance = new GameObject
        {
            LinkedBaseObjectId = baseObject.ObjectId,
            FootprintWidthCells = 1,
            FootprintHeightCells = 1,
            IncludeInPreview = false,
            PositionX = 10,
            ImageRotationDegrees = 0
        };
        var viewModel = new RoomDesignerPreviewObjectViewModel(instance, definitionResolver: id => id == baseObject.ObjectId ? baseObject : null);
        viewModel.UpdateGridCellSize(10);

        Assert.Equal(30d, viewModel.FootprintWidthPixels);
        Assert.True(viewModel.IncludeInPreview);

        baseObject.FootprintWidthCells = 4;
        baseObject.IncludeInPreview = false;

        Assert.Equal(40d, viewModel.FootprintWidthPixels);
        Assert.False(viewModel.IncludeInPreview);
        viewModel.PositionX = 22;
        viewModel.RoomRotationDegrees = 180;
        Assert.Equal(22d, instance.PositionX);
        Assert.Equal(180d, instance.ImageRotationDegrees);
        Assert.Equal(0d, baseObject.PositionX);
        Assert.Equal(0d, baseObject.ImageRotationDegrees);
    }

    [Theory]
    [InlineData(0d, 7d, -3d, 7d, -3d)]
    [InlineData(180d, -67d, -52d, 593.48d, 388.32d)]
    public void AlignmentEditorPlacement_NormalizesFullBitmapBoundsThenAppliesOffsets(
        double localRotationDegrees,
        double offsetX,
        double offsetY,
        double expectedX,
        double expectedY)
    {
        var placement = LargeObjectVisualGuideDialog.ComputeImagePlacement(
            localRotationDegrees,
            offsetX,
            offsetY,
            footprintWidthPixels: 520d,
            footprintHeightPixels: 320d,
            renderedBitmapWidthPixels: 1536d * 0.43d,
            renderedBitmapHeightPixels: 1024d * 0.43d);

        Assert.Equal(expectedX, placement.X, 6);
        Assert.Equal(expectedY, placement.Y, 6);
    }

    [Fact]
    public void PlacementComparisonMode_DefaultsToSharedOnly()
    {
        var viewModel = CreateViewModel(imageRotationDegrees: 90d, localRotationDegrees: 45d, rawOffsetX: 0d, rawOffsetY: 39d);

        Assert.Equal(RoomDesignerPreviewPlacementComparisonMode.SharedOnly, viewModel.PlacementComparisonMode);
    }

    [Fact]
    public void PlacementComparisonDelta_IsZeroInSharedOnlyModel()
    {
        var viewModel = CreateViewModel(imageRotationDegrees: 90d, localRotationDegrees: 45d, rawOffsetX: 0d, rawOffsetY: 39d);

        Assert.Equal(0d, viewModel.PlacementComparisonDeltaX, 6);
        Assert.Equal(0d, viewModel.PlacementComparisonDeltaY, 6);
    }

    [Theory]
    [InlineData(0d, 0d, 7d, 0d, 7d)]
    [InlineData(0d, 7d, 0d, 7d, 0d)]
    [InlineData(90d, 0d, 7d, 33d, 0d)]
    [InlineData(90d, 7d, 0d, 40d, 7d)]
    [InlineData(180d, 0d, 7d, 40d, 33d)]
    [InlineData(180d, 7d, 0d, 33d, 40d)]
    [InlineData(270d, 0d, 7d, 7d, 40d)]
    [InlineData(270d, 7d, 0d, 0d, 33d)]
    public void ImageLocalAlignmentOffsets_RoomRotationCarriesOffsetsWithObject(
        double imageRotationDegrees,
        double rawOffsetX,
        double rawOffsetY,
        double expectedOffsetX,
        double expectedOffsetY)
    {
        var viewModel = CreateViewModel(imageRotationDegrees, localRotationDegrees: 0d, rawOffsetX, rawOffsetY);

        Assert.Equal(expectedOffsetX, viewModel.ImageLocalAlignmentOffsetX, 6);
        Assert.Equal(expectedOffsetY, viewModel.ImageLocalAlignmentOffsetY, 6);
    }

    [Fact]
    public void ImageLocalAlignmentOffsets_SupportNonCardinalRotation()
    {
        var viewModel = CreateViewModel(45d, localRotationDegrees: 0d, 0d, 7d);

        Assert.Equal(23.334524, viewModel.ImageLocalAlignmentOffsetX, 6);
        Assert.Equal(4.949747, viewModel.ImageLocalAlignmentOffsetY, 6);
    }

    [Fact]
    public void ImageLocalAlignmentOffsets_LocalHalfTurn_DoesNotTranslateFootprint()
    {
        var viewModel = CreateViewModel(
            imageRotationDegrees: 0,
            localRotationDegrees: 180,
            rawOffsetX: 0,
            rawOffsetY: 0,
            footprintWidthCells: 10,
            footprintHeightCells: 10);

        // No thumbnail: the preview uses a 40 x 40 fallback image in a 400 x 400 footprint.
        Assert.Equal(40d, viewModel.ImageLocalAlignmentOffsetX, 6);
        Assert.Equal(40d, viewModel.ImageLocalAlignmentOffsetY, 6);
    }

    [Fact]
    public void ImageLocalAlignmentOffsets_ElevatorImage_UsesNativeSize()
    {
        var imagePath = Path.Combine(Path.GetTempPath(), "storyboard-elevator-preview-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            var bitmap = BitmapSource.Create(400, 400, 96, 96, PixelFormats.Bgra32, null, new byte[400 * 400 * 4], 400 * 4);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(imagePath)) encoder.Save(stream);

            var gameObject = new GameObject
            {
                PositionX = 600,
                PositionY = 80,
                FootprintWidthCells = 10,
                FootprintHeightCells = 10,
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        IsDefault = true,
                        FullImagePath = imagePath,
                        ImageLocalAlignmentRotationDegrees = 180,
                        ImageScale = 1
                    }
                ]
            };
            var viewModel = new RoomDesignerPreviewObjectViewModel(gameObject);
            viewModel.SelectedPreviewVariantName = "default";

            Assert.Equal(400d, viewModel.PreviewRenderedIconWidthPixels, 6);
            Assert.Equal(400d, viewModel.PreviewRenderedIconHeightPixels, 6);
            Assert.Equal(400d, viewModel.ImageLocalAlignmentOffsetX, 6);
            Assert.Equal(400d, viewModel.ImageLocalAlignmentOffsetY, 6);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void ImageLocalAlignmentOffsets_NormalizeRotatedBoundsBeforeApplyingLocalOffset()
    {
        var viewModel = CreateViewModel(
            imageRotationDegrees: 0d,
            localRotationDegrees: 45d,
            rawOffsetX: 0d,
            rawOffsetY: 39d);

        // The bitmap origin moves by the inverse of the rotated AABB's negative top-left.
        // The authored offset remains (0, 39) in footprint-local axes.
        Assert.Equal(28.284271, viewModel.ImageLocalAlignmentOffsetX, 6);
        Assert.Equal(39d, viewModel.ImageLocalAlignmentOffsetY, 6);
    }

    [Fact]
    public void ImageLocalAlignmentOffsets_RectangularRug_UseFullScaledBitmapBounds()
    {
        var imagePath = Path.Combine(Path.GetTempPath(), "storyboard-rug-preview-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            var bitmap = BitmapSource.Create(1536, 1024, 96, 96, PixelFormats.Bgra32, null, new byte[1536 * 1024 * 4], 1536 * 4);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(imagePath)) encoder.Save(stream);

            var gameObject = new GameObject
            {
                ImageRotationDegrees = 0,
                FootprintWidthCells = 13,
                FootprintHeightCells = 8,
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        IsDefault = true,
                        FullImagePath = imagePath,
                        ImageLocalAlignmentRotationDegrees = -180,
                        ImageLocalAlignmentOffsetX = -67,
                        ImageLocalAlignmentOffsetY = -52,
                        ImageScale = 0.43
                    }
                ]
            };
            var viewModel = new RoomDesignerPreviewObjectViewModel(gameObject);
            viewModel.SelectedPreviewVariantName = "default";

            Assert.Equal(1536 * 0.43, viewModel.PreviewRenderedIconWidthPixels, 6);
            Assert.Equal(1024 * 0.43, viewModel.PreviewRenderedIconHeightPixels, 6);
            Assert.Equal(593.48, viewModel.ImageLocalAlignmentOffsetX, 6);
            Assert.Equal(388.32, viewModel.ImageLocalAlignmentOffsetY, 6);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Theory]
    [InlineData(0d, 0d, 0d, 7d, 1, 1, null)]
    [InlineData(90d, 0d, 0d, 7d, 1, 1, null)]
    [InlineData(180d, 0d, 0d, 7d, 1, 1, null)]
    [InlineData(270d, 0d, 0d, 7d, 1, 1, null)]
    [InlineData(45d, 0d, 0d, 7d, 1, 1, null)]
    [InlineData(0d, 45d, 0d, 39d, 1, 3, null)]
    [InlineData(90d, 45d, 0d, 39d, 1, 3, null)]
    [InlineData(90d, 0d, 0d, 7d, 1, 2, "E")]
    public void ImageLocalAlignmentOffsets_MatchSharedPlacementMath(
        double roomRotationDegrees,
        double localRotationDegrees,
        double rawOffsetX,
        double rawOffsetY,
        int footprintWidthCells,
        int footprintHeightCells,
        string? footprintOrientation)
    {
        var viewModel = CreateViewModel(
            roomRotationDegrees,
            localRotationDegrees,
            rawOffsetX,
            rawOffsetY,
            imageScale: 1d,
            footprintWidthCells,
            footprintHeightCells,
            footprintOrientation);

        var expected = RuntimeRenderablePlacementMath.ComputeIconLocalOffset(
            roomRotationDegrees,
            localRotationDegrees,
            rawOffsetX,
            rawOffsetY,
            viewModel.UnrotatedFootprintWidthPixels,
            viewModel.UnrotatedFootprintHeightPixels,
            viewModel.PreviewRenderedIconWidthPixels,
            viewModel.PreviewRenderedIconHeightPixels);

        Assert.Equal(expected.X, viewModel.ImageLocalAlignmentOffsetX, 6);
        Assert.Equal(expected.Y, viewModel.ImageLocalAlignmentOffsetY, 6);
    }

    [Fact]
    public void LinkedObjectPreview_UsesDefinitionOwnedImageFields_WhenLocalVariantsAreOmitted()
    {
        var baseObjectId = Guid.NewGuid();
        var linkedObject = new GameObject
        {
            LinkedBaseObjectId = baseObjectId,
            ImageVariants = []
        };

        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            ImageVariants =
            [
                new ObjectImageVariant
                {
                    VariantName = "Closed",
                    IsDefault = true,
                    ImageLocalAlignmentOffsetX = 7d,
                    ImageLocalAlignmentOffsetY = 0d,
                    ImageScale = 2d
                }
            ]
        };

        var viewModel = new RoomDesignerPreviewObjectViewModel(
            linkedObject,
            definitionResolver: id => id == baseObjectId ? baseObject : null);

        viewModel.SelectedPreviewVariantName = "Closed";

        Assert.Contains("Closed", viewModel.AvailablePreviewVariantNames);
        Assert.Equal(2d, viewModel.ImageScale, 6);
        Assert.Equal(7d, viewModel.ImageLocalAlignmentOffsetX, 6);
        Assert.Equal(0d, viewModel.ImageLocalAlignmentOffsetY, 6);
    }

    [Fact]
    public void LinkedObjectPreview_BaseVariantReplacement_RaisesImageDerivedPropertyChanges()
    {
        var baseObjectId = Guid.NewGuid();
        var linkedObject = new GameObject
        {
            LinkedBaseObjectId = baseObjectId,
            ImageVariants = []
        };

        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            ImageVariants =
            [
                new ObjectImageVariant
                {
                    VariantName = "Closed",
                    IsDefault = true,
                    ImageScale = 1d
                }
            ]
        };

        var viewModel = new RoomDesignerPreviewObjectViewModel(
            linkedObject,
            definitionResolver: id => id == baseObjectId ? baseObject : null);

        viewModel.SelectedPreviewVariantName = "Closed";
        Assert.Equal(1d, viewModel.ImageScale, 6);

        var changed = new HashSet<string>(StringComparer.Ordinal);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.PropertyName))
            {
                changed.Add(args.PropertyName);
            }
        };

        baseObject.ImageVariants =
        [
            new ObjectImageVariant
            {
                VariantName = "Open",
                IsDefault = true,
                ImageScale = 2d
            }
        ];

        Assert.Contains(nameof(RoomDesignerPreviewObjectViewModel.AvailablePreviewVariantNames), changed);
        Assert.Contains(nameof(RoomDesignerPreviewObjectViewModel.ImageScale), changed);
        Assert.Equal(2d, viewModel.ImageScale, 6);
        Assert.Null(viewModel.SelectedPreviewVariantName);
    }

    private static RoomDesignerPreviewObjectViewModel CreateViewModel(
        double imageRotationDegrees,
        double localRotationDegrees,
        double rawOffsetX,
        double rawOffsetY,
        double imageScale = 1d,
        int footprintWidthCells = 1,
        int footprintHeightCells = 1,
        string? footprintOrientation = null)
    {
        var gameObject = new GameObject
        {
            ImageRotationDegrees = imageRotationDegrees,
            FootprintWidthCells = footprintWidthCells,
            FootprintHeightCells = footprintHeightCells,
            FootprintOrientation = footprintOrientation ?? string.Empty,
            ImageVariants =
            [
                new ObjectImageVariant
                {
                    VariantName = "default",
                    IsDefault = true,
                    ImageLocalAlignmentRotationDegrees = localRotationDegrees,
                    ImageLocalAlignmentOffsetX = rawOffsetX,
                    ImageLocalAlignmentOffsetY = rawOffsetY,
                    ImageScale = imageScale
                }
            ]
        };

        var viewModel = new RoomDesignerPreviewObjectViewModel(gameObject);
        viewModel.SelectedPreviewVariantName = "default";
        return viewModel;
    }
}
