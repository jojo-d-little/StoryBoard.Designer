using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class TraversalWizardDialogViewModelTests
{
    [Fact]
    public void SelectAllAndClearAll_OnlyAffectSelectableRows()
    {
        var vm = new TraversalWizardDialogViewModel(new TraversalWizardDialogRequest
        {
            SourceRoomName = "Room A",
            AreaName = "Area 1",
            EffectiveTraversalMode = AreaAdjacencyMode.FourDirectional,
            DoorTemplateOptions = Array.Empty<TraversalWizardDoorTemplateOption>(),
            Rows =
            [
                new TraversalWizardDirectionSeed
                {
                    Direction = Direction.North,
                    DirectionLabel = "N",
                    DestinationRoomName = "Room B",
                    HasImmediateNeighbor = true,
                    CanToggleSelection = true,
                    DefaultIncludeTraversal = true,
                    StatusText = "Ready to add traversal."
                },
                new TraversalWizardDirectionSeed
                {
                    Direction = Direction.NorthEast,
                    DirectionLabel = "NE",
                    DestinationRoomName = "Room C",
                    HasImmediateNeighbor = true,
                    CanToggleSelection = true,
                    DefaultIncludeTraversal = false,
                    StatusText = "Diagonal neighbor available. Enable to override 4-way default."
                },
                new TraversalWizardDirectionSeed
                {
                    Direction = Direction.East,
                    DirectionLabel = "E",
                    DestinationRoomName = "Room D",
                    HasImmediateNeighbor = true,
                    HasExistingTraversal = true,
                    CanToggleSelection = false,
                    DefaultIncludeTraversal = false,
                    StatusText = "Traversal already defined - left unchanged."
                },
                new TraversalWizardDirectionSeed
                {
                    Direction = Direction.South,
                    DirectionLabel = "S",
                    HasImmediateNeighbor = false,
                    CanToggleSelection = false,
                    DefaultIncludeTraversal = false,
                    StatusText = "No immediate neighboring room in this direction."
                }
            ]
        });

        Assert.Equal(1, vm.SelectedCount);

        vm.SelectAllCommand.Execute(null);

        Assert.Equal(2, vm.SelectedCount);
        Assert.True(vm.Rows[0].IncludeTraversal);
        Assert.True(vm.Rows[1].IncludeTraversal);
        Assert.False(vm.Rows[2].IncludeTraversal);
        Assert.False(vm.Rows[3].IncludeTraversal);

        vm.ClearAllCommand.Execute(null);

        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.Rows[0].IncludeTraversal);
        Assert.False(vm.Rows[1].IncludeTraversal);
        Assert.False(vm.Rows[2].IncludeTraversal);
        Assert.False(vm.Rows[3].IncludeTraversal);
    }

    [Fact]
    public void BuildResult_NormalizesDoorSettings_WhenDoorNotApplicable()
    {
        var vm = new TraversalWizardDialogViewModel(new TraversalWizardDialogRequest
        {
            SourceRoomName = "Room A",
            AreaName = "Area 1",
            EffectiveTraversalMode = AreaAdjacencyMode.EightDirectional,
            DoorTemplateOptions =
            [
                new TraversalWizardDoorTemplateOption
                {
                    TemplateId = Guid.NewGuid(),
                    DisplayName = "Door Template"
                }
            ],
            Rows =
            [
                new TraversalWizardDirectionSeed
                {
                    Direction = Direction.West,
                    DirectionLabel = "W",
                    DestinationRoomName = "Room B",
                    HasImmediateNeighbor = true,
                    CanToggleSelection = true,
                    DefaultIncludeTraversal = true,
                    DefaultDoorEnabled = true,
                    DefaultDoorState = TraversalWizardDoorDefaultStateOption.Closed,
                    DefaultDoorLockedWhenClosed = true,
                    DefaultDoorTemplateId = null,
                    StatusText = "Ready to add traversal."
                }
            ]
        });

        var row = Assert.Single(vm.Rows);
        row.DoorState = TraversalWizardDoorDefaultStateOption.Open;
        Assert.False(row.DoorLockedWhenClosed);

        row.IncludeTraversal = false;
        var disabledChoice = Assert.Single(vm.BuildResult().Rows);
        Assert.False(disabledChoice.IncludeTraversal);
        Assert.False(disabledChoice.DoorEnabled);
        Assert.Equal(TraversalWizardDoorDefaultStateOption.Open, disabledChoice.DoorState);
        Assert.False(disabledChoice.DoorLockedWhenClosed);

        row.IncludeTraversal = true;
        row.DoorEnabled = false;
        var noDoorChoice = Assert.Single(vm.BuildResult().Rows);
        Assert.True(noDoorChoice.IncludeTraversal);
        Assert.False(noDoorChoice.DoorEnabled);
        Assert.Equal(TraversalWizardDoorDefaultStateOption.Open, noDoorChoice.DoorState);
        Assert.False(noDoorChoice.DoorLockedWhenClosed);
        Assert.Null(noDoorChoice.DoorTemplateId);
    }
}
