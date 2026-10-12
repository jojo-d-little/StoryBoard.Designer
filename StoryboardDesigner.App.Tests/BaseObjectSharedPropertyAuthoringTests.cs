using System.Text.Json;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class BaseObjectSharedPropertyAuthoringTests
{
    [Fact]
    public void BaseObjectShare_CreatesAddsRemovesAndRoundTripsMembersWithoutTouchingOtherGroupsOrPlacementDerivatives()
    {
        var externalShareId = Guid.NewGuid();
        var root = new GameObject
        {
            Name = "Base Elevator",
            Variables = [BooleanProperty("IsMoving", "false")],
            ContainedObjects =
            [
                new GameObject { Name = "Left Door", Variables = [BooleanProperty("isHidden", "false")] },
                new GameObject { Name = "Right Door", Variables = [BooleanProperty("isHidden", "false")] }
            ]
        };
        var otherRoomObject = new GameObject { Name = "Other Object", Variables = [BooleanProperty("NightMode", "false", externalShareId)] };
        var roomNightMode = BooleanProperty("NightMode", "false", externalShareId);
        var room = new Room
        {
            Name = "Foyer",
            Variables = [roomNightMode],
            GameObjects = [otherRoomObject]
        };
        var area = new Area { Name = "Lobby", BaseObjects = [root], Rooms = [room] };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var unrelatedShare = new SharedVariableDefinition
        {
            Id = externalShareId,
            Name = "Unrelated night state",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            Participants =
            [
                Participant("global", roomNightMode.Id, "NightMode"),
                Participant("object", otherRoomObject.ObjectId, "NightMode")
            ]
        };
        var project = new ProjectModel { Name = "BaseShareAuthoring", Planets = [planet], SharedVariables = [unrelatedShare] };
        ScopeHierarchy.AttachParents(project);

        var selectedTarget = string.Empty;
        var treeContext = new TreeContextInteractionServiceStub
        {
            ChooseVariableHandler = (choices, _, _, _) =>
            {
                Assert.Contains(choices, choice => choice.Value == selectedTarget);
                return (true, selectedTarget);
            }
        };
        var projectUi = new ProjectUiServiceStub { ConfirmResult = true };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, projectUi);
        var roomNode = BuildRoomNode(project, room);
        var rootProperty = BuildPropertyNode(root, roomNode, "IsMoving");
        var leftProperty = BuildPropertyNode(root.ContainedObjects[0], roomNode, "isHidden");
        var rightProperty = BuildPropertyNode(root.ContainedObjects[1], roomNode, "isHidden");

        Assert.True(viewModel.ExecuteTreeContextAction("create-shared-game-property", rootProperty));
        var authoredShareId = Assert.IsType<Guid>(rootProperty.Variable.SharedVariableId);
        Assert.NotEqual(externalShareId, authoredShareId);

        selectedTarget = ShareTarget(root.ContainedObjects[0], "isHidden");
        Assert.True(viewModel.ExecuteTreeContextAction("share-game-property", rootProperty));
        Assert.Contains(treeContext.LastVariableChoices, choice => choice.Value == selectedTarget && choice.ScopePath.Contains("Object Hierarchy"));

        selectedTarget = ShareTarget(root.ContainedObjects[1], "isHidden");
        Assert.True(viewModel.ExecuteTreeContextAction("share-game-property", rootProperty));
        Assert.Equal(authoredShareId, leftProperty.Variable.SharedVariableId);
        Assert.Equal(authoredShareId, rightProperty.Variable.SharedVariableId);
        Assert.Equal(3, Assert.Single(project.SharedVariables, share => share.Id == authoredShareId).Participants.Count);

        var placement = CloneAsLinkedPlacement(root);
        room.GameObjects.Add(placement);
        ScopeHierarchy.AttachParents(project);
        LinkedBaseObjectSharedVariableCloner.PreserveAuthoredMemberships(project, root, placement);
        SharedVariableReconciliationService.ReconcileInPlace(project);
        var placementShareId = Assert.IsType<Guid>(placement.Variables.Single(variable => variable.Name == "IsMoving").SharedVariableId);
        Assert.NotEqual(authoredShareId, placementShareId);
        Assert.All(EnumerateVariables(placement), variable => Assert.Equal(placementShareId, variable.SharedVariableId));
        Assert.Equal(authoredShareId, root.Variables.Single(variable => variable.Name == "IsMoving").SharedVariableId);
        Assert.Contains(project.SharedVariables.Single(share => share.Id == externalShareId).Participants,
            participant => participant.Kind == "object" && participant.OwnerId == otherRoomObject.ObjectId && participant.VariableName == "NightMode");
        Assert.Contains(project.SharedVariables.Single(share => share.Id == externalShareId).Participants,
            participant => participant.Kind == "global" && participant.OwnerId == roomNightMode.Id && participant.VariableName == "NightMode");

        Assert.True(viewModel.ExecuteTreeContextAction("leave-shared-variable", leftProperty));
        Assert.Null(leftProperty.Variable.SharedVariableId);
        Assert.Equal(authoredShareId, rootProperty.Variable.SharedVariableId);
        Assert.Equal(authoredShareId, rightProperty.Variable.SharedVariableId);
        Assert.Equal(2, project.SharedVariables.Single(share => share.Id == authoredShareId).Participants.Count);
        Assert.Equal(placementShareId, placement.ContainedObjects[0].Variables.Single(variable => variable.Name == "isHidden").SharedVariableId);

        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var projectPath = Path.Combine(tempRoot, "BaseShareAuthoring.sbe.json");
            var export = new JsonExportService();
            export.SaveProjectModel(projectPath, project);
            var reopened = Assert.IsType<ProjectModel>(export.TryLoadProjectModel(projectPath));
            var reopenedArea = reopened.Planets.Single().Countries.Single().Areas.Single();
            var reopenedRoot = reopenedArea.BaseObjects.Single();
            var reopenedPlacement = reopenedArea.Rooms.Single().GameObjects.Single(gameObject => gameObject.LinkedBaseObjectId == reopenedRoot.ObjectId);
            Assert.Equal(authoredShareId, reopenedRoot.Variables.Single(variable => variable.Name == "IsMoving").SharedVariableId);
            Assert.Null(reopenedRoot.ContainedObjects[0].Variables.Single(variable => variable.Name == "isHidden").SharedVariableId);
            Assert.Equal(authoredShareId, reopenedRoot.ContainedObjects[1].Variables.Single(variable => variable.Name == "isHidden").SharedVariableId);
            Assert.Equal(placementShareId, reopenedPlacement.Variables.Single(variable => variable.Name == "IsMoving").SharedVariableId);
            Assert.Equal(2, reopened.SharedVariables.Single(share => share.Id == authoredShareId).Participants.Count);
            Assert.Equal(externalShareId, reopened.SharedVariables.Single(share => share.Id == externalShareId).Id);
            Assert.Equal(2, reopened.SharedVariables.Single(share => share.Id == externalShareId).Participants.Count);

            export.ExportCleanProjectV1(projectPath, reopened);
            AssertRuntimeShareId(projectPath, reopenedRoot.ObjectId, "IsMoving", authoredShareId);
            using var baseRuntime = LoadRuntimeObject(projectPath, reopenedRoot.ObjectId);
            AssertRuntimeShareId(baseRuntime.RootElement.GetProperty("containedObjects")[1], "isHidden", authoredShareId);
            AssertRuntimeShareId(projectPath, reopenedPlacement.ObjectId, "IsMoving", placementShareId);
            using var placementRuntime = LoadRuntimeObject(projectPath, reopenedPlacement.ObjectId);
            AssertRuntimeShareId(placementRuntime.RootElement.GetProperty("containedObjects")[0], "isHidden", placementShareId);
            AssertRuntimeShareId(projectPath, room.Id, "NightMode", externalShareId, "Room");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static GamePropertyDefinition BooleanProperty(string name, string defaultValue, Guid? sharedVariableId = null) => new()
    {
        Name = name,
        DefaultValue = defaultValue,
        ValueRestriction = GamePropertyValueRestriction.TrueFalse,
        SharedVariableId = sharedVariableId
    };

    private static SharedVariableParticipant Participant(string kind, Guid ownerId, string variableName) => new()
    {
        Kind = kind,
        OwnerId = ownerId,
        VariableName = variableName
    };

    private static GameObject CloneAsLinkedPlacement(GameObject source) => new()
    {
        Name = source.Name,
        LinkedBaseObjectId = source.ObjectId,
        LinkActionsToBaseObject = true,
        Variables = source.Variables.Select(CloneProperty).ToList(),
        ContainedObjects = source.ContainedObjects.Select(CloneChild).ToList()
    };

    private static GameObject CloneChild(GameObject source) => new()
    {
        Name = source.Name,
        LinkedBaseObjectId = source.ObjectId,
        LinkActionsToBaseObject = true,
        Variables = source.Variables.Select(CloneProperty).ToList(),
        ContainedObjects = source.ContainedObjects.Select(CloneChild).ToList()
    };

    private static GamePropertyDefinition CloneProperty(GamePropertyDefinition source) => new()
    {
        Name = source.Name,
        DefaultValue = source.DefaultValue,
        Lifetime = source.Lifetime,
        ValueRestriction = source.ValueRestriction,
        SharedVariableId = source.SharedVariableId
    };

    private static IEnumerable<GamePropertyDefinition> EnumerateVariables(GameObject root)
    {
        foreach (var variable in root.Variables)
        {
            yield return variable;
        }

        foreach (var child in root.ContainedObjects)
        {
            foreach (var variable in EnumerateVariables(child))
            {
                yield return variable;
            }
        }
    }

    private static string ShareTarget(GameObject owner, string propertyName) => $"object:{owner.ObjectId:N}:{propertyName}";

    private static GamePropertyNodeViewModel BuildPropertyNode(GameObject owner, RoomNodeViewModel roomNode, string propertyName)
    {
        var parentNode = new GameObjectNodeViewModel(owner, new RoomGameObjectsNodeViewModel(roomNode));
        var propertiesNode = new GamePropertiesContainerNodeViewModel(PropertyResolutionScope.Object, parentNode);
        parentNode.Children.Add(propertiesNode);
        var node = new GamePropertyNodeViewModel(owner.Variables.Single(variable => variable.Name == propertyName), PropertyResolutionScope.Object, propertiesNode);
        propertiesNode.Children.Add(node);
        return node;
    }

    private static RoomNodeViewModel BuildRoomNode(ProjectModel project, Room room)
    {
        var planet = Assert.Single(project.Planets);
        var country = Assert.Single(planet.Countries);
        var area = Assert.Single(country.Areas);
        var root = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, root);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        return new RoomNodeViewModel(room, area, areaNode);
    }

    private static void AssertRuntimeShareId(string projectPath, Guid objectId, string propertyName, Guid expectedShareId, string scopeFolder = "GameObject")
    {
        using var document = LoadRuntimeObject(projectPath, objectId, scopeFolder);
        AssertRuntimeShareId(document.RootElement, propertyName, expectedShareId);
    }

    private static JsonDocument LoadRuntimeObject(string projectPath, Guid objectId, string scopeFolder = "GameObject")
    {
        var jsonPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "GameRuntimeJson", scopeFolder,
            objectId.ToString("D").ToUpperInvariant() + ".runtime.json");
        return JsonDocument.Parse(File.ReadAllText(jsonPath));
    }

    private static void AssertRuntimeShareId(JsonElement objectJson, string propertyName, Guid expectedShareId)
    {
        var property = objectJson.GetProperty("gameProperties").EnumerateArray()
            .Single(item => string.Equals(item.GetProperty("name").GetString(), propertyName, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(expectedShareId.ToString("D"), property.GetProperty("sharedVariableId").GetString());
    }
}
