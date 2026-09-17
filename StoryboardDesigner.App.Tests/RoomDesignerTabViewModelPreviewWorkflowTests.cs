using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class RoomDesignerTabViewModelPreviewWorkflowTests
{
    [Fact]
    public void DirectionSlots_AlwaysExposeTenDirectionalEntriesIncludingUpAndDown()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(10, viewModel.DirectionSlots.Count());
        Assert.Contains(viewModel.DirectionSlots, slot => slot.Entry.Slot == RoomImageSlot.Up);
        Assert.Contains(viewModel.DirectionSlots, slot => slot.Entry.Slot == RoomImageSlot.Down);
    }

    [Fact]
    public void OverlayPreviewDirectionalAnchor_FollowsSelectedDirectionSlot()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedDirectionSlot = viewModel.NorthSlot;
        Assert.Equal(System.Windows.HorizontalAlignment.Center, viewModel.OverlayPreviewDirectionalHorizontalAlignment);
        Assert.Equal(System.Windows.VerticalAlignment.Top, viewModel.OverlayPreviewDirectionalVerticalAlignment);

        viewModel.SelectedDirectionSlot = viewModel.SouthSlot;
        Assert.Equal(System.Windows.HorizontalAlignment.Center, viewModel.OverlayPreviewDirectionalHorizontalAlignment);
        Assert.Equal(System.Windows.VerticalAlignment.Bottom, viewModel.OverlayPreviewDirectionalVerticalAlignment);

        viewModel.SelectedDirectionSlot = viewModel.EastSlot;
        Assert.Equal(System.Windows.HorizontalAlignment.Right, viewModel.OverlayPreviewDirectionalHorizontalAlignment);
        Assert.Equal(System.Windows.VerticalAlignment.Center, viewModel.OverlayPreviewDirectionalVerticalAlignment);

        viewModel.SelectedDirectionSlot = viewModel.WestSlot;
        Assert.Equal(System.Windows.HorizontalAlignment.Left, viewModel.OverlayPreviewDirectionalHorizontalAlignment);
        Assert.Equal(System.Windows.VerticalAlignment.Center, viewModel.OverlayPreviewDirectionalVerticalAlignment);

        viewModel.SelectedDirectionSlot = viewModel.NorthEastSlot;
        Assert.Equal(System.Windows.HorizontalAlignment.Right, viewModel.OverlayPreviewDirectionalHorizontalAlignment);
        Assert.Equal(System.Windows.VerticalAlignment.Top, viewModel.OverlayPreviewDirectionalVerticalAlignment);

        viewModel.SelectedDirectionSlot = viewModel.NorthWestSlot;
        Assert.Equal(System.Windows.HorizontalAlignment.Left, viewModel.OverlayPreviewDirectionalHorizontalAlignment);
        Assert.Equal(System.Windows.VerticalAlignment.Top, viewModel.OverlayPreviewDirectionalVerticalAlignment);

        viewModel.SelectedDirectionSlot = viewModel.SouthEastSlot;
        Assert.Equal(System.Windows.HorizontalAlignment.Right, viewModel.OverlayPreviewDirectionalHorizontalAlignment);
        Assert.Equal(System.Windows.VerticalAlignment.Bottom, viewModel.OverlayPreviewDirectionalVerticalAlignment);

        viewModel.SelectedDirectionSlot = viewModel.SouthWestSlot;
        Assert.Equal(System.Windows.HorizontalAlignment.Left, viewModel.OverlayPreviewDirectionalHorizontalAlignment);
        Assert.Equal(System.Windows.VerticalAlignment.Bottom, viewModel.OverlayPreviewDirectionalVerticalAlignment);

        viewModel.SelectedDirectionSlot = viewModel.UpSlot;
        Assert.Equal(System.Windows.HorizontalAlignment.Center, viewModel.OverlayPreviewDirectionalHorizontalAlignment);
        Assert.Equal(System.Windows.VerticalAlignment.Top, viewModel.OverlayPreviewDirectionalVerticalAlignment);

        viewModel.SelectedDirectionSlot = viewModel.DownSlot;
        Assert.Equal(System.Windows.HorizontalAlignment.Center, viewModel.OverlayPreviewDirectionalHorizontalAlignment);
        Assert.Equal(System.Windows.VerticalAlignment.Bottom, viewModel.OverlayPreviewDirectionalVerticalAlignment);
    }

    [Fact]
    public void OverlayPreviewDirectionalRenderTransformOrigin_FollowsSelectedDirectionSlotAnchor()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedDirectionSlot = viewModel.NorthSlot;
        Assert.Equal(new System.Windows.Point(0.5, 0), viewModel.OverlayPreviewDirectionalRenderTransformOrigin);

        viewModel.SelectedDirectionSlot = viewModel.EastSlot;
        Assert.Equal(new System.Windows.Point(1, 0.5), viewModel.OverlayPreviewDirectionalRenderTransformOrigin);

        viewModel.SelectedDirectionSlot = viewModel.SouthSlot;
        Assert.Equal(new System.Windows.Point(0.5, 1), viewModel.OverlayPreviewDirectionalRenderTransformOrigin);

        viewModel.SelectedDirectionSlot = viewModel.WestSlot;
        Assert.Equal(new System.Windows.Point(0.5, 0.5), viewModel.OverlayPreviewDirectionalRenderTransformOrigin);

        viewModel.SelectedDirectionSlot = viewModel.NorthWestSlot;
        Assert.Equal(new System.Windows.Point(0, 0), viewModel.OverlayPreviewDirectionalRenderTransformOrigin);

        viewModel.SelectedDirectionSlot = viewModel.SouthEastSlot;
        Assert.Equal(new System.Windows.Point(1, 1), viewModel.OverlayPreviewDirectionalRenderTransformOrigin);

        viewModel.SelectedDirectionSlot = viewModel.UpSlot;
        Assert.Equal(new System.Windows.Point(0.5, 0.5), viewModel.OverlayPreviewDirectionalRenderTransformOrigin);

        viewModel.SelectedDirectionSlot = viewModel.DownSlot;
        Assert.Equal(new System.Windows.Point(0.5, 0.5), viewModel.OverlayPreviewDirectionalRenderTransformOrigin);
    }

    [Fact]
    public void OverlayDirectionalPlacementMetadata_IsInvariantAcrossLandscapeAndPortrait()
    {
        var landscape = CreateViewModel(800, 600);
        var portrait = CreateViewModel(600, 800);

        foreach (var slot in Enum.GetValues<RoomImageSlot>())
        {
            var landscapeSlot = landscape.RoomImageSlots.Single(item => item.Entry.Slot == slot);
            var portraitSlot = portrait.RoomImageSlots.Single(item => item.Entry.Slot == slot);

            Assert.Equal(landscapeSlot.OverlayHorizontalAlignment, portraitSlot.OverlayHorizontalAlignment);
            Assert.Equal(landscapeSlot.OverlayVerticalAlignment, portraitSlot.OverlayVerticalAlignment);
            Assert.Equal(landscapeSlot.OverlayRenderTransformOrigin, portraitSlot.OverlayRenderTransformOrigin);
        }
    }

    [Fact]
    public void OverlayDirectionalRotation_IsNotAutoChanged_WhenCanvasShapeChanges()
    {
        var viewModel = CreateViewModel(800, 600);

        viewModel.NorthSlot!.OverlayRotationDegrees = 90;
        viewModel.DownSlot!.OverlayRotationDegrees = -90;
        viewModel.NorthSlot.OverlayOffsetX = 12;
        viewModel.DownSlot.OverlayOffsetY = -8;

        viewModel.DesignerCanvasWidth = 600;
        viewModel.DesignerCanvasHeight = 800;

        Assert.Equal(90, viewModel.NorthSlot.OverlayRotationDegrees);
        Assert.Equal(-90, viewModel.DownSlot.OverlayRotationDegrees);
        Assert.Equal(12, viewModel.NorthSlot.OverlayOffsetX);
        Assert.Equal(-8, viewModel.DownSlot.OverlayOffsetY);
    }

    [Fact]
    public void RoomDisplayMode_WhenChanged_UpdatesModeFlags()
    {
        var viewModel = CreateViewModel();

        viewModel.RoomDisplayMode = RuntimeRoomImageDisplayMode.Overlay;

        Assert.True(viewModel.IsOverlayMode);
        Assert.False(viewModel.IsIndependentMode);
    }

    [Fact]
    public void SelectedDirectionHiddenIndicatorText_ReflectsPreviewVisibilityToggle()
    {
        var viewModel = CreateViewModel();
        var selected = viewModel.SelectedDirectionSlot;

        Assert.NotNull(selected);
        Assert.Equal(string.Empty, viewModel.SelectedDirectionHiddenIndicatorText);

        selected!.IsPreviewVisible = false;

        Assert.Equal("Hidden in Preview", viewModel.SelectedDirectionHiddenIndicatorText);

        selected.IsPreviewVisible = true;

        Assert.Equal(string.Empty, viewModel.SelectedDirectionHiddenIndicatorText);
    }

    [Fact]
    public void OverlayPreviewSlots_ShowsAllVisibleDirectionalOverlays_NotOnlySelectedDirection()
    {
        var viewModel = CreateViewModel();

        viewModel.RoomDisplayMode = RuntimeRoomImageDisplayMode.Overlay;
        viewModel.NorthSlot!.FullImagePath = "north.png";
        viewModel.EastSlot!.FullImagePath = "east.png";
        viewModel.UpSlot!.FullImagePath = "up.png";

        viewModel.SelectedDirectionSlot = viewModel.NorthSlot;

        var overlaySlots = viewModel.OverlayPreviewSlots.Select(slot => slot.Entry.Slot).ToArray();

        Assert.Contains(RoomImageSlot.North, overlaySlots);
        Assert.Contains(RoomImageSlot.East, overlaySlots);
        Assert.Contains(RoomImageSlot.Up, overlaySlots);
        Assert.Equal(3, overlaySlots.Length);
    }

    [Fact]
    public void AssigningDirectionalImage_AddsSlotToOverlayPreview()
    {
        var viewModel = CreateViewModel();
        viewModel.RoomDisplayMode = RuntimeRoomImageDisplayMode.Overlay;

        var down = viewModel.DownSlot;
        Assert.NotNull(down);
        Assert.False(down!.HasConfiguredImage);

        down.FullImagePath = "down.png";

        Assert.True(down.HasConfiguredImage);
        Assert.Contains(viewModel.OverlayPreviewSlots, slot => slot.Entry.Slot == RoomImageSlot.Down);
    }

    [Fact]
    public void OverlayPreviewSlots_UseExpectedDefaultRenderOrder()
    {
        var viewModel = CreateViewModel();
        viewModel.RoomDisplayMode = RuntimeRoomImageDisplayMode.Overlay;

        viewModel.DownSlot!.FullImagePath = "down.png";
        viewModel.NorthSlot!.FullImagePath = "north.png";
        viewModel.EastSlot!.FullImagePath = "east.png";
        viewModel.SouthSlot!.FullImagePath = "south.png";
        viewModel.WestSlot!.FullImagePath = "west.png";
        viewModel.NorthEastSlot!.FullImagePath = "ne.png";
        viewModel.NorthWestSlot!.FullImagePath = "nw.png";
        viewModel.SouthEastSlot!.FullImagePath = "se.png";
        viewModel.SouthWestSlot!.FullImagePath = "sw.png";

        var ordered = viewModel.OverlayPreviewSlots.Select(slot => slot.Entry.Slot).ToList();

        Assert.Equal(RoomImageSlot.Down, ordered[0]);
        Assert.True(ordered.IndexOf(RoomImageSlot.NorthEast) > ordered.IndexOf(RoomImageSlot.West));
        Assert.True(ordered.IndexOf(RoomImageSlot.NorthWest) > ordered.IndexOf(RoomImageSlot.West));
        Assert.True(ordered.IndexOf(RoomImageSlot.SouthEast) > ordered.IndexOf(RoomImageSlot.West));
        Assert.True(ordered.IndexOf(RoomImageSlot.SouthWest) > ordered.IndexOf(RoomImageSlot.West));
    }

    [Fact]
    public void OverlayPreviewSlots_RespectExplicitRenderOrderOverride()
    {
        var viewModel = CreateViewModel();
        viewModel.RoomDisplayMode = RuntimeRoomImageDisplayMode.Overlay;

        viewModel.DownSlot!.FullImagePath = "down.png";
        viewModel.WestSlot!.FullImagePath = "west.png";

        viewModel.DownSlot.OverlayRenderOrder = 500;
        viewModel.WestSlot.OverlayRenderOrder = 10;

        var ordered = viewModel.OverlayPreviewSlots.Select(slot => slot.Entry.Slot).ToList();

        Assert.True(ordered.IndexOf(RoomImageSlot.West) < ordered.IndexOf(RoomImageSlot.Down));
    }

    [Fact]
    public void DownDirectionalImage_WithExistingFile_IsRenderableInOverlayPreview()
    {
        var viewModel = CreateViewModel();
        viewModel.RoomDisplayMode = RuntimeRoomImageDisplayMode.Overlay;

        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var downImagePath = Path.Combine(tempRoot, "down.png");

        try
        {
            // 1x1 PNG, enough to verify load/render pipeline for overlay preview.
            File.WriteAllBytes(downImagePath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9f2NEAAAAASUVORK5CYII="));

            var down = viewModel.DownSlot;
            Assert.NotNull(down);

            down!.FullImagePath = downImagePath;

            Assert.True(down.HasConfiguredImage);
            Assert.NotNull(down.VariantThumbnailSource);
            Assert.Contains(viewModel.OverlayPreviewSlots, slot => slot.Entry.Slot == RoomImageSlot.Down);
            Assert.False(viewModel.ShowNoPreviewImagePlaceholder);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void RenderableRoomChildObjects_OnlyIncludesImmediateChildrenWithIncludedImage()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var objectImagePath = Path.Combine(tempRoot, "object.png");
            File.WriteAllBytes(objectImagePath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9f2NEAAAAASUVORK5CYII="));

            var room = new Room
            {
                Name = "Room A",
                Images = Enum.GetValues<RoomImageSlot>()
                    .Select(slot => new RoomImageEntry
                    {
                        Slot = slot,
                        OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                        Image = new RoomImageVariant()
                    })
                    .ToList(),
                GameObjects =
                [
                    new GameObject
                    {
                        Name = "Included",
                        ImageVariants =
                        [
                            new ObjectImageVariant { VariantName = "default", FullImagePath = objectImagePath, IsDefault = true }
                        ],
                        IncludeInPreview = true
                    },
                    new GameObject
                    {
                        Name = "Excluded",
                        ImageVariants =
                        [
                            new ObjectImageVariant { VariantName = "default", FullImagePath = objectImagePath, IsDefault = true }
                        ],
                        IncludeInPreview = false
                    },
                    new GameObject
                    {
                        Name = "NoImage",
                        IncludeInPreview = true
                    }
                ]
            };

            var viewModel = new RoomDesignerTabViewModel(room, 800, 600);

            var renderableNames = viewModel.RenderableRoomChildObjects.Select(item => item.DisplayName).ToList();

            Assert.Single(renderableNames);
            Assert.Equal("Included", renderableNames[0]);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void RoomChildObjectIncludeToggle_UpdatesRenderableCollection()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var objectImagePath = Path.Combine(tempRoot, "toggle.png");
            File.WriteAllBytes(objectImagePath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9f2NEAAAAASUVORK5CYII="));

            var room = new Room
            {
                Name = "Room A",
                Images = Enum.GetValues<RoomImageSlot>()
                    .Select(slot => new RoomImageEntry
                    {
                        Slot = slot,
                        OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                        Image = new RoomImageVariant()
                    })
                    .ToList(),
                GameObjects =
                [
                    new GameObject
                    {
                        Name = "Toggle",
                        ImageVariants =
                        [
                            new ObjectImageVariant { VariantName = "default", FullImagePath = objectImagePath, IsDefault = true }
                        ],
                        IncludeInPreview = true
                    }
                ]
            };

            var viewModel = new RoomDesignerTabViewModel(room, 800, 600);
            var item = viewModel.RoomChildObjects.Single();
            Assert.Single(viewModel.RenderableRoomChildObjects);

            item.IncludeInPreview = false;

            Assert.Empty(viewModel.RenderableRoomChildObjects);

            item.IncludeInPreview = true;

            Assert.Single(viewModel.RenderableRoomChildObjects);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MoveRoomChildObjectUp_ReordersRoomObjectsAndUpdatesDrawPriority()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var objectImagePath = Path.Combine(tempRoot, "order-up.png");
            File.WriteAllBytes(objectImagePath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9f2NEAAAAASUVORK5CYII="));

            var room = new Room
            {
                Name = "Room A",
                Images = Enum.GetValues<RoomImageSlot>()
                    .Select(slot => new RoomImageEntry
                    {
                        Slot = slot,
                        OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                        Image = new RoomImageVariant()
                    })
                    .ToList(),
                GameObjects =
                [
                    new GameObject { Name = "First", ImageVariants = [new ObjectImageVariant { VariantName = "default", FullImagePath = objectImagePath, IsDefault = true }], IncludeInPreview = true },
                    new GameObject { Name = "Second", ImageVariants = [new ObjectImageVariant { VariantName = "default", FullImagePath = objectImagePath, IsDefault = true }], IncludeInPreview = true }
                ]
            };

            var viewModel = new RoomDesignerTabViewModel(room, 800, 600);
            var second = viewModel.RoomChildObjects.Single(item => item.DisplayName == "Second");

            viewModel.MoveRoomChildObjectUpCommand.Execute(second);

            Assert.Equal("Second", room.GameObjects[0].Name);
            Assert.Equal("First", room.GameObjects[1].Name);
            Assert.Equal("Second", viewModel.RoomChildObjects[0].DisplayName);
            Assert.True(viewModel.RoomChildObjects[0].DrawOrderZIndex > viewModel.RoomChildObjects[1].DrawOrderZIndex);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MoveRoomChildObjectDown_ReordersRoomObjectsAndUpdatesDrawPriority()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var objectImagePath = Path.Combine(tempRoot, "order-down.png");
            File.WriteAllBytes(objectImagePath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9f2NEAAAAASUVORK5CYII="));

            var room = new Room
            {
                Name = "Room A",
                Images = Enum.GetValues<RoomImageSlot>()
                    .Select(slot => new RoomImageEntry
                    {
                        Slot = slot,
                        OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                        Image = new RoomImageVariant()
                    })
                    .ToList(),
                GameObjects =
                [
                    new GameObject { Name = "First", ImageVariants = [new ObjectImageVariant { VariantName = "default", FullImagePath = objectImagePath, IsDefault = true }], IncludeInPreview = true },
                    new GameObject { Name = "Second", ImageVariants = [new ObjectImageVariant { VariantName = "default", FullImagePath = objectImagePath, IsDefault = true }], IncludeInPreview = true }
                ]
            };

            var viewModel = new RoomDesignerTabViewModel(room, 800, 600);
            var first = viewModel.RoomChildObjects.Single(item => item.DisplayName == "First");

            viewModel.MoveRoomChildObjectDownCommand.Execute(first);

            Assert.Equal("Second", room.GameObjects[0].Name);
            Assert.Equal("First", room.GameObjects[1].Name);
            Assert.Equal("Second", viewModel.RoomChildObjects[0].DisplayName);
            Assert.True(viewModel.RoomChildObjects[0].DrawOrderZIndex > viewModel.RoomChildObjects[1].DrawOrderZIndex);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void EditRoomChildObjectImageCommand_InvokesProvidedEditCallback()
    {
        var room = new Room
        {
            Name = "Room A",
            Images = Enum.GetValues<RoomImageSlot>()
                .Select(slot => new RoomImageEntry
                {
                    Slot = slot,
                    OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                    Image = new RoomImageVariant()
                })
                .ToList(),
            GameObjects =
            [
                new GameObject { Name = "Editable" }
            ]
        };

        var invoked = false;
        var viewModel = new RoomDesignerTabViewModel(
            room,
            800,
            600,
            onEdited: null,
            projectFilePathAccessor: null,
            editRoomChildObjectImageAction: _ =>
            {
                invoked = true;
                return true;
            });

        var row = viewModel.RoomChildObjects.Single();
        viewModel.EditRoomChildObjectImageCommand.Execute(row);

        Assert.True(invoked);
    }

    [Fact]
    public void RoomChildObjectFilter_FiltersVisibleAndHiddenRowsByIncludeFlag()
    {
        var room = new Room
        {
            Name = "Room A",
            Images = Enum.GetValues<RoomImageSlot>()
                .Select(slot => new RoomImageEntry
                {
                    Slot = slot,
                    OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                    Image = new RoomImageVariant()
                })
                .ToList(),
            GameObjects =
            [
                new GameObject { Name = "Visible", IncludeInPreview = true },
                new GameObject { Name = "Hidden", IncludeInPreview = false }
            ]
        };

        var viewModel = new RoomDesignerTabViewModel(room, 800, 600);

        viewModel.RoomChildObjectFilter = "Visible";
        Assert.Single(viewModel.FilteredRoomChildObjects);
        Assert.Equal("Visible", viewModel.FilteredRoomChildObjects[0].DisplayName);

        viewModel.RoomChildObjectFilter = "Hidden";
        Assert.Single(viewModel.FilteredRoomChildObjects);
        Assert.Equal("Hidden", viewModel.FilteredRoomChildObjects[0].DisplayName);

        viewModel.RoomChildObjectFilter = "All";
        Assert.Equal(2, viewModel.FilteredRoomChildObjects.Count);
    }

    [Fact]
    public void NudgeSelectedRoomChildObject_AppliesDeltaAndClampsToBounds()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var objectImagePath = Path.Combine(tempRoot, "nudge.png");
            File.WriteAllBytes(objectImagePath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9f2NEAAAAASUVORK5CYII="));

            var room = new Room
            {
                Name = "Room A",
                Images = Enum.GetValues<RoomImageSlot>()
                    .Select(slot => new RoomImageEntry
                    {
                        Slot = slot,
                        OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                        Image = new RoomImageVariant()
                    })
                    .ToList(),
                GameObjects =
                [
                    new GameObject
                    {
                        Name = "Nudge",
                        IncludeInPreview = true,
                        ImageVariants =
                        [
                            new ObjectImageVariant { VariantName = "default", FullImagePath = objectImagePath, IsDefault = true }
                        ],
                        PositionX = 10,
                        PositionY = 10
                    }
                ]
            };

            var viewModel = new RoomDesignerTabViewModel(room, 800, 600);
            viewModel.IsSnapToGridEnabled = false;
            var selected = viewModel.RoomChildObjects.Single();
            viewModel.SelectedRoomChildObject = selected;

            var nudged = viewModel.NudgeSelectedRoomChildObject(5, 7, 100, 100, 20, 20);

            Assert.True(nudged);
            Assert.Equal(15, selected.PositionX);
            Assert.Equal(17, selected.PositionY);

            viewModel.NudgeSelectedRoomChildObject(200, 200, 100, 100, 20, 20);
            Assert.Equal(80, selected.PositionX);
            Assert.Equal(80, selected.PositionY);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void NudgeSelectedRoomChildObject_WithScaledObjectDimensions_ClampsUsingScaledBounds()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var objectImagePath = Path.Combine(tempRoot, "nudge-scaled.png");
            File.WriteAllBytes(objectImagePath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9f2NEAAAAASUVORK5CYII="));

            var room = new Room
            {
                Name = "Room A",
                Images = Enum.GetValues<RoomImageSlot>()
                    .Select(slot => new RoomImageEntry
                    {
                        Slot = slot,
                        OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                        Image = new RoomImageVariant()
                    })
                    .ToList(),
                GameObjects =
                [
                    new GameObject
                    {
                        Name = "ScaledNudge",
                        IncludeInPreview = true,
                        ImageVariants =
                        [
                            new ObjectImageVariant { VariantName = "default", FullImagePath = objectImagePath, ImageScale = 0.5, IsDefault = true }
                        ],
                        PositionX = 70,
                        PositionY = 10
                    }
                ]
            };

            var viewModel = new RoomDesignerTabViewModel(room, 800, 600);
            viewModel.IsSnapToGridEnabled = false;
            var selected = viewModel.RoomChildObjects.Single();
            viewModel.SelectedRoomChildObject = selected;

            // Base image size 40x40 with 0.5 scale means interactive bounds should be 20x20,
            // so max position in a 100x100 preview is 80 rather than 60.
            var nudged = viewModel.NudgeSelectedRoomChildObject(100, 0, 100, 100, 20, 20);

            Assert.True(nudged);
            Assert.Equal(80, selected.PositionX);
            Assert.Equal(10, selected.PositionY);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryUndoLastRoomChildMove_RestoresPreviousPositionAfterNudge()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var objectImagePath = Path.Combine(tempRoot, "undo-nudge.png");
            File.WriteAllBytes(objectImagePath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9f2NEAAAAASUVORK5CYII="));

            var room = new Room
            {
                Name = "Room A",
                Images = Enum.GetValues<RoomImageSlot>()
                    .Select(slot => new RoomImageEntry
                    {
                        Slot = slot,
                        OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                        Image = new RoomImageVariant()
                    })
                    .ToList(),
                GameObjects =
                [
                    new GameObject
                    {
                        Name = "Undoable",
                        IncludeInPreview = true,
                        ImageVariants =
                        [
                            new ObjectImageVariant { VariantName = "default", FullImagePath = objectImagePath, IsDefault = true }
                        ],
                        PositionX = 10,
                        PositionY = 10
                    }
                ]
            };

            var viewModel = new RoomDesignerTabViewModel(room, 800, 600);
            viewModel.IsSnapToGridEnabled = false;
            var selected = viewModel.RoomChildObjects.Single();
            viewModel.SelectedRoomChildObject = selected;

            Assert.True(viewModel.NudgeSelectedRoomChildObject(7, 3, 100, 100, 20, 20));
            Assert.Equal(17, selected.PositionX);
            Assert.Equal(13, selected.PositionY);

            Assert.True(viewModel.TryUndoLastRoomChildMove());
            Assert.Equal(10, selected.PositionX);
            Assert.Equal(10, selected.PositionY);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryUndoLastRoomChildMove_RestoresPreviousPositionAfterCommitMove()
    {
        var room = new Room
        {
            Name = "Room A",
            Images = Enum.GetValues<RoomImageSlot>()
                .Select(slot => new RoomImageEntry
                {
                    Slot = slot,
                    OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                    Image = new RoomImageVariant()
                })
                .ToList(),
            GameObjects =
            [
                new GameObject
                {
                    Name = "Undoable",
                    IncludeInPreview = true,
                    PositionX = 5,
                    PositionY = 6
                }
            ]
        };

        var viewModel = new RoomDesignerTabViewModel(room, 800, 600);
        var selected = viewModel.RoomChildObjects.Single();
        viewModel.SelectedRoomChildObject = selected;

        Assert.True(viewModel.TryCommitRoomChildObjectMove(selected, 5, 6, 25, 30));
        Assert.Equal(25, selected.PositionX);
        Assert.Equal(30, selected.PositionY);

        Assert.True(viewModel.TryUndoLastRoomChildMove());
        Assert.Equal(5, selected.PositionX);
        Assert.Equal(6, selected.PositionY);
    }

    [Fact]
    public void RoomChildPreviewVariantSelection_UsesSelectedVariantScale_WithoutPersistingObjectScale()
    {
        var room = new Room
        {
            Name = "Room A",
            Images = Enum.GetValues<RoomImageSlot>()
                .Select(slot => new RoomImageEntry
                {
                    Slot = slot,
                    OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                    Image = new RoomImageVariant()
                })
                .ToList(),
            GameObjects =
            [
                new GameObject
                {
                    Name = "Door",
                    ImageVariants =
                    [
                        new ObjectImageVariant { VariantName = "closed", FullImagePath = "closed.png", ImageScale = 0.6, IsDefault = true },
                        new ObjectImageVariant { VariantName = "open", FullImagePath = "open.png", ImageScale = 1.4 }
                    ]
                }
            ]
        };

        var viewModel = new RoomDesignerTabViewModel(room, 800, 600);
        var row = viewModel.RoomChildObjects.Single();

        Assert.Equal(0.6, row.ImageScale, 3);

        row.SelectPreviewVariantCommand.Execute("open");

        Assert.Equal("open", row.SelectedPreviewVariantName);
        Assert.Equal(1.4, row.ImageScale, 3);
        Assert.Equal(0.6, row.GameObject.ResolveImageScale(null), 3);
    }

    [Fact]
    public void RoomChildPreviewVariantSelection_ExposesVariantNamesForThumbnailMenu()
    {
        var room = new Room
        {
            Name = "Room A",
            Images = Enum.GetValues<RoomImageSlot>()
                .Select(slot => new RoomImageEntry
                {
                    Slot = slot,
                    OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                    Image = new RoomImageVariant()
                })
                .ToList(),
            GameObjects =
            [
                new GameObject
                {
                    Name = "Display",
                    ImageVariants =
                    [
                        new ObjectImageVariant { VariantName = "default", FullImagePath = "default.png", IsDefault = true },
                        new ObjectImageVariant { VariantName = "alt", FullImagePath = "alt.png" }
                    ]
                }
            ]
        };

        var viewModel = new RoomDesignerTabViewModel(room, 800, 600);
        var row = viewModel.RoomChildObjects.Single();

        Assert.True(row.HasMultiplePreviewVariants);
        Assert.Equal(2, row.AvailablePreviewVariantNames.Count);
        Assert.Contains("default", row.AvailablePreviewVariantNames);
        Assert.Contains("alt", row.AvailablePreviewVariantNames);
    }

    [Fact]
    public void RoomChildFootprintPixels_FallsBackToSingleCell_WhenFootprintBoundsAreMissing()
    {
        var room = new Room
        {
            Name = "Room A",
            Images = Enum.GetValues<RoomImageSlot>()
                .Select(slot => new RoomImageEntry
                {
                    Slot = slot,
                    OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                    Image = new RoomImageVariant()
                })
                .ToList(),
            GameObjects =
            [
                new GameObject
                {
                    Name = "FallbackFootprint",
                    IncludeInPreview = true,
                    FootprintWidthCells = 0,
                    FootprintHeightCells = 0,
                    FootprintOrientation = "N"
                }
            ]
        };

        var viewModel = new RoomDesignerTabViewModel(room, 800, 600, 40);
        var row = viewModel.RoomChildObjects.Single();

        Assert.Equal(1, row.OrientedFootprintWidthCells);
        Assert.Equal(1, row.OrientedFootprintHeightCells);
        Assert.Equal(40, row.FootprintWidthPixels);
        Assert.Equal(40, row.FootprintHeightPixels);
    }

    [Fact]
    public void RoomChildPreviewBaseIconSize_UsesOneCellFallbackWithoutImage_AndUsesCornerRelativePlacement()
    {
        var room = new Room
        {
            Name = "Room A",
            Images = Enum.GetValues<RoomImageSlot>()
                .Select(slot => new RoomImageEntry
                {
                    Slot = slot,
                    OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                    Image = new RoomImageVariant()
                })
                .ToList(),
            GameObjects =
            [
                new GameObject
                {
                    Name = "LargeFootprint",
                    IncludeInPreview = true,
                    FootprintWidthCells = 4,
                    FootprintHeightCells = 2,
                    FootprintOrientation = "N"
                }
            ]
        };

        var viewModel = new RoomDesignerTabViewModel(room, 800, 600, 40);
        var row = viewModel.RoomChildObjects.Single();

        Assert.Equal(160, row.FootprintWidthPixels);
        Assert.Equal(80, row.FootprintHeightPixels);
        Assert.Equal(40, row.PreviewBaseIconWidthPixels, 6);
        Assert.Equal(40, row.PreviewBaseIconHeightPixels, 6);
        Assert.Equal(40, row.PreviewBaseIconSizePixels, 6);
        Assert.Equal(0, row.ImageLocalAlignmentOffsetX, 6);
        Assert.Equal(0, row.ImageLocalAlignmentOffsetY, 6);

        row.UpdateGridCellSize(24);
        Assert.Equal(24, row.PreviewBaseIconWidthPixels, 3);
        Assert.Equal(24, row.PreviewBaseIconHeightPixels, 3);
        Assert.Equal(24, row.PreviewBaseIconSizePixels, 3);
        Assert.Equal(0, row.ImageLocalAlignmentOffsetX, 3);
        Assert.Equal(0, row.ImageLocalAlignmentOffsetY, 3);
    }

    [Fact]
    public void RoomChildPreviewPlacement_AppliesQuarterTurnFootprintCompensation()
    {
        var room = new Room
        {
            Name = "Room A",
            Images = Enum.GetValues<RoomImageSlot>()
                .Select(slot => new RoomImageEntry
                {
                    Slot = slot,
                    OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                    Image = new RoomImageVariant()
                })
                .ToList(),
            GameObjects =
            [
                new GameObject
                {
                    Name = "ScaledRotated",
                    IncludeInPreview = true,
                    FootprintWidthCells = 4,
                    FootprintHeightCells = 2,
                    FootprintOrientation = "N",
                    ImageRotationDegrees = 90,
                    ImageVariants =
                    [
                        new ObjectImageVariant
                        {
                            VariantName = "default",
                            ImageScale = 1.4,
                            IsDefault = true
                        }
                    ]
                }
            ]
        };

        var viewModel = new RoomDesignerTabViewModel(room, 800, 600, 40);
        var row = viewModel.RoomChildObjects.Single();

        Assert.Equal(80, row.ImageLocalAlignmentOffsetX, 6);
        Assert.Equal(0, row.ImageLocalAlignmentOffsetY, 6);
    }

    [Fact]
    public void RoomChildPreviewBaseIconSize_UsesNativeImagePixels_WithoutImplicitCenteringOffset()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var objectImagePath = Path.Combine(tempRoot, "native.png");
            File.WriteAllBytes(objectImagePath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9f2NEAAAAASUVORK5CYII="));

            var room = new Room
            {
                Name = "Room A",
                Images = Enum.GetValues<RoomImageSlot>()
                    .Select(slot => new RoomImageEntry
                    {
                        Slot = slot,
                        OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                        Image = new RoomImageVariant()
                    })
                    .ToList(),
                GameObjects =
                [
                    new GameObject
                    {
                        Name = "NativeSized",
                        IncludeInPreview = true,
                        FootprintWidthCells = 1,
                        FootprintHeightCells = 1,
                        FootprintOrientation = "N",
                        ImageVariants =
                        [
                            new ObjectImageVariant
                            {
                                VariantName = "default",
                                FullImagePath = objectImagePath,
                                ImageScale = 1,
                                IsDefault = true
                            }
                        ]
                    }
                ]
            };

            var viewModel = new RoomDesignerTabViewModel(room, 800, 600, 40);
            var row = viewModel.RoomChildObjects.Single();

            Assert.Equal(1, row.PreviewBaseIconWidthPixels, 6);
            Assert.Equal(1, row.PreviewBaseIconHeightPixels, 6);
            Assert.Equal(0, row.ImageLocalAlignmentOffsetX, 3);
            Assert.Equal(0, row.ImageLocalAlignmentOffsetY, 3);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void RoomChildFootprintOrientation_SwapsWithQuarterTurnRoomRotation()
    {
        var room = new Room
        {
            Name = "Room A",
            Images = Enum.GetValues<RoomImageSlot>()
                .Select(slot => new RoomImageEntry
                {
                    Slot = slot,
                    OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                    Image = new RoomImageVariant()
                })
                .ToList(),
            GameObjects =
            [
                new GameObject
                {
                    Name = "DoorLike",
                    IncludeInPreview = true,
                    FootprintWidthCells = 1,
                    FootprintHeightCells = 4,
                    FootprintOrientation = "N",
                    ImageRotationDegrees = 90
                }
            ]
        };

        var viewModel = new RoomDesignerTabViewModel(room, 800, 600, 40);
        var row = viewModel.RoomChildObjects.Single();

        Assert.Equal(4, row.OrientedFootprintWidthCells);
        Assert.Equal(1, row.OrientedFootprintHeightCells);
        Assert.Equal(160, row.FootprintWidthPixels);
        Assert.Equal(40, row.FootprintHeightPixels);

        row.RoomRotationDegrees = 180;

        Assert.Equal(1, row.OrientedFootprintWidthCells);
        Assert.Equal(4, row.OrientedFootprintHeightCells);
        Assert.Equal(40, row.FootprintWidthPixels);
        Assert.Equal(160, row.FootprintHeightPixels);
    }

    [Fact]
    public void PreviewPlacementComparisonMode_ExposesSharedOnlyMode()
    {
        var room = new Room
        {
            Name = "Room A",
            Images = Enum.GetValues<RoomImageSlot>()
                .Select(slot => new RoomImageEntry
                {
                    Slot = slot,
                    OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                    Image = new RoomImageVariant()
                })
                .ToList(),
            GameObjects =
            [
                new GameObject { Name = "One", IncludeInPreview = true },
                new GameObject { Name = "Two", IncludeInPreview = true }
            ]
        };

        var viewModel = new RoomDesignerTabViewModel(room, 800, 600, 40);
        Assert.Equal([RoomDesignerPreviewPlacementComparisonMode.SharedOnly], viewModel.PreviewPlacementComparisonModeOptions);
        Assert.Equal(RoomDesignerPreviewPlacementComparisonMode.SharedOnly, viewModel.PreviewPlacementComparisonMode);
        Assert.All(viewModel.RoomChildObjects, child =>
            Assert.Equal(RoomDesignerPreviewPlacementComparisonMode.SharedOnly, child.PlacementComparisonMode));
    }

    [Fact]
    public void RoomDesignerDiagnosticsLines_RespectSelectedDiagnosticsLevel()
    {
        var room = new Room
        {
            Name = "Room A",
            Images = Enum.GetValues<RoomImageSlot>()
                .Select(slot => new RoomImageEntry
                {
                    Slot = slot,
                    OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                    Image = new RoomImageVariant()
                })
                .ToList(),
            GameObjects =
            [
                new GameObject { Name = "One", IncludeInPreview = true }
            ]
        };

        var viewModel = new RoomDesignerTabViewModel(room, 800, 600, 40);

        viewModel.SelectedRoomDesignerDiagnosticsLevel = GameDiagnosticsLevel.None;
        Assert.Equal(["Diagnostics disabled."], viewModel.RoomDesignerDiagnosticsLines);

        viewModel.SelectedRoomDesignerDiagnosticsLevel = GameDiagnosticsLevel.High;
        Assert.Contains(viewModel.RoomDesignerDiagnosticsLines, line => line.Contains("mode=", StringComparison.Ordinal));
        Assert.Contains(viewModel.RoomDesignerDiagnosticsLines, line => line.Contains("delta=", StringComparison.Ordinal));
        Assert.Contains(viewModel.RoomDesignerDiagnosticsLines, line => line.Contains("selectedOffset=", StringComparison.Ordinal));
        Assert.Contains(viewModel.RoomDesignerDiagnosticsLines, line => line.Contains("roomRenderableCount=", StringComparison.Ordinal));
    }

    [Fact]
    public void RoomDesignerDiagnosticsLines_IncludeOverlayDirectionTelemetry()
    {
        var viewModel = CreateViewModel(600, 800);
        viewModel.RoomDisplayMode = RuntimeRoomImageDisplayMode.Overlay;
        viewModel.SelectedDirectionSlot = viewModel.DownSlot;
        viewModel.DownSlot!.OverlayRotationDegrees = 90;
        viewModel.DownSlot.OverlayOffsetX = 14;
        viewModel.DownSlot.OverlayOffsetY = -6;
        viewModel.DownSlot.FullImagePath = "down.png";
        viewModel.SelectedRoomDesignerDiagnosticsLevel = GameDiagnosticsLevel.Medium;

        var lines = viewModel.RoomDesignerDiagnosticsLines;

        Assert.Contains(lines, line => line.Contains("canvas=600x800", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("selected=none", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("selectedDirection=Down", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("rot=90", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("offset=(14,-6)", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("overlaySlot=Down", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("No selected room object.", StringComparison.Ordinal));
    }

    private static RoomDesignerTabViewModel CreateViewModel(int canvasWidth = 800, int canvasHeight = 600)
    {
        var room = new Room
        {
            Name = "Room A",
            Images = Enum.GetValues<RoomImageSlot>()
                .Select(slot => new RoomImageEntry
                {
                    Slot = slot,
                    OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                    Image = new RoomImageVariant()
                })
                .ToList()
        };

            return new RoomDesignerTabViewModel(room, canvasWidth, canvasHeight);
    }
}

