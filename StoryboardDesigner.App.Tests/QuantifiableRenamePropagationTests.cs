using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class QuantifiableRenamePropagationTests
{
    [Fact]
    public void EditObjectBasicProperties_CompositePartOptions_IncludeNonInventoriableRoomObjects()
    {
        var currentObject = new GameObject
        {
            Name = "Workbench",
            IsInventoriable = false
        };

        var inventoriableObject = new GameObject
        {
            Name = "Key",
            IsInventoriable = true
        };

        var nonInventoriableObject = new GameObject
        {
            Name = "Statue",
            IsInventoriable = false
        };

        var room = new Room
        {
            Name = "Workshop",
            GameObjects = [currentObject, inventoriableObject, nonInventoriableObject]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [room]
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country]
        };

        var project = new ProjectModel
        {
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var roomObjectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var objectNode = new GameObjectNodeViewModel(currentObject, roomObjectsNode);

        viewModel.EditObjectBasicProperties(objectNode);

        Assert.NotNull(treeContext.LastObjectBasicPropertiesRequest);
        var optionNames = treeContext.LastObjectBasicPropertiesRequest!.AvailableCompositePartOptions
            .Select(option => option.Name)
            .ToList();

        Assert.Contains("Key", optionNames, StringComparer.Ordinal);
        Assert.Contains("Statue", optionNames, StringComparer.Ordinal);
        Assert.DoesNotContain("Workbench", optionNames, StringComparer.Ordinal);
    }

    [Fact]
    public void EditObjectBasicProperties_RenameBaseQuantifiable_DoesNotPropagateToLinkedRoomInstances()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Flowers",
            IsQuantifiable = true,
            Quantity = 1,
            QuantifiablePlacementDistributionMode = "IndividualInstances"
        };

        var linkedInstance = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Flowers",
            IsQuantifiable = true,
            Quantity = 1,
            QuantifiablePlacementDistributionMode = "IndividualInstances",
            LinkedBaseObjectId = baseObjectId,
            LinkActionsToBaseObject = true
        };

        var roomA = new Room
        {
            Name = "Room A",
            GameObjects = [baseObject]
        };

        var roomB = new Room
        {
            Name = "Room B",
            GameObjects = [linkedInstance]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [roomA, roomB]
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country]
        };

        var project = new ProjectModel
        {
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with { Name = "Petals" }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(roomA, area, areaNode);
        var roomObjectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var baseObjectNode = new GameObjectNodeViewModel(baseObject, roomObjectsNode);

        var updated = viewModel.EditObjectBasicProperties(baseObjectNode);

        Assert.True(updated);
        Assert.Equal("Petals", baseObject.Name);
        Assert.Equal("Flowers", linkedInstance.Name);
    }

    [Fact]
    public void EditObjectBasicProperties_RoomObject_PersistsNameSynonymsEdits()
    {
        var roomObject = new GameObject
        {
            Name = "Knife",
            NameInGame = "Knife",
            NameSynonyms = ["blade"]
        };

        var room = new Room
        {
            Name = "Workshop",
            GameObjects = [roomObject]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [room]
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country]
        };

        var project = new ProjectModel
        {
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with
            {
                ObjectNameSynonyms = "blade, shiv"
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var roomObjectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var objectNode = new GameObjectNodeViewModel(roomObject, roomObjectsNode);

        var updated = viewModel.EditObjectBasicProperties(objectNode);

        Assert.True(updated);
        Assert.Equal(2, roomObject.NameSynonyms.Count);
        Assert.Contains("blade", roomObject.NameSynonyms, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("shiv", roomObject.NameSynonyms, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void EditObjectBasicProperties_RenameBaseQuantifiable_MultipleLinkedInSameRoom_LeavesLinkedNamesUntouched()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Marble",
            IsQuantifiable = true,
            Quantity = 1,
            QuantifiablePlacementDistributionMode = "IndividualInstances"
        };

        var linkedA = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Marble_1",
            IsQuantifiable = true,
            Quantity = 1,
            QuantifiablePlacementDistributionMode = "IndividualInstances",
            LinkedBaseObjectId = baseObjectId,
            LinkActionsToBaseObject = true
        };

        var linkedB = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Marble_2",
            IsQuantifiable = true,
            Quantity = 1,
            QuantifiablePlacementDistributionMode = "IndividualInstances",
            LinkedBaseObjectId = baseObjectId,
            LinkActionsToBaseObject = true
        };

        var room = new Room
        {
            Name = "Opening Room",
            GameObjects = [baseObject, linkedA, linkedB]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [room]
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country]
        };

        var project = new ProjectModel
        {
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with { Name = "Marbles" }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var roomObjectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var baseObjectNode = new GameObjectNodeViewModel(baseObject, roomObjectsNode);

        var updated = viewModel.EditObjectBasicProperties(baseObjectNode);

        Assert.True(updated);
        Assert.Equal("Marbles", baseObject.Name);
        Assert.Equal("Marble_1", linkedA.Name);
        Assert.Equal("Marble_2", linkedB.Name);
    }

    [Fact]
    public void EditObjectBasicProperties_RenameBaseIndividualQuantifiable_WithUnchangedQuantity_LeavesExistingLinkedNamesUntouched()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Marble",
            IsQuantifiable = true,
            Quantity = 3,
            QuantifiablePlacementDistributionMode = "IndividualInstances"
        };

        var linkedA = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Marble",
            IsQuantifiable = true,
            Quantity = 1,
            QuantifiablePlacementDistributionMode = "IndividualInstances",
            LinkedBaseObjectId = baseObjectId,
            LinkActionsToBaseObject = true
        };

        var linkedB = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Marble",
            IsQuantifiable = true,
            Quantity = 1,
            QuantifiablePlacementDistributionMode = "IndividualInstances",
            LinkedBaseObjectId = baseObjectId,
            LinkActionsToBaseObject = true
        };

        var room = new Room
        {
            Name = "Opening Room",
            GameObjects = [baseObject, linkedA, linkedB]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [room]
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country]
        };

        var project = new ProjectModel
        {
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with { Name = "Marbles", Quantity = 3 }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var roomObjectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var baseObjectNode = new GameObjectNodeViewModel(baseObject, roomObjectsNode);

        var updated = viewModel.EditObjectBasicProperties(baseObjectNode);

        Assert.True(updated);
        Assert.Equal(3, room.GameObjects.Count);
        Assert.Equal("Marbles", baseObject.Name);
        Assert.Equal("Marble", linkedA.Name);
        Assert.Equal("Marble", linkedB.Name);
        Assert.DoesNotContain(room.GameObjects, obj => obj.Name.Contains("_", StringComparison.Ordinal));
    }

    [Fact]
    public void EditObjectBasicProperties_RenameLinkedInstance_DoesNotWriteThroughToBase()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Flowers",
            IsQuantifiable = true,
            Quantity = 1,
            QuantifiablePlacementDistributionMode = "IndividualInstances"
        };

        var linkedInstance = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Flowers",
            IsQuantifiable = true,
            Quantity = 1,
            QuantifiablePlacementDistributionMode = "IndividualInstances",
            LinkedBaseObjectId = baseObjectId,
            LinkActionsToBaseObject = true
        };

        var roomA = new Room
        {
            Name = "Room A",
            GameObjects = [baseObject]
        };

        var roomB = new Room
        {
            Name = "Room B",
            GameObjects = [linkedInstance]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [roomA, roomB]
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country]
        };

        var project = new ProjectModel
        {
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with { Name = "Flowers North" }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(roomB, area, areaNode);
        var roomObjectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var linkedObjectNode = new GameObjectNodeViewModel(linkedInstance, roomObjectsNode);

        var updated = viewModel.EditObjectBasicProperties(linkedObjectNode);

        Assert.True(updated);
        Assert.Equal("Flowers", baseObject.Name);
        Assert.Equal("Flowers North", linkedInstance.Name);
        Assert.False(linkedInstance.InstanceOverrides.ContainsKey("Name"));
    }

    [Fact]
    public void EditObjectBasicProperties_PlayerLinkedInstanceRename_DoesNotWriteThroughToBase()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Flowers",
            IsQuantifiable = true,
            Quantity = 1,
            QuantifiablePlacementDistributionMode = "IndividualInstances"
        };

        var linkedPlayerObject = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Flowers",
            IsQuantifiable = true,
            Quantity = 1,
            QuantifiablePlacementDistributionMode = "IndividualInstances",
            LinkedBaseObjectId = baseObjectId,
            LinkActionsToBaseObject = true
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [baseObject]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [room]
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country]
        };

        var project = new ProjectModel
        {
            Planets = [planet],
            GameObjects = [linkedPlayerObject]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with { Name = "Flowers Player" }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var playerNode = new GlobalObjectsNodeViewModel(project, rootNode);
        var linkedObjectNode = new GlobalObjectNodeViewModel(linkedPlayerObject, playerNode);

        var updated = viewModel.EditObjectBasicProperties(linkedObjectNode);

        Assert.True(updated);
        Assert.Equal("Flowers", baseObject.Name);
        Assert.Equal("Flowers Player", linkedPlayerObject.Name);
        Assert.False(linkedPlayerObject.InstanceOverrides.ContainsKey("Name"));
    }

    [Fact]
    public void EditObjectBasicProperties_RenameSelectedPlayerObject_PreservesVariables_WithoutRetargetingPlayerSelectionName()
    {
        var playerObject = new GameObject
        {
            Name = "Player",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isPlayer",
                    DefaultValue = "true",
                    Lifetime = GamePropertyLifetime.Singleton,
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse
                },
                new GamePropertyDefinition
                {
                    Name = "health",
                    DefaultValue = "100",
                    Lifetime = GamePropertyLifetime.Singleton,
                    ValueRestriction = GamePropertyValueRestriction.Numeric
                }
            ]
        };

        var project = new ProjectModel
        {
            PlayerCharacterObjectName = "Player",
            GlobalObjectScopeName = "Global Objects",
            GameObjects = [playerObject]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with { Name = "PlayerX" }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var playerNode = new GlobalObjectsNodeViewModel(project, rootNode);
        var playerObjectNode = new GlobalObjectNodeViewModel(playerObject, playerNode);

        var updated = viewModel.EditObjectBasicProperties(playerObjectNode);

        Assert.True(updated);
        Assert.Equal("Player", project.PlayerCharacterObjectName);
        Assert.Contains(
            playerObject.Variables,
            variable => string.Equals(variable.Name, "isPlayer", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(variable.DefaultValue, "true", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            playerObject.Variables,
            variable => string.Equals(variable.Name, "health", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(variable.DefaultValue, "100", StringComparison.Ordinal));
    }

    [Fact]
    public void EditObjectBasicProperties_PlayerNameInGameEdit_DoesNotRemoveIsPlayerVariable_WhenNoGlobalPlayerSelectionSet()
    {
        var playerObject = new GameObject
        {
            Name = "Player",
            NameInGame = "The Hero",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isPlayer",
                    DefaultValue = "true",
                    Lifetime = GamePropertyLifetime.Singleton,
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse
                },
                new GamePropertyDefinition
                {
                    Name = "health",
                    DefaultValue = "100",
                    Lifetime = GamePropertyLifetime.Singleton,
                    ValueRestriction = GamePropertyValueRestriction.Numeric
                }
            ]
        };

        var project = new ProjectModel
        {
            PlayerCharacterObjectName = string.Empty,
            GlobalObjectScopeName = "Global Objects",
            GameObjects = [playerObject]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with { NameInGame = "PlayerX" }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var playerNode = new GlobalObjectsNodeViewModel(project, rootNode);
        var playerObjectNode = new GlobalObjectNodeViewModel(playerObject, playerNode);

        var updated = viewModel.EditObjectBasicProperties(playerObjectNode);

        Assert.True(updated);
        Assert.Equal("PlayerX", playerObject.NameInGame);
        Assert.Contains(
            playerObject.Variables,
            variable => string.Equals(variable.Name, "isPlayer", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(variable.DefaultValue, "true", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            playerObject.Variables,
            variable => string.Equals(variable.Name, "health", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(variable.DefaultValue, "100", StringComparison.Ordinal));
    }

    [Fact]
    public void AddNewContainedPlayerObject_DoesNotChangeTruePlayerCharacterContractSelection()
    {
        var selectedPlayerObject = new GameObject
        {
            Name = "Hero",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isPlayer",
                    DefaultValue = "true",
                    Lifetime = GamePropertyLifetime.Singleton,
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse
                }
            ]
        };

        var inventoryHolderObject = new GameObject
        {
            Name = "Backpack"
        };

        var project = new ProjectModel
        {
            PlayerCharacterObjectName = "Hero",
            GlobalObjectScopeName = "Global Objects",
            GameObjects = [selectedPlayerObject, inventoryHolderObject]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with { Name = "Potion" }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var playerNode = new GlobalObjectsNodeViewModel(project, rootNode);
        var inventoryHolderNode = new GlobalObjectNodeViewModel(inventoryHolderObject, playerNode);

        var handled = viewModel.ExecuteTreeContextAction("add-new-object", inventoryHolderNode);

        Assert.True(handled);
        Assert.Equal("Hero", project.PlayerCharacterObjectName);
        Assert.Contains(
            selectedPlayerObject.Variables,
            variable => string.Equals(variable.Name, "isPlayer", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(variable.DefaultValue, "true", StringComparison.OrdinalIgnoreCase));
        Assert.Single(inventoryHolderObject.ContainedObjects);
        Assert.DoesNotContain(
            inventoryHolderObject.ContainedObjects[0].Variables,
            variable => string.Equals(variable.Name, "isPlayer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RemovePlayerCollectionObject_WhenSelectedAsTruePlayer_ClearsSelectionAndReappliesContract()
    {
        var selectedPlayerObject = new GameObject
        {
            Name = "Hero",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isPlayer",
                    DefaultValue = "true",
                    Lifetime = GamePropertyLifetime.Singleton,
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse
                }
            ]
        };

        var otherGlobalObject = new GameObject
        {
            Name = "Companion",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isPlayer",
                    DefaultValue = "true",
                    Lifetime = GamePropertyLifetime.Singleton,
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse
                }
            ]
        };

        var project = new ProjectModel
        {
            PlayerCharacterObjectName = "Hero",
            GlobalObjectScopeName = "Global Objects",
            GameObjects = [selectedPlayerObject, otherGlobalObject]
        };

        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var playerNode = new GlobalObjectsNodeViewModel(project, rootNode);
        var selectedPlayerNode = new GlobalObjectNodeViewModel(selectedPlayerObject, playerNode);

        var handled = viewModel.ExecuteTreeContextAction("remove-object", selectedPlayerNode);

        Assert.True(handled);
        Assert.Equal(string.Empty, project.PlayerCharacterObjectName);
        Assert.DoesNotContain(project.GameObjects, objectModel => objectModel.ObjectId == selectedPlayerObject.ObjectId);
        Assert.DoesNotContain(
            otherGlobalObject.Variables,
            variable => string.Equals(variable.Name, "isPlayer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EditObjectBasicProperties_TemplateLinkedInstanceRename_DoesNotWriteThroughToBase()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Flowers",
            IsQuantifiable = true,
            Quantity = 1,
            QuantifiablePlacementDistributionMode = "IndividualInstances"
        };

        var linkedTemplateObject = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Flowers",
            IsQuantifiable = true,
            Quantity = 1,
            QuantifiablePlacementDistributionMode = "IndividualInstances",
            LinkedBaseObjectId = baseObjectId,
            LinkActionsToBaseObject = true
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [baseObject]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [room]
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country]
        };

        var project = new ProjectModel
        {
            Planets = [planet],
            ObjectTemplates = [linkedTemplateObject]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with { Name = "Flowers Template" }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var templatesNode = new ObjectTemplatesNodeViewModel(project, rootNode);
        var linkedObjectNode = new TemplateGameObjectNodeViewModel(linkedTemplateObject, templatesNode);

        var updated = viewModel.EditObjectBasicProperties(linkedObjectNode);

        Assert.True(updated);
        Assert.Equal("Flowers", baseObject.Name);
        Assert.Equal("Flowers Template", linkedTemplateObject.Name);
        Assert.False(linkedTemplateObject.InstanceOverrides.ContainsKey("Name"));
    }

    [Fact]
    public void EditObjectBasicProperties_TemplateObject_IncludesChooserVariableChoicesAndSelfAliasTokens()
    {
        var templateObject = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "DoorTemplate",
            IsOpenable = true
        };
        templateObject.ApplyFeatureVariableContract();

        var project = new ProjectModel
        {
            ObjectTemplates = [templateObject]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with { ProducerNotes = "chooser-test" }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var templatesNode = new ObjectTemplatesNodeViewModel(project, rootNode);
        var templateNode = new TemplateGameObjectNodeViewModel(templateObject, templatesNode);

        var edited = viewModel.EditObjectBasicProperties(templateNode);

        Assert.True(edited);
        var request = Assert.IsType<ObjectBasicPropertiesEditRequest>(treeContext.LastObjectBasicPropertiesRequest);
        Assert.NotNull(request.ImageVariantChooserVariableChoices);
        Assert.NotNull(request.ImageVariantChooserReferenceTokens);
        Assert.Contains(request.ImageVariantChooserVariableChoices!, choice =>
            string.Equals(choice.Value, "owner.isOpen", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(request.ImageVariantChooserReferenceTokens!, token =>
            string.Equals(token, "owner.isOpen", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EditObjectBasicProperties_LinkedInstance_UsesInstanceOwnedName_WhileApplyingAllowedOverrides()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Flowers",
            IsInventoriable = false,
            IsContainer = false,
            IsOpenable = false,
            IsQuantifiable = true,
            Quantity = 1,
            QuantifiablePlacementDistributionMode = "IndividualInstances"
        };

        var linkedInstance = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Flowers",
            IsInventoriable = false,
            IsContainer = false,
            IsOpenable = false,
            IsQuantifiable = true,
            Quantity = 1,
            QuantifiablePlacementDistributionMode = "IndividualInstances",
            LinkedBaseObjectId = baseObjectId,
            LinkActionsToBaseObject = true
        };

        var roomA = new Room
        {
            Name = "Room A",
            GameObjects = [baseObject]
        };

        var roomB = new Room
        {
            Name = "Room B",
            GameObjects = [linkedInstance]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [roomA, roomB]
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country]
        };

        var project = new ProjectModel
        {
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with
            {
                Name = "Flowers Local",
                ObjectNameSynonyms = "petals, bloom",
                Description = "Room-specific description",
                IsInventoriable = true,
                IsContainer = true,
                IsOpenable = true
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(roomB, area, areaNode);
        var roomObjectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var linkedObjectNode = new GameObjectNodeViewModel(linkedInstance, roomObjectsNode);

        var updated = viewModel.EditObjectBasicProperties(linkedObjectNode);

        Assert.True(updated);
        Assert.Equal("Flowers", baseObject.Name);
        Assert.Equal("Flowers Local", linkedInstance.Name);
        Assert.False(linkedInstance.IsInventoriable);
        Assert.False(linkedInstance.IsContainer);
        Assert.False(linkedInstance.IsOpenable);
        Assert.False(linkedInstance.InstanceOverrides.ContainsKey("Name"));
        Assert.True(linkedInstance.InstanceOverrides.TryGetValue("NameSynonyms", out var nameSynonymsOverride));
        Assert.Equal("petals, bloom", nameSynonymsOverride);
        Assert.True(linkedInstance.InstanceOverrides.TryGetValue("Description", out var descriptionOverride));
        Assert.Equal("Room-specific description", descriptionOverride);
    }

    [Fact]
    public void EditObjectBasicProperties_LinkedInstance_PassesReadOnlyGuidanceNoticeToDialogRequest()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Flowers"
        };

        var linkedInstance = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Flowers",
            LinkedBaseObjectId = baseObjectId,
            LinkActionsToBaseObject = true
        };

        var roomA = new Room
        {
            Name = "Room A",
            GameObjects = [baseObject]
        };

        var roomB = new Room
        {
            Name = "Room B",
            GameObjects = [linkedInstance]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [roomA, roomB]
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country]
        };

        var project = new ProjectModel
        {
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(roomB, area, areaNode);
        var roomObjectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var linkedObjectNode = new GameObjectNodeViewModel(linkedInstance, roomObjectsNode);

        var updated = viewModel.EditObjectBasicProperties(linkedObjectNode);

        Assert.False(updated);
        Assert.NotNull(treeContext.LastObjectBasicPropertiesRequest);
        Assert.Contains("linked instance", treeContext.LastObjectBasicPropertiesRequest!.LinkedEditNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Base item path", treeContext.LastObjectBasicPropertiesRequest.LinkedEditNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Flowers", treeContext.LastObjectBasicPropertiesRequest.LinkedBaseObjectName);
    }

    [Fact]
    public void EditObjectBasicProperties_LinkedInstance_SeedsDialogNameFromLocalInstanceName()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "DoorBase"
        };

        var linkedInstance = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "North Door",
            LinkedBaseObjectId = baseObjectId,
            LinkActionsToBaseObject = true
        };

        var roomA = new Room
        {
            Name = "Room A",
            GameObjects = [baseObject]
        };

        var roomB = new Room
        {
            Name = "Room B",
            GameObjects = [linkedInstance]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [roomA, roomB]
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country]
        };

        var project = new ProjectModel
        {
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(roomB, area, areaNode);
        var roomObjectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var linkedObjectNode = new GameObjectNodeViewModel(linkedInstance, roomObjectsNode);

        var updated = viewModel.EditObjectBasicProperties(linkedObjectNode);

        Assert.False(updated);
        Assert.NotNull(treeContext.LastObjectBasicPropertiesRequest);
        Assert.Equal("North Door", treeContext.LastObjectBasicPropertiesRequest!.Name);
        Assert.Equal("DoorBase", treeContext.LastObjectBasicPropertiesRequest.LinkedBaseObjectName);
    }

    [Fact]
    public void EditObjectBasicProperties_LinkedInstance_UpdatesInstanceNameInGameWithoutSparseOverride()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Flowers",
            NameInGame = "A bunch of flowers"
        };

        var linkedInstance = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Flowers",
            NameInGame = "A bunch of flowers",
            LinkedBaseObjectId = baseObjectId,
            LinkActionsToBaseObject = true
        };

        var roomA = new Room
        {
            Name = "Room A",
            GameObjects = [baseObject]
        };

        var roomB = new Room
        {
            Name = "Room B",
            GameObjects = [linkedInstance]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [roomA, roomB]
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country]
        };

        var project = new ProjectModel
        {
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with
            {
                NameInGame = "Fresh flowers"
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(roomB, area, areaNode);
        var roomObjectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var linkedObjectNode = new GameObjectNodeViewModel(linkedInstance, roomObjectsNode);

        var updated = viewModel.EditObjectBasicProperties(linkedObjectNode);

        Assert.True(updated);
        Assert.Equal("Fresh flowers", linkedInstance.NameInGame);
        Assert.False(linkedInstance.InstanceOverrides.ContainsKey("NameInGame"));
    }

    [Fact]
    public void EditObjectBasicProperties_LinkedInstance_IgnoresNameSynonymEdits_WhileApplyingInstanceNameInGameEdit()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Flowers",
            NameInGame = "A bunch of flowers",
            NameSynonyms = ["bouquet", "blossoms"]
        };

        var linkedInstance = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Flowers",
            NameInGame = "A bunch of flowers",
            NameSynonyms = ["bouquet", "blossoms"],
            LinkedBaseObjectId = baseObjectId,
            LinkActionsToBaseObject = true
        };

        var roomA = new Room
        {
            Name = "Room A",
            GameObjects = [baseObject]
        };

        var roomB = new Room
        {
            Name = "Room B",
            GameObjects = [linkedInstance]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [roomA, roomB]
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country]
        };

        var project = new ProjectModel
        {
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initialValues => initialValues with
            {
                NameInGame = "Fresh flowers",
                ObjectNameSynonyms = "petals, bloom"
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(roomB, area, areaNode);
        var roomObjectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var linkedObjectNode = new GameObjectNodeViewModel(linkedInstance, roomObjectsNode);

        var updated = viewModel.EditObjectBasicProperties(linkedObjectNode);

        Assert.True(updated);
        Assert.Equal("Fresh flowers", linkedInstance.NameInGame);
        Assert.Equal(2, linkedInstance.NameSynonyms.Count);
        Assert.Contains("bouquet", linkedInstance.NameSynonyms, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("blossoms", linkedInstance.NameSynonyms, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("petals", linkedInstance.NameSynonyms, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("bloom", linkedInstance.NameSynonyms, StringComparer.OrdinalIgnoreCase);
    }

}
