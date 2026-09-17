using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;
using System.Reflection;

namespace StoryboardDesigner.App.Tests;

public sealed class ScopedVerbCatalogSyncTests
{
    [Fact]
    public void EditScopedVerbs_GlobalScope_RepeatedEditsKeepNodeBoundToModelList()
    {
        var project = new ProjectModel
        {
            Name = "Verb Sync Test",
            CommandVerbs = ["look"],
            Planets = [new Planet { Name = "Planet A" }]
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            EditScopedTokenListHandler = (_, tokenKind, values, _, _) =>
            {
                Assert.Equal("Verbs", tokenKind);
                var next = values.Count == 1 ? "inspect" : "nudge";
                values.Add(next);
                return true;
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(viewModel);

        var globalVerbsNode = EnumerateNodes(viewModel.HierarchyRoots)
            .OfType<ScopedVerbsNodeViewModel>()
            .Single(node => node.Scope == PropertyResolutionScope.Global);

        var firstHandled = viewModel.ExecuteTreeContextAction("edit-scoped-verbs", globalVerbsNode);
        var secondHandled = viewModel.ExecuteTreeContextAction("edit-scoped-verbs", globalVerbsNode);

        Assert.True(firstHandled);
        Assert.True(secondHandled);
        Assert.Same(project.CommandVerbs, globalVerbsNode.Verbs);
        Assert.Contains("inspect", project.CommandVerbs);
        Assert.Contains("nudge", project.CommandVerbs);
        Assert.Contains(globalVerbsNode.Children.OfType<ScopedVerbEntryNodeViewModel>(), child =>
            string.Equals(child.Verb, "inspect", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(globalVerbsNode.Children.OfType<ScopedVerbEntryNodeViewModel>(), child =>
            string.Equals(child.Verb, "nudge", StringComparison.OrdinalIgnoreCase));
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