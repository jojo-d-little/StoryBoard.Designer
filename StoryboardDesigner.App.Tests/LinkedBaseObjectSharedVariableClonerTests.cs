using System.Text.Json;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class LinkedBaseObjectSharedVariableClonerTests
{
    [Fact]
    public void PreserveAuthoredMemberships_LocalRootAndChildGroupGetsFreshIdPerPlacement()
    {
        var fixture = CreateFixture(includeMixedGroup: false);
        var existingDerivative = CloneForPlacement(fixture.BaseObject);
        foreach (var (sourceVariable, derivativeVariable) in PairVariables(fixture.BaseObject, existingDerivative))
        {
            derivativeVariable.SharedVariableId = sourceVariable.SharedVariableId;
        }
        fixture.Room.GameObjects.Add(existingDerivative);
        var first = CloneForPlacement(fixture.BaseObject);
        var second = CloneForPlacement(fixture.BaseObject);

        LinkedBaseObjectSharedVariableCloner.PreserveAuthoredMemberships(fixture.Project, fixture.BaseObject, first);
        LinkedBaseObjectSharedVariableCloner.PreserveAuthoredMemberships(fixture.Project, fixture.BaseObject, second);

        var baseShareId = fixture.BaseLocalVariable.SharedVariableId!.Value;
        var firstShareId = GetLocalGroupShareId(first, fixture.BaseLocalVariable);
        var secondShareId = GetLocalGroupShareId(second, fixture.BaseLocalVariable);

        Assert.NotEqual(baseShareId, firstShareId);
        Assert.NotEqual(baseShareId, secondShareId);
        Assert.NotEqual(firstShareId, secondShareId);
        fixture.Room.GameObjects.Add(first);
        fixture.Room.GameObjects.Add(second);
        SharedVariableReconciliationService.ReconcileInPlace(fixture.Project);
        Assert.Contains(fixture.Project.SharedVariables.Single(shared => shared.Id == firstShareId).Participants, participant => participant.OwnerId == first.ObjectId);
        Assert.Contains(fixture.Project.SharedVariables.Single(shared => shared.Id == secondShareId).Participants, participant => participant.OwnerId == second.ObjectId);
        Assert.NotEqual(fixture.BaseObject.Variables[0].Id, first.Variables[0].Id);
        Assert.NotEqual(fixture.BaseObject.ContainedObjects[0].Variables[0].Id, first.ContainedObjects[0].Variables[0].Id);
        Assert.NotEqual(fixture.BaseObject.ContainedObjects[1].Variables[0].Id, first.ContainedObjects[1].Variables[0].Id);
        Assert.NotEqual(first.Variables[0].Id, second.Variables[0].Id);
        Assert.NotEqual(first.ContainedObjects[0].Variables[0].Id, second.ContainedObjects[0].Variables[0].Id);
        Assert.NotEqual(first.ContainedObjects[1].Variables[0].Id, second.ContainedObjects[1].Variables[0].Id);
    }

    [Fact]
    public void PreserveAuthoredMemberships_MixedRoomGroupRetainsExistingShareId()
    {
        var fixture = CreateFixture(includeMixedGroup: true);
        var placement = CloneForPlacement(fixture.BaseObject);

        LinkedBaseObjectSharedVariableCloner.PreserveAuthoredMemberships(fixture.Project, fixture.BaseObject, placement);

        var mixed = fixture.BaseObject.Variables.Single(variable => variable.Name == "ExternallyShared");
        Assert.Equal(fixture.MixedShareId, GetLocalGroupShareId(placement, mixed));
    }

    [Fact]
    public void PreserveAuthoredMemberships_PlacementPropertyEditsDoNotChangeBaseOrAnotherPlacement()
    {
        var fixture = CreateFixture(includeMixedGroup: false);
        var first = CloneForPlacement(fixture.BaseObject);
        var second = CloneForPlacement(fixture.BaseObject);
        LinkedBaseObjectSharedVariableCloner.PreserveAuthoredMemberships(fixture.Project, fixture.BaseObject, first);
        LinkedBaseObjectSharedVariableCloner.PreserveAuthoredMemberships(fixture.Project, fixture.BaseObject, second);
        first.Variables.Single(variable => variable.Name == fixture.BaseLocalVariable.Name).DefaultValue = "edited placement";

        Assert.Equal("base value", fixture.BaseLocalVariable.DefaultValue);
        Assert.Equal(GamePropertyLifetime.Singleton, fixture.BaseLocalVariable.Lifetime);
        Assert.Equal(GamePropertyValueRestriction.TrueFalse, fixture.BaseLocalVariable.ValueRestriction);
        Assert.Equal("base value", second.Variables.Single(variable => variable.Name == fixture.BaseLocalVariable.Name).DefaultValue);
    }

    [Fact]
    public void SaveReopenAndRuntimeExport_PreserveShareIdsAndParticipants()
    {
        var fixture = CreateFixture(includeMixedGroup: true);
        var first = CloneForPlacement(fixture.BaseObject);
        var second = CloneForPlacement(fixture.BaseObject);
        first.Name = "Elevator 1";
        second.Name = "Elevator 2";
        LinkedBaseObjectSharedVariableCloner.PreserveAuthoredMemberships(fixture.Project, fixture.BaseObject, first);
        LinkedBaseObjectSharedVariableCloner.PreserveAuthoredMemberships(fixture.Project, fixture.BaseObject, second);
        first.Variables.Single(variable => variable.Name == fixture.BaseLocalVariable.Name).DefaultValue = "true";
        second.Variables.Single(variable => variable.Name == fixture.BaseLocalVariable.Name).DefaultValue = "false";
        fixture.Room.GameObjects.Add(first);
        fixture.Room.GameObjects.Add(second);
        ScopeHierarchy.AttachParents(fixture.Project);

        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var projectPath = Path.Combine(tempRoot, "SharedPlacement", "SharedPlacement.sbr");
            var service = new JsonExportService();
            service.SaveProjectModel(projectPath, fixture.Project);

            var reopened = Assert.IsType<ProjectModel>(service.TryLoadProjectModel(projectPath));
            var loadedArea = reopened.Planets.Single().Countries.Single().Areas.Single();
            var loadedBase = loadedArea.BaseObjects.Single();
            var loadedRoom = loadedArea.Rooms.Single();
            var loadedFirst = loadedRoom.GameObjects.Single(gameObject => gameObject.Name == "Elevator 1");
            var loadedSecond = loadedRoom.GameObjects.Single(gameObject => gameObject.Name == "Elevator 2");
            var loadedLocalBaseShareId = loadedBase.Variables.Single(variable => variable.Name == "LocalShared").SharedVariableId;
            var loadedBaseLocal = loadedBase.Variables.Single(variable => variable.Name == "LocalShared");
            var loadedFirstLocalShareId = GetLocalGroupShareId(loadedFirst, loadedBaseLocal);
            var loadedSecondLocalShareId = GetLocalGroupShareId(loadedSecond, loadedBaseLocal);
            Assert.Equal("true", loadedFirst.Variables.Single(variable => variable.Name == "LocalShared").DefaultValue);
            Assert.Equal("false", loadedSecond.Variables.Single(variable => variable.Name == "LocalShared").DefaultValue);

            Assert.NotEqual(loadedLocalBaseShareId, loadedFirstLocalShareId);
            Assert.NotEqual(loadedLocalBaseShareId, loadedSecondLocalShareId);
            Assert.NotEqual(loadedFirstLocalShareId, loadedSecondLocalShareId);
            var loadedMixed = loadedBase.Variables.Single(variable => variable.Name == "ExternallyShared");
            Assert.Equal(fixture.MixedShareId, GetLocalGroupShareId(loadedFirst, loadedMixed));
            Assert.Equal(fixture.MixedShareId, GetLocalGroupShareId(loadedSecond, loadedMixed));

            var localSharedDefinitions = reopened.SharedVariables
                .Where(shared => shared.Id == loadedFirstLocalShareId || shared.Id == loadedSecondLocalShareId)
                .ToDictionary(shared => shared.Id);
            Assert.Contains(localSharedDefinitions[loadedFirstLocalShareId!.Value].Participants, participant => participant.OwnerId == loadedFirst.ObjectId);
            Assert.Contains(localSharedDefinitions[loadedSecondLocalShareId!.Value].Participants, participant => participant.OwnerId == loadedSecond.ObjectId);
            Assert.Contains(reopened.SharedVariables.Single(shared => shared.Id == fixture.MixedShareId).Participants,
                participant => participant.OwnerId == loadedFirst.ObjectId && participant.VariableName == "ExternallyShared");

            service.ExportCleanProjectV1(projectPath, reopened);
            AssertRuntimePropertyShareId(projectPath, fixture.BaseObject.ObjectId, "LocalShared", loadedLocalBaseShareId);
            AssertRuntimePropertyShareId(projectPath, loadedFirst.ObjectId, "LocalShared", loadedFirstLocalShareId);
            AssertRuntimePropertyShareId(projectPath, loadedSecond.ObjectId, "LocalShared", loadedSecondLocalShareId);
            AssertRuntimePropertyShareId(projectPath, loadedFirst.ObjectId, "ExternallyShared", fixture.MixedShareId);
            AssertRuntimePropertyShareId(projectPath, loadedSecond.ObjectId, "ExternallyShared", fixture.MixedShareId);
            AssertRuntimePropertyValue(projectPath, loadedFirst.ObjectId, "LocalShared", "true");
            AssertRuntimePropertyValue(projectPath, loadedSecond.ObjectId, "LocalShared", "false");
            AssertRuntimePropertyDefinition(projectPath, loadedFirst.ObjectId, "LocalShared", "LocalShared", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse);
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
    public void SaveReopen_StoresPlacementOwnedFieldsAndResolvesSettingsFromBase()
    {
        var fixture = CreateFixture(includeMixedGroup: false);
        fixture.BaseObject.FootprintWidthCells = 3;
        fixture.BaseObject.HideContainedObjects = false;
        fixture.BaseObject.IsContainer = true;
        fixture.BaseObject.ProducerNotes = "Base-owned production notes";
        var instance = CloneForPlacement(fixture.BaseObject);
        instance.Name = "Elevator in Foyer";
        instance.ProducerNotes = "Stale copied production notes";
        instance.PositionX = 42;
        instance.PositionY = 18;
        instance.ImageRotationDegrees = 90;
        instance.RenderZOrder = 7;
        instance.FootprintWidthCells = 9;
        instance.HideContainedObjects = true;
        instance.InstanceOverrides[EffectiveObjectFieldResolver.DescriptionField] = "Placed instance note";
        LinkedBaseObjectSharedVariableCloner.PreserveAuthoredMemberships(fixture.Project, fixture.BaseObject, instance);
        fixture.Room.GameObjects.Add(instance);
        ScopeHierarchy.AttachParents(fixture.Project);

        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var projectPath = Path.Combine(tempRoot, "LinkedSettings", "LinkedSettings.sbr");
            var service = new JsonExportService();
            service.SaveProjectModel(projectPath, fixture.Project);
            var reopened = Assert.IsType<ProjectModel>(service.TryLoadProjectModel(projectPath));
            var loadedArea = reopened.Planets.Single().Countries.Single().Areas.Single();
            var loadedBase = loadedArea.BaseObjects.Single();
            var loadedInstance = loadedArea.Rooms.Single().GameObjects.Single();

            Assert.Equal(42, loadedInstance.PositionX);
            Assert.Equal(18, loadedInstance.PositionY);
            Assert.Equal(90, loadedInstance.ImageRotationDegrees);
            Assert.Equal(7, loadedInstance.RenderZOrder);
            Assert.Equal(string.Empty, loadedInstance.ProducerNotes);
            Assert.Equal("Base-owned production notes", loadedBase.ProducerNotes);
            Assert.Equal("Placed instance note", loadedInstance.InstanceOverrides[EffectiveObjectFieldResolver.DescriptionField]);
            Assert.Equal(1, loadedInstance.FootprintWidthCells);
            Assert.Empty(loadedInstance.ImageVariants);
            Assert.Same(loadedBase, loadedInstance.ResolveEffectiveDefinitionOwnedSource(id => id == loadedBase.ObjectId ? loadedBase : null));
            Assert.Equal(3, loadedInstance.ResolveEffectiveDefinitionOwnedSource(id => id == loadedBase.ObjectId ? loadedBase : null).FootprintWidthCells);
            Assert.False(loadedInstance.ResolveEffectiveDefinitionOwnedSource(id => id == loadedBase.ObjectId ? loadedBase : null).HideContainedObjects);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static Fixture CreateFixture(bool includeMixedGroup)
    {
        var localShareId = Guid.NewGuid();
        var mixedShareId = Guid.NewGuid();
        var rootVariable = NewVariable("LocalShared", "base value", localShareId);
        var leftVariable = NewVariable("isHidden", "false", localShareId);
        var rightVariable = NewVariable("isHidden", "false", localShareId);
        var baseObject = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Elevator",
            Variables = new List<GamePropertyDefinition> { rootVariable },
            ContainedObjects = new List<GameObject>
            {
                new()
                {
                    ObjectId = Guid.NewGuid(),
                    Name = "Left Door",
                    Variables = new List<GamePropertyDefinition> { leftVariable }
                },
                new()
                {
                    ObjectId = Guid.NewGuid(),
                    Name = "Right Door",
                    Variables = new List<GamePropertyDefinition> { rightVariable }
                }
            }
        };

        GamePropertyDefinition? mixedVariable = null;
        var room = new Room { Name = "Foyer" };
        if (includeMixedGroup)
        {
            mixedVariable = NewVariable("ExternallyShared", "closed", mixedShareId);
            baseObject.Variables.Add(mixedVariable);
            room.Variables.Add(NewVariable("FoyerState", "closed", mixedShareId));
        }

        var area = new Area { Name = "Lobby", Rooms = new List<Room> { room }, BaseObjects = new List<GameObject> { baseObject } };
        var country = new Country { Name = "Country", Areas = new List<Area> { area } };
        var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
        var project = new ProjectModel { Name = "SharedPlacement", Planets = new List<Planet> { planet } };
        if (includeMixedGroup)
        {
            project.SharedVariables.Add(new SharedVariableDefinition
            {
                Id = mixedShareId,
                Name = "Foyer Shared State",
                DefaultValue = "closed",
                Participants = new List<SharedVariableParticipant>
                {
                    Participant("object", baseObject.ObjectId, "ExternallyShared"),
                    Participant("room", room.Id, "FoyerState")
                }
            });
        }

        project.SharedVariables.Add(new SharedVariableDefinition
        {
            Id = localShareId,
            Name = "Elevator State",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            Participants = new List<SharedVariableParticipant>
            {
                Participant("object", baseObject.ObjectId, "LocalShared"),
                Participant("object", baseObject.ContainedObjects[0].ObjectId, "isHidden"),
                Participant("object", baseObject.ContainedObjects[1].ObjectId, "isHidden")
            }
        });
        ScopeHierarchy.AttachParents(project);

        return new Fixture(project, baseObject, room, rootVariable, mixedShareId);
    }

    private static GameObject CloneForPlacement(GameObject source)
    {
        return new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = source.Name + " placement",
            LinkedBaseObjectId = source.ObjectId,
            LinkActionsToBaseObject = true,
            Variables = source.Variables.Select(CloneVariable).ToList(),
            ContainedObjects = source.ContainedObjects.Select(CloneForPlacement).ToList()
        };
    }

    private static GamePropertyDefinition CloneVariable(GamePropertyDefinition source)
    {
        return new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = source.Name,
            DefaultValue = source.DefaultValue,
            Lifetime = source.Lifetime,
            ValueRestriction = source.ValueRestriction
        };
    }

    private static GamePropertyDefinition NewVariable(string name, string defaultValue, Guid sharedId)
    {
        return new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            DefaultValue = defaultValue,
            Lifetime = GamePropertyLifetime.Singleton,
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            SharedVariableId = sharedId
        };
    }

    private static SharedVariableParticipant Participant(string kind, Guid ownerId, string variableName)
    {
        return new SharedVariableParticipant
        {
            Kind = kind,
            OwnerId = ownerId,
            VariableName = variableName
        };
    }

    private static Guid? GetLocalGroupShareId(GameObject root, GamePropertyDefinition baseProperty)
    {
        return root.Variables.FirstOrDefault(variable => variable.Id == baseProperty.Id
            || string.Equals(variable.Name, baseProperty.Name, StringComparison.OrdinalIgnoreCase))?.SharedVariableId
            ?? baseProperty.SharedVariableId;
    }

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

    private static IEnumerable<(GamePropertyDefinition Source, GamePropertyDefinition Clone)> PairVariables(GameObject source, GameObject clone)
    {
        for (var index = 0; index < Math.Min(source.Variables.Count, clone.Variables.Count); index++)
        {
            yield return (source.Variables[index], clone.Variables[index]);
        }

        for (var index = 0; index < Math.Min(source.ContainedObjects.Count, clone.ContainedObjects.Count); index++)
        {
            foreach (var pair in PairVariables(source.ContainedObjects[index], clone.ContainedObjects[index]))
            {
                yield return pair;
            }
        }
    }

    private static void AssertRuntimePropertyShareId(string projectPath, Guid objectId, string propertyName, Guid? expectedShareId)
    {
        var objectPath = Path.Combine(
            Path.GetDirectoryName(projectPath)!,
            "GameRuntimeJson",
            "GameObject",
            objectId.ToString("D").ToUpperInvariant() + ".runtime.json");
        using var document = JsonDocument.Parse(File.ReadAllText(objectPath));
        var properties = document.RootElement.GetProperty("gameProperties").EnumerateArray();
        var property = properties.Single(element => element.GetProperty("name").GetString() == propertyName);
        Assert.Equal(expectedShareId?.ToString("D"), property.GetProperty("sharedVariableId").GetString());
    }

    private static void AssertRuntimePropertyValue(string projectPath, Guid objectId, string propertyName, string expectedValue)
    {
        var objectPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "GameRuntimeJson", "GameObject", objectId.ToString("D").ToUpperInvariant() + ".runtime.json");
        using var document = JsonDocument.Parse(File.ReadAllText(objectPath));
        var property = document.RootElement.GetProperty("gameProperties").EnumerateArray().Single(element => element.GetProperty("name").GetString() == propertyName);
        Assert.Equal(expectedValue, property.GetProperty("defaultValue").GetString());
    }

    private static void AssertRuntimePropertyDefinition(string projectPath, Guid objectId, string propertyName, string expectedName, GamePropertyLifetime expectedLifetime, GamePropertyValueRestriction expectedRestriction)
    {
        var objectPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "GameRuntimeJson", "GameObject", objectId.ToString("D").ToUpperInvariant() + ".runtime.json");
        using var document = JsonDocument.Parse(File.ReadAllText(objectPath));
        var property = document.RootElement.GetProperty("gameProperties").EnumerateArray().Single(element => element.GetProperty("name").GetString() == propertyName);
        Assert.Equal(expectedName, property.GetProperty("name").GetString());
        Assert.Equal(expectedLifetime.ToString(), property.GetProperty("lifetime").GetString());
        Assert.Equal(expectedRestriction.ToString(), property.GetProperty("valueRestriction").GetString());
    }

    private sealed record Fixture(
        ProjectModel Project,
        GameObject BaseObject,
        Room Room,
        GamePropertyDefinition BaseLocalVariable,
        Guid MixedShareId);
}
