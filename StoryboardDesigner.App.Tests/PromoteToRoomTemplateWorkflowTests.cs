using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using System.Reflection;

namespace StoryboardDesigner.App.Tests;

public sealed class PromoteToRoomTemplateWorkflowTests
{
    [Fact]
    public void GetTreeContextActions_Room_IncludesCreateTemplateFrom()
    {
        var project = BuildProject(out var roomNode);
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var actions = viewModel.GetTreeContextActions(roomNode);

        Assert.Contains(actions, action => string.Equals(action.ActionId, "create-template-from-room", StringComparison.Ordinal));
    }

    [Fact]
    public void GetTreeContextActions_Room_IncludesDeleteRoom()
    {
        var project = BuildProject(out var roomNode);
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var actions = viewModel.GetTreeContextActions(roomNode);

        Assert.Contains(actions, action => string.Equals(action.ActionId, "delete-room", StringComparison.Ordinal));
    }

    [Fact]
    public void ExecuteTreeContextAction_CreateTemplateFromRoom_CreatesRoomTemplateDefinition()
    {
        var project = BuildProject(out var roomNode);
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var handled = viewModel.ExecuteTreeContextAction("create-template-from-room", roomNode);

        Assert.True(handled);
        var definition = Assert.Single(project.RoomTemplates);
        Assert.Equal(roomNode.Room.Name, definition.Name);
        Assert.NotEqual(roomNode.Room.Id, definition.Id);
        Assert.Equal(roomNode.Room.GameObjects.Count, definition.GameObjects.Count);
    }

    [Fact]
    public void ExecuteTreeContextAction_DeleteRoom_RemovesRoomFromAreaAndHierarchy()
    {
        var project = BuildProjectModel(out var sourceRoomId);
        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var sourceNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<RoomNodeViewModel>()
            .Single(node => node.Room.Id == sourceRoomId);

        var area = project.Planets.Single().Countries.Single().Areas.Single();
        area.StartingRoomId = sourceRoomId;

        var handled = viewModel.ExecuteTreeContextAction("delete-room", sourceNode);

        Assert.True(handled);
        Assert.Empty(area.Rooms);
        Assert.Null(area.StartingRoomId);
        Assert.DoesNotContain(
            EnumerateNodes(viewModel.HierarchyRoots).OfType<RoomNodeViewModel>(),
            node => node.Room.Id == sourceRoomId);
    }

    [Fact]
    public void ExecuteTreeContextAction_CreateTemplateFromRoom_AddsNodeToRoomTemplateCatalogTree()
    {
        var project = BuildProjectModel(out var sourceRoomId);
        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var sourceNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<RoomNodeViewModel>()
            .Single(node => node.Room.Id == sourceRoomId);

        var handled = viewModel.ExecuteTreeContextAction("create-template-from-room", sourceNode);

        Assert.True(handled);

        var promotedDefinition = Assert.Single(project.RoomTemplates);
        var templateCatalogNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<RoomTemplatesNodeViewModel>()
            .Single(node => node.Parent is ProjectRootNodeViewModel);

        Assert.Contains(
            templateCatalogNode.Children.OfType<TemplateRoomNodeViewModel>(),
            node => node.Room.Id == promotedDefinition.Id);
    }

    [Fact]
    public void ExecuteTreeContextAction_CreateTemplateFromRoom_WhenDuplicateExists_ShowsWarningAndDoesNotCreate()
    {
        var project = BuildProjectModel(out var sourceRoomId);
        var sourceRoom = project.Planets
            .SelectMany(planet => planet.Countries)
            .SelectMany(country => country.Areas)
            .SelectMany(area => area.Rooms)
            .Single(room => room.Id == sourceRoomId);

        project.RoomTemplates.Add(new Room { Name = sourceRoom.Name });
        ScopeHierarchy.AttachParents(project);

        var projectUi = new ProjectUiServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            projectUi);

        InvokeLoadProjectIntoHierarchy(viewModel);

        var sourceNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<RoomNodeViewModel>()
            .Single(node => node.Room.Id == sourceRoomId);

        var handled = viewModel.ExecuteTreeContextAction("create-template-from-room", sourceNode);

        Assert.False(handled);
        Assert.Single(project.RoomTemplates);
        Assert.Contains(projectUi.WarningMessages, message => message.Contains("already exists", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExecuteTreeContextAction_CreateTemplateFromRoom_WhenNamePromptCanceled_DoesNotCreate()
    {
        var project = BuildProject(out var roomNode);
        var treeContext = new TreeContextInteractionServiceStub
        {
            TemplateNameFromObjectHandler = _ => null
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var handled = viewModel.ExecuteTreeContextAction("create-template-from-room", roomNode);

        Assert.False(handled);
        Assert.Empty(project.RoomTemplates);
    }

    [Fact]
    public void GetTreeContextActions_RoomTemplatesCatalog_IncludesAddNewRoomTemplate()
    {
        var project = BuildProjectModel(out _);
        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var templatesNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<RoomTemplatesNodeViewModel>()
            .Single(node => node.Parent is ProjectRootNodeViewModel);

        var actions = viewModel.GetTreeContextActions(templatesNode);

        Assert.Contains(actions, action => string.Equals(action.ActionId, "add-new-room-template", StringComparison.Ordinal));
    }

    [Fact]
    public void ExecuteTreeContextAction_AddNewRoomTemplate_AddsRoomTemplate()
    {
        var project = BuildProjectModel(out _);
        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            TemplateNameFromObjectHandler = _ => "Puzzle Room Template"
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var templatesNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<RoomTemplatesNodeViewModel>()
            .Single(node => node.Parent is ProjectRootNodeViewModel);

        var handled = viewModel.ExecuteTreeContextAction("add-new-room-template", templatesNode);

        Assert.True(handled);
        Assert.Contains(project.RoomTemplates, room => string.Equals(room.Name, "Puzzle Room Template", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetTreeContextActions_TemplateRoom_IncludesRoomStyleEditingActions()
    {
        var project = BuildProjectModel(out _);
        project.RoomTemplates.Add(new Room { Name = "Template Room" });
        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var templateRoomNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<TemplateRoomNodeViewModel>()
            .Single(node => node.Room.Name == "Template Room");

        var actions = viewModel.GetTreeContextActions(templateRoomNode);

        Assert.Contains(actions, action => string.Equals(action.ActionId, "edit-scoped-actions", StringComparison.Ordinal));
        Assert.Contains(actions, action => string.Equals(action.ActionId, "edit-scoped-verbs", StringComparison.Ordinal));
        Assert.Contains(actions, action => string.Equals(action.ActionId, "edit-scoped-directionals", StringComparison.Ordinal));
        Assert.Contains(actions, action => string.Equals(action.ActionId, "delete-room-template", StringComparison.Ordinal));
        Assert.Contains(actions, action => string.Equals(action.ActionId, "add-new-object", StringComparison.Ordinal));
    }

    [Fact]
    public void ExecuteTreeContextAction_DeleteRoomTemplate_RemovesTemplateFromProjectAndHierarchy()
    {
        var project = BuildProjectModel(out _);
        var templateRoom = new Room { Name = "Template Room" };
        project.RoomTemplates.Add(templateRoom);
        ScopeHierarchy.AttachParents(project);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var templateRoomNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<TemplateRoomNodeViewModel>()
            .Single(node => ReferenceEquals(node.Room, templateRoom));

        var handled = viewModel.ExecuteTreeContextAction("delete-room-template", templateRoomNode);

        Assert.True(handled);
        Assert.Empty(project.RoomTemplates);
        Assert.DoesNotContain(
            EnumerateNodes(viewModel.HierarchyRoots).OfType<TemplateRoomNodeViewModel>(),
            node => ReferenceEquals(node.Room, templateRoom));
    }

    [Fact]
    public void AddObjectToCurrentRoom_WithTemplateRoomSelected_AddsObjectToTemplateRoom()
    {
        var project = BuildProjectModel(out _);
        var templateRoom = new Room { Name = "Template Room" };
        project.RoomTemplates.Add(templateRoom);
        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initial => initial with { Name = "Template Lever" }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var templateRoomNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<TemplateRoomNodeViewModel>()
            .Single(node => ReferenceEquals(node.Room, templateRoom));

        viewModel.SelectedNode = templateRoomNode;

        var handled = viewModel.AddObjectToCurrentRoom();

        Assert.True(handled);
        Assert.Single(templateRoom.GameObjects);
        Assert.Equal("Template Lever", templateRoom.GameObjects[0].Name);

        var templateObjectsNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<ObjectTemplatesNodeViewModel>()
            .Single(node => node.Parent is TemplateRoomNodeViewModel parent && ReferenceEquals(parent.Room, templateRoom));
        var projectedObject = Assert.Single(templateObjectsNode.Children.OfType<TemplateGameObjectNodeViewModel>());
        Assert.Equal("Template Lever", projectedObject.GameObject.Name);
        Assert.Same(projectedObject, viewModel.SelectedNode);
    }

    [Fact]
    public void ReviewGameProperties_TemplateRoomVariables_OpensScopeReview()
    {
        var project = BuildProjectModel(out _);
        var templateRoom = new Room { Name = "Template Room" };
        templateRoom.Variables.Add(new GamePropertyDefinition { Name = "isLit", DefaultValue = "false" });
        project.RoomTemplates.Add(templateRoom);
        ScopeHierarchy.AttachParents(project);

        var reviewOpened = false;
        var treeContext = new TreeContextInteractionServiceStub
        {
            ShowVariableScopeReviewHandler = (scopeLabel, variables) =>
            {
                reviewOpened = true;
                Assert.Equal("Room - Template Room", scopeLabel);
                Assert.Contains(variables, variable => string.Equals(variable.Name, "isLit", StringComparison.Ordinal));
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var templateVariablesNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<GamePropertiesContainerNodeViewModel>()
            .Single(node => node.Parent is TemplateRoomNodeViewModel parent && ReferenceEquals(parent.Room, templateRoom));

        var handled = viewModel.ExecuteTreeContextAction("review-game-properties", templateVariablesNode);

        Assert.True(handled);
        Assert.True(reviewOpened);
    }

    [Fact]
    public void ExecuteTreeContextAction_AddNewRoom_WhenTemplatePickerCanceled_DoesNotCreateRoom()
    {
        var project = BuildProjectModel(out _);
        project.RoomTemplates.Add(new Room { Name = "Template Room" });
        ScopeHierarchy.AttachParents(project);

        var editRoomSettingsCalled = false;
        var treeContext = new TreeContextInteractionServiceStub
        {
            RoomTemplateSelectionHandler = _ => (false, null),
            EditRoomSettingsHandler = initial =>
            {
                editRoomSettingsCalled = true;
                return initial;
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var areaNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<AreaNodeViewModel>()
            .Single();
        var originalRoomCount = areaNode.Area.Rooms.Count;

        var handled = viewModel.ExecuteTreeContextAction("add-new-room", areaNode);

        Assert.False(handled);
        Assert.Equal(originalRoomCount, areaNode.Area.Rooms.Count);
        Assert.False(editRoomSettingsCalled);
    }

    [Fact]
    public void ExecuteTreeContextAction_AddNewRoom_WithBlankSelection_CreatesBlankRoom()
    {
        var project = BuildProjectModel(out _);
        project.RoomTemplates.Add(new Room
        {
            Name = "Template Room",
            GameObjects = [new GameObject { Name = "Template Object" }]
        });
        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            RoomTemplateSelectionHandler = _ => (true, null),
            EditRoomSettingsHandler = _ => new RoomSettingsEditRequest("Manual Room Name", "Manual producer notes", "Manual Room Alias")
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var areaNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<AreaNodeViewModel>()
            .Single();

        var handled = viewModel.ExecuteTreeContextAction("add-new-room", areaNode);

        Assert.True(handled);
        var createdRoom = Assert.Single(areaNode.Area.Rooms, room => string.Equals(room.Name, "Manual Room Name", StringComparison.Ordinal));
        Assert.Equal("Manual Room Alias", createdRoom.NameInGame);
        Assert.Equal("Manual producer notes", createdRoom.ProducerNotes);
        Assert.Empty(createdRoom.GameObjects);
    }

    [Fact]
    public void ExecuteTreeContextAction_AddNewRoom_WithSelectedTemplate_ClonesTemplateButUsesManualName()
    {
        var project = BuildProjectModel(out _);
        var templateRoom = new Room
        {
            Name = "Puzzle Template",
            Description = "Template description.",
            ProducerNotes = "Template producer notes.",
            RoomDisplayMode = RuntimeRoomImageDisplayMode.Overlay,
            GameObjects = [new GameObject { Name = "Switch" }]
        };
        project.RoomTemplates.Add(templateRoom);
        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            RoomTemplateSelectionHandler = templates => (true, templates.Single(room => room.Name == "Puzzle Template")),
            EditRoomSettingsHandler = _ => new RoomSettingsEditRequest("Manual Room Name", "Manual producer notes", "Puzzle Room Alias")
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var areaNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<AreaNodeViewModel>()
            .Single();

        var handled = viewModel.ExecuteTreeContextAction("add-new-room", areaNode);

        Assert.True(handled);
        var createdRoom = areaNode.Area.Rooms.Single(room => string.Equals(room.Name, "Manual Room Name", StringComparison.Ordinal));
        Assert.Equal("Puzzle Room Alias", createdRoom.NameInGame);
        Assert.Equal("Manual producer notes", createdRoom.ProducerNotes);
        Assert.Equal("Template description.", createdRoom.Description);
        Assert.Equal(RuntimeRoomImageDisplayMode.Overlay, createdRoom.RoomDisplayMode);
        Assert.Single(createdRoom.GameObjects);
        Assert.Equal("Switch", createdRoom.GameObjects[0].Name);
        Assert.NotEqual(templateRoom.Id, createdRoom.Id);
    }

    [Fact]
    public void ExecuteTreeContextAction_AddNewRoom_WithSelectedTemplate_SeedsDialogWithTemplateNameInGame()
    {
        var project = BuildProjectModel(out _);
        var templateRoom = new Room
        {
            Name = "Puzzle Template",
            NameInGame = "Template Alias"
        };
        project.RoomTemplates.Add(templateRoom);
        ScopeHierarchy.AttachParents(project);

        RoomSettingsEditRequest? capturedInitial = null;
        var treeContext = new TreeContextInteractionServiceStub
        {
            RoomTemplateSelectionHandler = templates => (true, templates.Single(room => room.Name == "Puzzle Template")),
            EditRoomSettingsHandler = initial =>
            {
                capturedInitial = initial;
                return initial with { Name = "Created Room" };
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var areaNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<AreaNodeViewModel>()
            .Single();

        var handled = viewModel.ExecuteTreeContextAction("add-new-room", areaNode);

        Assert.True(handled);
        Assert.NotNull(capturedInitial);
        Assert.Equal("Template Alias", capturedInitial!.NameInGame);
    }

    [Fact]
    public void ExecuteTreeContextAction_CreateTemplateFromRoom_DropsExternalSharedLinksAndRemapsInternal()
    {
        var sharedId = Guid.NewGuid();
        var shared = new SharedVariableDefinition
        {
            Id = sharedId,
            Name = "shared_switch",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse
        };

        var sourceObject = new GameObject
        {
            Name = "Room Switch",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isOn",
                    DefaultValue = "false",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                    SharedVariableId = sharedId
                }
            ]
        };

        var externalObject = new GameObject
        {
            Name = "External Switch",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isOn",
                    DefaultValue = "false",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                    SharedVariableId = sharedId
                }
            ]
        };

        shared.Participants.Add(new SharedVariableParticipant
        {
            Kind = "object",
            OwnerId = sourceObject.ObjectId,
            VariableName = "isOn"
        });
        shared.Participants.Add(new SharedVariableParticipant
        {
            Kind = "object",
            OwnerId = externalObject.ObjectId,
            VariableName = "isOn"
        });

        var room = new Room { Name = "Source Room", GameObjects = [sourceObject] };
        var externalRoom = new Room { Name = "External Room", GameObjects = [externalObject] };
        var area = new Area { Name = "Area", Rooms = [room, externalRoom] };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel
        {
            Name = "SharedBoundary",
            GlobalObjectScopeName = "Global Objects",
            Planets = [planet],
            SharedVariables = [shared]
        };

        ScopeHierarchy.AttachParents(project);

        var projectUi = new ProjectUiServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            projectUi);

        InvokeLoadProjectIntoHierarchy(viewModel);

        var sourceNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<RoomNodeViewModel>()
            .Single(node => node.Room.Name == "Source Room");

        var handled = viewModel.ExecuteTreeContextAction("create-template-from-room", sourceNode);

        Assert.True(handled);
        var template = Assert.Single(project.RoomTemplates);
        var templateVariable = Assert.Single(template.GameObjects[0].Variables, variable => variable.Name == "isOn");
        Assert.True(templateVariable.SharedVariableId.HasValue);
        Assert.NotEqual(sharedId, templateVariable.SharedVariableId.Value);

        var clonedShared = Assert.Single(project.SharedVariables, variable => variable.Id == templateVariable.SharedVariableId.Value);
        Assert.Single(clonedShared.Participants);
        Assert.Contains(projectUi.WarningMessages, message => message.Contains("dropped out-of-boundary", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExecuteTreeContextAction_AddNewRoom_FromTemplate_DropsExternalSharedLinksAndRemapsInternal()
    {
        var sharedId = Guid.NewGuid();
        var templateObject = new GameObject
        {
            Name = "Template Switch",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isOn",
                    DefaultValue = "false",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                    SharedVariableId = sharedId
                }
            ]
        };
        var templateRoom = new Room
        {
            Name = "Template Room",
            GameObjects = [templateObject]
        };

        var externalObject = new GameObject
        {
            Name = "External Switch",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isOn",
                    DefaultValue = "false",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                    SharedVariableId = sharedId
                }
            ]
        };

        var shared = new SharedVariableDefinition
        {
            Id = sharedId,
            Name = "shared_template_switch",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            Participants =
            [
                new SharedVariableParticipant
                {
                    Kind = "object",
                    OwnerId = templateObject.ObjectId,
                    VariableName = "isOn"
                },
                new SharedVariableParticipant
                {
                    Kind = "object",
                    OwnerId = externalObject.ObjectId,
                    VariableName = "isOn"
                }
            ]
        };

        var sourceRoom = new Room { Name = "Source Room" };
        var externalRoom = new Room { Name = "External Room", GameObjects = [externalObject] };
        var area = new Area { Name = "Area", Rooms = [sourceRoom, externalRoom] };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel
        {
            Name = "SharedBootstrapBoundary",
            GlobalObjectScopeName = "Global Objects",
            Planets = [planet],
            RoomTemplates = [templateRoom],
            SharedVariables = [shared]
        };

        ScopeHierarchy.AttachParents(project);

        var projectUi = new ProjectUiServiceStub();
        var treeContext = new TreeContextInteractionServiceStub
        {
            RoomTemplateSelectionHandler = templates => (true, templates.Single(room => room.Name == "Template Room")),
            EditRoomSettingsHandler = _ => new RoomSettingsEditRequest("Bootstrapped Room", "notes", "Boot Alias")
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, projectUi);
        InvokeLoadProjectIntoHierarchy(viewModel);

        var areaNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<AreaNodeViewModel>()
            .Single();

        var handled = viewModel.ExecuteTreeContextAction("add-new-room", areaNode);

        Assert.True(handled);
        var createdRoom = areaNode.Area.Rooms.Single(room => room.Name == "Bootstrapped Room");
        Assert.Equal("Boot Alias", createdRoom.NameInGame);
        var createdVariable = Assert.Single(createdRoom.GameObjects[0].Variables, variable => variable.Name == "isOn");
        Assert.True(createdVariable.SharedVariableId.HasValue);
        Assert.NotEqual(sharedId, createdVariable.SharedVariableId.Value);
        Assert.Contains(projectUi.WarningMessages, message => message.Contains("dropped out-of-boundary", StringComparison.OrdinalIgnoreCase));
    }

    private static ProjectModel BuildProject(out RoomNodeViewModel roomNode)
    {
        var project = BuildProjectModel(out var roomId);

        var rootNode = new ProjectRootNodeViewModel(project);
        var planet = project.Planets[0];
        var country = planet.Countries[0];
        var area = country.Areas[0];
        var room = area.Rooms.Single(r => r.Id == roomId);

        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        roomNode = new RoomNodeViewModel(room, area, areaNode);

        return project;
    }

    private static ProjectModel BuildProjectModel(out Guid sourceRoomId)
    {
        var sourceRoom = new Room
        {
            Name = "Puzzle Room",
            Description = "A room with puzzle elements.",
            GameObjects =
            [
                new GameObject
                {
                    Name = "Switch",
                    IsActivatable = true,
                    IsActiveDefaultValue = false
                }
            ]
        };

        sourceRoomId = sourceRoom.Id;

        var area = new Area
        {
            Name = "Area A",
            Rooms = [sourceRoom]
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

        return new ProjectModel
        {
            Name = "PromoteRoomTemplateFixture",
            GlobalObjectScopeName = "Global Objects",
            Planets = [planet]
        };
    }

    private static void InvokeLoadProjectIntoHierarchy(MainWindowViewModel viewModel)
    {
        var method = typeof(MainWindowViewModel)
            .GetMethod("LoadProjectIntoHierarchy", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method!.Invoke(viewModel, null);
    }

    private static IEnumerable<HierarchyNodeViewModel> EnumerateNodes(IEnumerable<HierarchyNodeViewModel> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var child in EnumerateNodes(root.Children.Cast<HierarchyNodeViewModel>()))
            {
                yield return child;
            }
        }
    }
}

