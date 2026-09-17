using System.Reflection;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelRoomSettingsTests
{
    [Fact]
    public void EditRoomSettings_RoomNode_UpdatesNameInGame()
    {
        var project = BuildProjectModel(out _);
        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditRoomSettingsHandler = initial => initial with { NameInGame = "Atrium" }
        };

        var vm = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(vm);

        var roomNode = EnumerateNodes(vm.HierarchyRoots)
            .OfType<RoomNodeViewModel>()
            .Single();

        var edited = vm.EditRoomSettings(roomNode);

        Assert.True(edited);
        Assert.Equal("Atrium", roomNode.Room.NameInGame);
    }

    [Fact]
    public void EditRoomSettings_TemplateRoomNode_UpdatesNameInGame()
    {
        var project = BuildProjectModel(out _);
        project.RoomTemplates.Add(new Room { Name = "Template Room" });
        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditRoomSettingsHandler = initial => initial with { NameInGame = "Template Alias" }
        };

        var vm = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(vm);

        var templateRoomNode = EnumerateNodes(vm.HierarchyRoots)
            .OfType<TemplateRoomNodeViewModel>()
            .Single(node => string.Equals(node.Room.Name, "Template Room", StringComparison.Ordinal));

        var edited = vm.EditRoomSettings(templateRoomNode);

        Assert.True(edited);
        Assert.Equal("Template Alias", templateRoomNode.Room.NameInGame);
    }

    private static ProjectModel BuildProjectModel(out Guid roomId)
    {
        var room = new Room { Name = "Room One" };
        roomId = room.Id;

        return new ProjectModel
        {
            Name = "Room Settings",
            Planets =
            [
                new Planet
                {
                    Name = "Planet",
                    Countries =
                    [
                        new Country
                        {
                            Name = "Country",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "Area",
                                    Rooms = [room]
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    private static void InvokeLoadProjectIntoHierarchy(MainWindowViewModel vm)
    {
        var method = typeof(MainWindowViewModel).GetMethod("LoadProjectIntoHierarchy", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(vm, null);
    }

    private static IEnumerable<HierarchyNodeViewModel> EnumerateNodes(IEnumerable<HierarchyNodeViewModel> roots)
    {
        var stack = new Stack<HierarchyNodeViewModel>(roots.Reverse());
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }
}
