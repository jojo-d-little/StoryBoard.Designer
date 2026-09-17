using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelVariableChoicesTests
{
    [Fact]
    public void GetVariableChoicesForScopeNode_ObjectScope_IncludesNearByQuantityForQuantifiableObject()
    {
        var flowers = new GameObject
        {
            Name = "Flowers",
            IsQuantifiable = true,
            Quantity = 3
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [flowers]
        };

        var project = CreateProjectWithRoom(room);
        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var choices = viewModel.GetVariableChoicesForScopeNode(flowers, PropertyResolutionScope.Object);

        Assert.Contains(choices, choice => string.Equals(choice.Value, "self.nearByQuantity", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(choices, choice => string.Equals(choice.Value, "Flowers.nearByQuantity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetVariableChoicesForScopeNode_ObjectScope_DoesNotIncludeNearByQuantityForNonQuantifiableObject()
    {
        var key = new GameObject
        {
            Name = "Key",
            IsQuantifiable = false
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [key]
        };

        var project = CreateProjectWithRoom(room);
        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var choices = viewModel.GetVariableChoicesForScopeNode(key, PropertyResolutionScope.Object);

        Assert.DoesNotContain(choices, choice => string.Equals(choice.Value, "self.nearByQuantity", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(choices, choice => string.Equals(choice.Value, "Key.nearByQuantity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetVariableChoicesForScopeNode_RoomScope_IncludesSelfAliasesForRoomVariables()
    {
        var room = new Room
        {
            Name = "Room A",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "ambientLight",
                    DefaultValue = "dim",
                    ValueRestriction = GamePropertyValueRestriction.Unrestricted,
                    Lifetime = GamePropertyLifetime.Singleton
                }
            ]
        };

        var project = CreateProjectWithRoom(room);
        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var choices = viewModel.GetVariableChoicesForScopeNode(room, PropertyResolutionScope.Room);

        Assert.Contains(choices, choice => string.Equals(choice.Value, "ambientLight", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(choices, choice => string.Equals(choice.Value, "self.name", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(choices, choice => string.Equals(choice.Value, "self.nameInGame", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(choices, choice => string.Equals(choice.Value, "self.ambientLight", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetVariableChoicesForScopeNode_TemplateRoomScope_IncludesSelfAliasesForRoomVariables()
    {
        var templateRoom = new Room
        {
            Name = "Template Room",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "roomFlavor",
                    DefaultValue = "quiet",
                    ValueRestriction = GamePropertyValueRestriction.Unrestricted,
                    Lifetime = GamePropertyLifetime.Singleton
                }
            ]
        };

        var project = new ProjectModel
        {
            RoomTemplates = [templateRoom]
        };
        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var choices = viewModel.GetVariableChoicesForScopeNode(templateRoom, PropertyResolutionScope.Room);

        Assert.Contains(choices, choice => string.Equals(choice.Value, "roomFlavor", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(choices, choice => string.Equals(choice.Value, "self.name", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(choices, choice => string.Equals(choice.Value, "self.nameInGame", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(choices, choice => string.Equals(choice.Value, "self.roomFlavor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetVariableChoicesForScopeNode_GlobalScope_IncludesProjectWideChoices()
    {
        var globalObject = new GameObject
        {
            Name = "GlobalAnchor",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "globalObjectState",
                    DefaultValue = "ready",
                    ValueRestriction = GamePropertyValueRestriction.Unrestricted,
                    Lifetime = GamePropertyLifetime.Singleton
                }
            ]
        };

        var room = new Room
        {
            Name = "Room A",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "roomMood",
                    DefaultValue = "calm",
                    ValueRestriction = GamePropertyValueRestriction.Unrestricted,
                    Lifetime = GamePropertyLifetime.Singleton
                }
            ]
        };

        var project = CreateProjectWithRoom(room);
        project.GlobalVariables.Add(new GamePropertyDefinition
        {
            Name = "globalFlag",
            DefaultValue = "on",
            ValueRestriction = GamePropertyValueRestriction.Unrestricted,
            Lifetime = GamePropertyLifetime.Singleton
        });
        project.GlobalScope.GameObjects.Add(globalObject);
        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var choices = viewModel.GetVariableChoicesForScopeNode(project, PropertyResolutionScope.Global);

        Assert.NotEmpty(choices);
        Assert.Contains(choices, choice => string.Equals(choice.Value, "globalFlag", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(choices, choice => string.Equals(choice.Value, "roomMood", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(choices, choice => string.Equals(choice.Value, "GlobalAnchor.globalObjectState", StringComparison.OrdinalIgnoreCase));
    }

    private static ProjectModel CreateProjectWithRoom(Room room)
    {
        return new ProjectModel
        {
            Planets =
            [
                new Planet
                {
                    Name = "Planet A",
                    Countries =
                    [
                        new Country
                        {
                            Name = "Country A",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "Area A",
                                    Rooms = [room]
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }
}
