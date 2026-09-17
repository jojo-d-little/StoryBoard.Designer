using System.IO;
using System.Reflection;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class PhaseNarrativeReviewContextActionTests
{
    [Fact]
    public void GetTreeContextActions_BookAndChapter_IncludeReviewNarrative()
    {
        var project = BuildProject();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());

        var hierarchy = BuildHierarchy(project);
        var bookNode = EnumerateNodes(hierarchy).OfType<PhaseNodeViewModel>().Single(node => node.PhaseNode.Tier == PhaseTier.Book);
        var chapterNode = EnumerateNodes(hierarchy).OfType<PhaseNodeViewModel>().Single(node => node.PhaseNode.Tier == PhaseTier.Chapter);
        var pageNode = EnumerateNodes(hierarchy).OfType<PhaseNodeViewModel>().Single(node => node.PhaseNode.Tier == PhaseTier.Page);

        Assert.Contains(viewModel.GetTreeContextActions(bookNode), action => action.ActionId == "review-phase-narrative");
        Assert.Contains(viewModel.GetTreeContextActions(chapterNode), action => action.ActionId == "review-phase-narrative");
        Assert.DoesNotContain(viewModel.GetTreeContextActions(pageNode), action => action.ActionId == "review-phase-narrative");
    }

    [Fact]
    public void ExecuteTreeContextAction_ReviewNarrative_Book_ExportsAndOpensBrowserAtAnchor()
    {
        var project = BuildProject();
        var projectUi = new ProjectUiServiceStub();
        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(project, new TreeContextInteractionServiceStub(), projectUi);

        var projectFilePath = Path.Combine(Path.GetTempPath(), "phase-review-tests", "NarrativeReview.sbe.json");
        SetProjectFilePath(bundle.ViewModel, projectFilePath);

        var hierarchy = BuildHierarchy(project);
        var bookNode = EnumerateNodes(hierarchy).OfType<PhaseNodeViewModel>().Single(node => node.PhaseNode.Tier == PhaseTier.Book);

        var handled = bundle.ViewModel.ExecuteTreeContextAction("review-phase-narrative", bookNode);

        Assert.True(handled);

        var expectedReviewPath = Path.Combine(Path.GetDirectoryName(projectFilePath) ?? string.Empty, "phase-narrative-review.html");
        var expectedUri = new Uri(expectedReviewPath).AbsoluteUri + $"#phase-{bookNode.PhaseNode.Id:N}";
        Assert.Equal(expectedUri, projectUi.LastOpenedUri);
    }

    [Fact]
    public void GetTreeContextActions_ChapterAndPage_IncludeMoveActionsWhenSiblingReorderIsPossible()
    {
        var project = BuildProjectWithTwoChaptersAndTwoPages();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());

        var hierarchy = BuildHierarchy(project);
        var chapters = EnumerateNodes(hierarchy)
            .OfType<PhaseNodeViewModel>()
            .Where(node => node.PhaseNode.Tier == PhaseTier.Chapter)
            .OrderBy(node => node.PhaseNode.PhaseKey, StringComparer.Ordinal)
            .ToList();
        var pages = EnumerateNodes(hierarchy)
            .OfType<PhaseNodeViewModel>()
            .Where(node => node.PhaseNode.Tier == PhaseTier.Page)
            .OrderBy(node => node.PhaseNode.PhaseKey, StringComparer.Ordinal)
            .ToList();

        var firstChapterActions = viewModel.GetTreeContextActions(chapters[0]);
        var secondChapterActions = viewModel.GetTreeContextActions(chapters[1]);
        var firstPageActions = viewModel.GetTreeContextActions(pages[0]);
        var secondPageActions = viewModel.GetTreeContextActions(pages[1]);

        Assert.Contains(firstChapterActions, action => action.ActionId == "move-phase-node-down");
        Assert.DoesNotContain(firstChapterActions, action => action.ActionId == "move-phase-node-up");
        Assert.Contains(secondChapterActions, action => action.ActionId == "move-phase-node-up");

        Assert.Contains(firstPageActions, action => action.ActionId == "move-phase-node-down");
        Assert.DoesNotContain(firstPageActions, action => action.ActionId == "move-phase-node-up");
        Assert.Contains(secondPageActions, action => action.ActionId == "move-phase-node-up");
    }

    [Fact]
    public void ExecuteTreeContextAction_MovePhaseNodeDown_ReordersChapterSiblings()
    {
        var project = BuildProjectWithTwoChaptersAndTwoPages();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());

        var hierarchy = BuildHierarchy(project);
        var firstChapterNode = EnumerateNodes(hierarchy)
            .OfType<PhaseNodeViewModel>()
            .Single(node => node.PhaseNode.Tier == PhaseTier.Chapter && node.PhaseNode.PhaseKey == "chapter-1");

        var handled = viewModel.ExecuteTreeContextAction("move-phase-node-down", firstChapterNode);

        Assert.True(handled);
        Assert.Equal("chapter-2", project.PhaseBooks[0].Children[0].PhaseKey);
        Assert.Equal("chapter-1", project.PhaseBooks[0].Children[1].PhaseKey);
    }

    [Fact]
    public void ExecuteTreeContextAction_MovePhaseNodeDown_ReordersPageSiblings()
    {
        var project = BuildProjectWithTwoChaptersAndTwoPages();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());

        var hierarchy = BuildHierarchy(project);
        var firstPageNode = EnumerateNodes(hierarchy)
            .OfType<PhaseNodeViewModel>()
            .Single(node => node.PhaseNode.Tier == PhaseTier.Page && node.PhaseNode.PhaseKey == "page-1");

        var handled = viewModel.ExecuteTreeContextAction("move-phase-node-down", firstPageNode);

        Assert.True(handled);
        Assert.Equal("page-2", project.PhaseBooks[0].Children[0].Children[0].PhaseKey);
        Assert.Equal("page-1", project.PhaseBooks[0].Children[0].Children[1].PhaseKey);
    }

    [Fact]
    public void ExecuteTreeContextAction_EditPhaseNode_PersistsTextCueEffectKeys()
    {
        var project = BuildProject();
        var treeContext = new TreeContextInteractionServiceStub
        {
            EditPhaseNodeHandler = initial => initial with
            {
                TitlePresentationCueEffectKey = "text.hudoverlay.fadein.auto.2500",
                ProloguePresentationCueEffectKey = "text.hudoverlay.manualscroll.manualdismiss",
                NarrativePresentationCueEffectKey = "text.narrativedialog.archive"
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var hierarchy = BuildHierarchy(project);
        var pageNode = EnumerateNodes(hierarchy)
            .OfType<PhaseNodeViewModel>()
            .Single(node => node.PhaseNode.Tier == PhaseTier.Page);

        var handled = viewModel.ExecuteTreeContextAction("edit-phase-node", pageNode);

        Assert.True(handled);
        Assert.Equal("text.hudoverlay.fadein.auto.2500", pageNode.PhaseNode.TitlePresentationCueEffectKey);
        Assert.Equal("text.hudoverlay.manualscroll.manualdismiss", pageNode.PhaseNode.ProloguePresentationCueEffectKey);
        Assert.Equal("text.narrativedialog.archive", pageNode.PhaseNode.NarrativePresentationCueEffectKey);
    }

    [Fact]
    public void GetTreeContextActions_Page_UsesSetStartingPhaseAction_WhenPageIsNotCurrentStart()
    {
        var project = BuildProject();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());

        var hierarchy = BuildHierarchy(project);
        var pageNode = EnumerateNodes(hierarchy)
            .OfType<PhaseNodeViewModel>()
            .Single(node => node.PhaseNode.Tier == PhaseTier.Page);

        var actions = viewModel.GetTreeContextActions(pageNode);

        Assert.Contains(actions, action => action.ActionId == "set-starting-phase-page");
        Assert.DoesNotContain(actions, action => action.ActionId == "clear-starting-phase-page");
    }

    [Fact]
    public void GetTreeContextActions_Page_UsesClearStartingPhaseAction_WhenPageIsCurrentStart()
    {
        var project = BuildProject();
        project.StartingPhasePageId = project.PhaseBooks[0].Children[0].Children[0].Id;
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());

        var hierarchy = BuildHierarchy(project);
        var pageNode = EnumerateNodes(hierarchy)
            .OfType<PhaseNodeViewModel>()
            .Single(node => node.PhaseNode.Tier == PhaseTier.Page);

        var actions = viewModel.GetTreeContextActions(pageNode);

        Assert.Contains(actions, action => action.ActionId == "clear-starting-phase-page");
        Assert.DoesNotContain(actions, action => action.ActionId == "set-starting-phase-page");
    }

    [Fact]
    public void ExecuteTreeContextAction_SetStartingPhasePage_UpdatesProjectAndActionState()
    {
        var project = BuildProject();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());

        var hierarchy = BuildHierarchy(project);
        var pageNode = EnumerateNodes(hierarchy)
            .OfType<PhaseNodeViewModel>()
            .Single(node => node.PhaseNode.Tier == PhaseTier.Page);

        var handled = viewModel.ExecuteTreeContextAction("set-starting-phase-page", pageNode);

        Assert.True(handled);
        Assert.Equal(pageNode.PhaseNode.Id, project.StartingPhasePageId);
        Assert.Equal("Updated starting phase page.", viewModel.ExportStatus);
    }

    [Fact]
    public void ExecuteTreeContextAction_ClearStartingPhasePage_ClearsOnlyWhenNodeMatchesCurrentStart()
    {
        var project = BuildProject();
        var pageId = project.PhaseBooks[0].Children[0].Children[0].Id;
        project.StartingPhasePageId = pageId;

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());

        var hierarchy = BuildHierarchy(project);
        var pageNode = EnumerateNodes(hierarchy)
            .OfType<PhaseNodeViewModel>()
            .Single(node => node.PhaseNode.Tier == PhaseTier.Page);

        var handled = viewModel.ExecuteTreeContextAction("clear-starting-phase-page", pageNode);

        Assert.True(handled);
        Assert.Null(project.StartingPhasePageId);
        Assert.Equal("Cleared starting phase page.", viewModel.ExportStatus);
    }

    private static IReadOnlyList<HierarchyNodeViewModel> BuildHierarchy(ProjectModel project)
    {
        var method = typeof(MainWindowViewModel).GetMethod("BuildHierarchy", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, [project]);
        return Assert.IsType<List<HierarchyNodeViewModel>>(result);
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

    private static void SetProjectFilePath(MainWindowViewModel vm, string projectFilePath)
    {
        var field = typeof(MainWindowViewModel).GetField("_projectFilePath", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(vm, projectFilePath);
    }

    private static ProjectModel BuildProject()
    {
        var page = new PhaseNode
        {
            Id = Guid.Parse("00000000-0000-0000-0000-0000000000A3"),
            Tier = PhaseTier.Page,
            PhaseKey = "page-1",
            DisplayName = "Page One"
        };

        var chapter = new PhaseNode
        {
            Id = Guid.Parse("00000000-0000-0000-0000-0000000000A2"),
            Tier = PhaseTier.Chapter,
            PhaseKey = "chapter-1",
            DisplayName = "Chapter One"
        };
        chapter.Children.Add(page);

        var book = new PhaseNode
        {
            Id = Guid.Parse("00000000-0000-0000-0000-0000000000A1"),
            Tier = PhaseTier.Book,
            PhaseKey = "book-1",
            DisplayName = "Book One"
        };
        book.Children.Add(chapter);

        return new ProjectModel
        {
            Name = "Phase Narrative Review",
            PhaseBooks = [book]
        };
    }

    private static ProjectModel BuildProjectWithTwoChaptersAndTwoPages()
    {
        var pageOne = new PhaseNode
        {
            Id = Guid.Parse("00000000-0000-0000-0000-0000000001A1"),
            Tier = PhaseTier.Page,
            PhaseKey = "page-1",
            DisplayName = "Page One"
        };

        var pageTwo = new PhaseNode
        {
            Id = Guid.Parse("00000000-0000-0000-0000-0000000001A2"),
            Tier = PhaseTier.Page,
            PhaseKey = "page-2",
            DisplayName = "Page Two"
        };

        var chapterOne = new PhaseNode
        {
            Id = Guid.Parse("00000000-0000-0000-0000-0000000001B1"),
            Tier = PhaseTier.Chapter,
            PhaseKey = "chapter-1",
            DisplayName = "Chapter One"
        };
        chapterOne.Children.Add(pageOne);
        chapterOne.Children.Add(pageTwo);

        var chapterTwo = new PhaseNode
        {
            Id = Guid.Parse("00000000-0000-0000-0000-0000000001B2"),
            Tier = PhaseTier.Chapter,
            PhaseKey = "chapter-2",
            DisplayName = "Chapter Two"
        };

        var book = new PhaseNode
        {
            Id = Guid.Parse("00000000-0000-0000-0000-0000000001C1"),
            Tier = PhaseTier.Book,
            PhaseKey = "book-1",
            DisplayName = "Book One"
        };
        book.Children.Add(chapterOne);
        book.Children.Add(chapterTwo);

        return new ProjectModel
        {
            Name = "Phase Reorder",
            PhaseBooks = [book]
        };
    }
}
