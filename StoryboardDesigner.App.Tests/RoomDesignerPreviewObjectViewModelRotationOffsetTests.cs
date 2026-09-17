using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;
using Storyboard.Shared.GameServices.Spatial;
using System.ComponentModel;

namespace StoryboardDesigner.App.Tests;

public sealed class RoomDesignerPreviewObjectViewModelRotationOffsetTests
{
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
    [InlineData(90d, 0d, 7d, 33d, 0d)]
    [InlineData(180d, 0d, 7d, 40d, 33d)]
    [InlineData(270d, 0d, 7d, 7d, 40d)]
    public void ImageLocalAlignmentOffsets_RotateWithImageRotation(
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

        Assert.Equal(-4.949747, viewModel.ImageLocalAlignmentOffsetX, 6);
        Assert.Equal(4.949747, viewModel.ImageLocalAlignmentOffsetY, 6);
    }

    [Fact]
    public void ImageLocalAlignmentOffsets_ApplyCenterPivotCompensation_ForLargeLocalRotationAndOffset()
    {
        // Crowbar-style case: large local offset + non-zero local rotation.
        var roomRotationDegrees = 0d;
        var localRotationDegrees = 45d;
        var rawOffsetX = 0d;
        var rawOffsetY = 39d;
        var viewModel = CreateViewModel(roomRotationDegrees, localRotationDegrees, rawOffsetX, rawOffsetY, imageScale: 1d);

        // Base fallback image size is 40x40 when no thumbnail exists.
        var center = (X: 20d, Y: 20d);
        var localRadians = localRotationDegrees * (Math.PI / 180d);
        var localCos = Math.Cos(localRadians);
        var localSin = Math.Sin(localRadians);
        var rotatedCenterX = (center.X * localCos) - (center.Y * localSin);
        var rotatedCenterY = (center.X * localSin) + (center.Y * localCos);
        var compensationX = center.X - rotatedCenterX;
        var compensationY = center.Y - rotatedCenterY;

        var finalRadians = (roomRotationDegrees + localRotationDegrees) * (Math.PI / 180d);
        var finalCos = Math.Cos(finalRadians);
        var finalSin = Math.Sin(finalRadians);
        var rotatedOffsetX = (rawOffsetX * finalCos) - (rawOffsetY * finalSin);
        var rotatedOffsetY = (rawOffsetX * finalSin) + (rawOffsetY * finalCos);

        var expectedX = rotatedOffsetX + compensationX;
        var expectedY = rotatedOffsetY + compensationY;

        Assert.Equal(expectedX, viewModel.ImageLocalAlignmentOffsetX, 6);
        Assert.Equal(expectedY, viewModel.ImageLocalAlignmentOffsetY, 6);
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
            viewModel.FootprintWidthPixels,
            viewModel.FootprintHeightPixels,
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
