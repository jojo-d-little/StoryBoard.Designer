using System.Text.Json;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class StructuralPartRelativeRenderOrderTests
{
    [Fact]
    public void EditingFrontToBackList_StoresSignedRankRelativeToParentPlaceholder()
    {
        var front = Guid.NewGuid();
        var behind = Guid.NewGuid();
        var list = new[]
        {
            new StructuralPartRenderOrderEntry(front, "Front", false, null),
            new StructuralPartRenderOrderEntry(null, "Elevator (parent)", true, 0),
            new StructuralPartRenderOrderEntry(behind, "Behind", false, null)
        };

        var ranks = StructuralPartRelativeRenderOrderPolicy.AssignRelativeRanks(list);

        Assert.Equal(1, ranks[front]);
        Assert.Equal(-1, ranks[behind]);
    }

    [Fact]
    public void EditingBaseObjectSettings_AppliesChildFamilyRanks()
    {
        var front = BuildStructuralChildModel("Front", StructuralPartPlacementKind.ParentRelative);
        var behind = BuildStructuralChildModel("Behind", StructuralPartPlacementKind.ParentRelative);
        var parent = new GameObject { Name = "Elevator", ContainedObjects = [front, behind] };
        var project = new ProjectModel { Name = "Render Order Edit", ObjectTemplates = [parent] };
        ScopeHierarchy.AttachParents(project);
        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initial => initial with
            {
                StructuralPartPlacements = initial.StructuralPartPlacements!
                    .Select(item => item with
                    {
                        StructuralPartRelativeRenderOrder = item.ObjectId == front.ObjectId ? 1 : -1
                    })
                    .ToList()
            }
        };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());
        var rootNode = new ProjectRootNodeViewModel(project);
        var templatesNode = new ObjectTemplatesNodeViewModel(project, rootNode);
        var parentNode = new TemplateGameObjectNodeViewModel(parent, templatesNode);

        Assert.True(viewModel.EditObjectBasicProperties(parentNode));
        Assert.Equal(1, front.StructuralPartRelativeRenderOrder);
        Assert.Equal(-1, behind.StructuralPartRelativeRenderOrder);
    }

    [Fact]
    public void BuildFrontToBackList_UsesLegacyOrderWhenNoFamilyOrderIsAuthored()
    {
        var first = BuildStructuralChildItem("First", StructuralPartPlacementKind.ParentRelative);
        var second = BuildStructuralChildItem("Second", StructuralPartPlacementKind.ParentRelative);
        var roomCoordinate = BuildStructuralChildItem("Fixed", StructuralPartPlacementKind.RoomCoordinates);
        var entries = StructuralPartRelativeRenderOrderPolicy.BuildFrontToBack(
            "Elevator",
            [first, second, roomCoordinate]);

        Assert.Equal(new[] { second.Name, first.Name, "Elevator (parent)" }, entries.Select(static item => item.DisplayName));
        Assert.All(entries.Where(static item => !item.IsParent), item => Assert.Null(item.RelativeOrder));
    }

    [Fact]
    public void PreviewOrdering_MatchesLegacyAndExplicitChildRanks()
    {
        Assert.True(StructuralPartRelativeRenderOrderPolicy.RendersInFrontOfParent(null));
        Assert.True(StructuralPartRelativeRenderOrderPolicy.RendersInFrontOfParent(1));
        Assert.False(StructuralPartRelativeRenderOrderPolicy.RendersInFrontOfParent(-1));
    }

    [Fact]
    public void SaveAndReopen_PreservesDefinitionOwnedStructuralPartRelativeRenderOrder()
    {
        var floor = BuildStructuralChildModel("TheFloor", StructuralPartPlacementKind.ParentRelative);
        floor.StructuralPartRelativeRenderOrder = -2;
        var elevator = new GameObject { Name = "Elevator", ContainedObjects = [floor] };
        var project = BuildProject(elevator, new Room { Name = "Foyer" });
        var tempRoot = CreateTempRoot();

        try
        {
            var projectPath = Path.Combine(tempRoot, "RenderOrder.sbe.json");
            var service = new JsonExportService();
            service.SaveProjectModel(projectPath, project);

            var reopened = Assert.IsType<ProjectModel>(service.TryLoadProjectModel(projectPath));
            var loadedFloor = Assert.Single(Assert.Single(reopened.Planets).Countries.Single().Areas.Single().BaseObjects.Single().ContainedObjects);
            Assert.Equal(-2, loadedFloor.StructuralPartRelativeRenderOrder);

            service.ExportCleanProjectV1(projectPath, reopened);
            var runtimeFile = Path.Combine(tempRoot, "GameRuntimeJson", "GameObject", elevator.ObjectId.ToString("D").ToUpperInvariant() + ".runtime.json");
            using var runtime = JsonDocument.Parse(File.ReadAllText(runtimeFile));
            Assert.Equal(-2, runtime.RootElement.GetProperty("containedObjects")[0]
                .GetProperty("structuralPartRelativeRenderOrder").GetInt32());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void RuntimeExport_UsesBaseDefinitionOrderAndDoesNotPersistLinkedOverride()
    {
        var floorDefinition = BuildStructuralChildModel("TheFloor", StructuralPartPlacementKind.ParentRelative);
        floorDefinition.StructuralPartRelativeRenderOrder = -2;
        var elevatorDefinition = new GameObject { Name = "Elevator", ContainedObjects = [floorDefinition] };
        var floorInstance = BuildStructuralChildModel("TheFloor", StructuralPartPlacementKind.ParentRelative);
        floorInstance.LinkedBaseObjectId = floorDefinition.ObjectId;
        floorInstance.StructuralPartRelativeRenderOrder = 3;
        var elevatorInstance = new GameObject
        {
            Name = "Elevator",
            LinkedBaseObjectId = elevatorDefinition.ObjectId,
            LinkActionsToBaseObject = true,
            ContainedObjects = [floorInstance]
        };
        var room = new Room { Name = "Foyer", GameObjects = [elevatorInstance] };
        var area = new Area { Name = "Area", BaseObjects = [elevatorDefinition], Rooms = [room] };
        var project = BuildProject(area);
        var tempRoot = CreateTempRoot();

        try
        {
            var projectPath = Path.Combine(tempRoot, "LinkedRenderOrder.sbe.json");
            var service = new JsonExportService();
            service.SaveProjectModel(projectPath, project);

            var authoredInstancePath = Directory.GetFiles(tempRoot, $"{elevatorInstance.ObjectId:N}.object.json", SearchOption.AllDirectories).Single();
            using (var authored = JsonDocument.Parse(File.ReadAllText(authoredInstancePath)))
            {
                Assert.False(authored.RootElement.GetProperty("containedObjects")[0]
                    .TryGetProperty("structuralPartRelativeRenderOrder", out _));
            }

            service.ExportCleanProjectV1(projectPath, project);
            var runtimeFile = Path.Combine(tempRoot, "GameRuntimeJson", "GameObject", elevatorInstance.ObjectId.ToString("D").ToUpperInvariant() + ".runtime.json");
            using var runtime = JsonDocument.Parse(File.ReadAllText(runtimeFile));
            Assert.Equal(-2, runtime.RootElement.GetProperty("containedObjects")[0]
                .GetProperty("structuralPartRelativeRenderOrder").GetInt32());
            Assert.Equal(-2, floorInstance.StructuralPartRelativeRenderOrder);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static StructuralPartPlacementEditItem BuildStructuralChildItem(string name, StructuralPartPlacementKind mode) =>
        new(Guid.NewGuid(), name, 1, 1, "N", new StructuralPartPlacement { Mode = mode });

    private static GameObject BuildStructuralChildModel(string name, StructuralPartPlacementKind mode) =>
        new()
        {
            Name = name,
            IsPartOfParent = true,
            Placement = new StructuralPartPlacement
            {
                Mode = mode,
                ParentRelativeOffsetX = 0,
                ParentRelativeOffsetY = 0,
                RoomCoordinateX = 0,
                RoomCoordinateY = 0
            }
        };

    private static ProjectModel BuildProject(GameObject baseObject, Room room)
    {
        var area = new Area { Name = "Area", BaseObjects = [baseObject], Rooms = [room] };
        return BuildProject(area);
    }

    private static ProjectModel BuildProject(Area area) => new()
    {
        Name = "Structural Part Render Order",
        Planets =
        [
            new Planet
            {
                Name = "Planet",
                Countries = [new Country { Name = "Country", Areas = [area] }]
            }
        ]
    };

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
