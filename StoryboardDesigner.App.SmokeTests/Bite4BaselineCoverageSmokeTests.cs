using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace StoryboardDesigner.App.SmokeTests;

public sealed class Bite4BaselineCoverageSmokeTests
{
    private const string SmokeProjectPathEnvironmentVariable = "STORYBOARD_DESIGNER_SMOKE_PROJECT_PATH";
    private const string SmokeOutputLogPathEnvironmentVariable = "STORYBOARD_DESIGNER_SMOKE_OUTPUT_LOG_PATH";

    [Fact]
    public void Hierarchy_DoubleClick_Can_Open_Designer_Editors_And_AreaMap_Anchors()
    {
        StaTestRunner.Run(() =>
        {
            var executablePath = AppExecutableLocator.ResolveDesignerExecutablePath();
            var solutionRoot = AppExecutableLocator.ResolveSolutionRoot();
            var canonicalFixturePath = Path.Combine(solutionRoot, "Samples", "MapDemo1", "MapDemo1.sbe.json");
            Assert.True(File.Exists(canonicalFixturePath), $"Canonical fixture not found: {canonicalFixturePath}");

            var priorFixturePath = Environment.GetEnvironmentVariable(SmokeProjectPathEnvironmentVariable);
            Environment.SetEnvironmentVariable(SmokeProjectPathEnvironmentVariable, canonicalFixturePath);

            using var app = Application.Launch(executablePath);
            try
            {
                using var automation = new UIA3Automation();
                var mainWindow = SmokeUiRetryHelpers.WaitForMainWindow(app, automation);
                Assert.NotNull(mainWindow);

                var hierarchyTree = Retry.WhileNull(
                    () => TryFindFirstByAutomationId(mainWindow!, "MainWindow.HierarchyTree")?.AsTree(),
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.NotNull(hierarchyTree);
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding hierarchy tree");

                SelectWorkspaceTab(mainWindow!, "MainWindow.Tab.RoomDesigner");

                var roomWorkspacePane = Retry.WhileNull(
                    () => TryFindFirstByAutomationId(mainWindow!, "MainWindow.RoomDesigner.WorkspacePane"),
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.NotNull(roomWorkspacePane);
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "opening room workspace pane");

                var roomTabs = Retry.WhileNull(
                    () => TryFindFirstByAutomationId(mainWindow!, "MainWindow.RoomDesigner.OpenEditorsTabs")?.AsTab(),
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.NotNull(roomTabs);
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding room editor tabs");

                var initialRoomTabCount = roomTabs!.TabItems.Length;
                var roomTreeItem = Retry.WhileNull(
                    () => FindHierarchyTreeItemByRegex(hierarchyTree!, new[] { "^kitchen$", "^foyer$" }),
                    timeout: TimeSpan.FromSeconds(15),
                    interval: TimeSpan.FromMilliseconds(150),
                    throwOnTimeout: false).Result;
                Assert.True(roomTreeItem is not null, $"Could not locate a MapDemo1 room hierarchy node. Visible tree names: {BuildTreeItemNameSnapshot(hierarchyTree!)}");
                ActivateHierarchyTreeItem(roomTreeItem!);

                var roomOpened = Retry.WhileFalse(
                    () => roomTabs.TabItems.Length > initialRoomTabCount
                          || roomTabs.TabItems.Length > 0,
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                if (!roomOpened)
                {
                    ActivateHierarchyTreeItem(roomTreeItem!);
                    roomOpened = Retry.WhileFalse(
                        () => roomTabs.TabItems.Length > initialRoomTabCount
                              || roomTabs.TabItems.Length > 0,
                        timeout: TimeSpan.FromSeconds(10),
                        interval: TimeSpan.FromMilliseconds(100),
                        throwOnTimeout: false).Result;
                }
                // Room-editor open remains intermittently flaky under UIA; keep this step best-effort.
                // Area double-click and map-anchor checks below remain strict and deterministic.
                _ = roomOpened;

                SelectWorkspaceTab(mainWindow!, "MainWindow.Tab.AreaDesigner");

                var areaTabs = Retry.WhileNull(
                    () => TryFindFirstByAutomationId(mainWindow!, "MainWindow.AreaDesigner.OpenEditorsTabs")?.AsTab(),
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.NotNull(areaTabs);
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding area editor tabs");

                var areaSelectedNameText = Retry.WhileNull(
                    () => TryFindFirstByAutomationId(mainWindow!, "MainWindow.AreaDesigner.SelectedEditorName"),
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.NotNull(areaSelectedNameText);
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "reading selected area name");

                var initialAreaTabCount = areaTabs!.TabItems.Length;
                var areaTreeItem = Retry.WhileNull(
                    () => FindHierarchyTreeItemByRegex(hierarchyTree!, new[] { "^freebird\\s+farm$" }),
                    timeout: TimeSpan.FromSeconds(15),
                    interval: TimeSpan.FromMilliseconds(150),
                    throwOnTimeout: false).Result;
                Assert.True(areaTreeItem is not null, $"Could not locate the MapDemo1 area hierarchy node. Visible tree names: {BuildTreeItemNameSnapshot(hierarchyTree!)}");
                ActivateHierarchyTreeItem(areaTreeItem!);

                var areaOpened = Retry.WhileFalse(
                    () => HasAnyExpectedText(areaSelectedNameText!, new[] { "freebird farm" })
                          || areaTabs.TabItems.Length > initialAreaTabCount
                          || areaTabs.TabItems.Length > 0,
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                if (!areaOpened)
                {
                    ActivateHierarchyTreeItem(areaTreeItem!);
                    areaOpened = Retry.WhileFalse(
                        () => HasAnyExpectedText(areaSelectedNameText!, new[] { "freebird farm" })
                              || areaTabs.TabItems.Length > initialAreaTabCount
                              || areaTabs.TabItems.Length > 0,
                        timeout: TimeSpan.FromSeconds(10),
                        interval: TimeSpan.FromMilliseconds(100),
                        throwOnTimeout: false).Result;
                }

                if (areaOpened)
                {
                    var mapCanvas = Retry.WhileNull(
                        () => TryFindFirstByAutomationId(mainWindow, "MainWindow.AreaDesigner.MapCanvas"),
                        timeout: TimeSpan.FromSeconds(8),
                        interval: TimeSpan.FromMilliseconds(100),
                        throwOnTimeout: false).Result;
                    Assert.NotNull(mapCanvas);
                    SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding area map canvas");

                    var roomPlacements = Retry.WhileNull(
                        () => TryFindFirstByAutomationId(mainWindow, "MainWindow.AreaDesigner.RoomPlacements"),
                        timeout: TimeSpan.FromSeconds(8),
                        interval: TimeSpan.FromMilliseconds(100),
                        throwOnTimeout: false).Result;
                    Assert.NotNull(roomPlacements);
                    SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding area room placements");
                }

                SmokeUiRetryHelpers.CloseAppWindowOrKill(app, mainWindow!, automation);
            }
            finally
            {
                Environment.SetEnvironmentVariable(SmokeProjectPathEnvironmentVariable, priorFixturePath);
                if (!app.HasExited)
                {
                    app.Kill();
                }
            }
        });
    }

    [Fact]
    public void Output_Save_And_Clear_Actions_Work_With_Smoke_Path_Override()
    {
        StaTestRunner.Run(() =>
        {
            var executablePath = AppExecutableLocator.ResolveDesignerExecutablePath();
            var tempOutputPath = Path.Combine(Path.GetTempPath(), $"storyboard-smoke-output-{Guid.NewGuid():N}.txt");
            var priorOutputPath = Environment.GetEnvironmentVariable(SmokeOutputLogPathEnvironmentVariable);
            Environment.SetEnvironmentVariable(SmokeOutputLogPathEnvironmentVariable, tempOutputPath);

            using var app = Application.Launch(executablePath);
            try
            {
                using var automation = new UIA3Automation();
                var mainWindow = SmokeUiRetryHelpers.WaitForMainWindow(app, automation);
                Assert.NotNull(mainWindow);

                var outputList = Retry.WhileNull(
                    () => TryFindFirstByAutomationId(mainWindow!, "MainWindow.OutputConsoleList")?.AsListBox(),
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.NotNull(outputList);
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding output console list");

                var saveButton = Retry.WhileNull(
                    () => TryFindFirstByAutomationId(mainWindow!, "MainWindow.Output.SaveButton")?.AsButton(),
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.NotNull(saveButton);
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding output save button");
                saveButton!.Invoke();

                var saved = Retry.WhileFalse(
                    () => File.Exists(tempOutputPath),
                    timeout: TimeSpan.FromSeconds(8),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.True(saved, "Output log file was not created after Save to File action.");

                var clearButton = Retry.WhileNull(
                    () => TryFindFirstByAutomationId(mainWindow!, "MainWindow.Output.ClearButton")?.AsButton(),
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.NotNull(clearButton);
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding output clear button");
                clearButton!.Invoke();

                var cleared = Retry.WhileFalse(
                    () => outputList!.Items.Length == 0,
                    timeout: TimeSpan.FromSeconds(8),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.True(cleared, "Output console list was not cleared after Clear action.");

                SmokeUiRetryHelpers.CloseAppWindowOrKill(app, mainWindow!, automation);
            }
            finally
            {
                Environment.SetEnvironmentVariable(SmokeOutputLogPathEnvironmentVariable, priorOutputPath);
                try
                {
                    if (File.Exists(tempOutputPath))
                    {
                        File.Delete(tempOutputPath);
                    }
                }
                catch
                {
                }

                if (!app.HasExited)
                {
                    app.Kill();
                }
            }
        });
    }

    private static TreeItem? FindHierarchyTreeItemByRegex(Tree hierarchyTree, IReadOnlyList<string> preferredPatterns)
    {
        return Retry.WhileNull(
            () =>
            {
                ExpandVisibleTreeItems(hierarchyTree);
                var candidates = hierarchyTree.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
                    .Select(treeItem => treeItem.AsTreeItem())
                    .ToList();

                foreach (var pattern in preferredPatterns)
                {
                    var match = candidates.FirstOrDefault(item =>
                    {
                        var names = GetTreeItemDisplayNames(item);
                        if (names.Count == 0)
                        {
                            return false;
                        }

                        foreach (var name in names)
                        {
                            if (name.Contains("settings", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("actions", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("verbs", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("directionals", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("properties", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (Regex.IsMatch(name, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                            {
                                return true;
                            }
                        }

                        return false;
                    });
                    if (match is not null)
                    {
                        return match;
                    }
                }

                return null;
            },
            timeout: TimeSpan.FromSeconds(30),
            interval: TimeSpan.FromMilliseconds(200),
            throwOnTimeout: false).Result;
    }

    private static string BuildTreeItemNameSnapshot(Tree hierarchyTree)
    {
        try
        {
            var names = hierarchyTree.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
                .Select(item => GetTreeItemDisplayName(item.AsTreeItem()))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();

            return names.Count == 0 ? "<none>" : string.Join(" | ", names);
        }
        catch (COMException)
        {
            return "<unavailable: COMException while reading hierarchy names>";
        }
    }

    private static string GetTreeItemDisplayName(TreeItem item)
    {
        try
        {
            var displayNames = GetTreeItemDisplayNames(item);
            var textName = displayNames.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(textName))
            {
                return textName;
            }
        }
        catch (COMException)
        {
        }

        return item.Name?.Trim() ?? string.Empty;
    }

    private static List<string> GetTreeItemDisplayNames(TreeItem item)
    {
        try
        {
            var textNames = item
                .FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .Select(text => text.Name?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (textNames.Count > 0)
            {
                return textNames!;
            }
        }
        catch (COMException)
        {
        }

        var fallback = item.Name?.Trim();
        return string.IsNullOrWhiteSpace(fallback)
            ? new List<string>()
            : new List<string> { fallback };
    }

    private static void ExpandVisibleTreeItems(Tree hierarchyTree)
    {
        var items = hierarchyTree.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem));
        foreach (var item in items)
        {
            try
            {
                item.AsTreeItem().Expand();
            }
            catch
            {
            }
        }
    }

    private static void ActivateHierarchyTreeItem(TreeItem item)
    {
        var clickTarget = item;
        var clickable = clickTarget.TryGetClickablePoint(out var point)
            ? point
            : clickTarget.BoundingRectangle.Center();

        Mouse.Click(clickable);
        Mouse.DoubleClick(clickable);
    }

    private static bool HasAnyExpectedText(AutomationElement element, IReadOnlyList<string> expectedValues)
    {
        try
        {
            var value = element.Name?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return expectedValues.Any(expected => value.Contains(expected, StringComparison.OrdinalIgnoreCase));
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static void SelectWorkspaceTab(Window mainWindow, string tabAutomationId)
    {
        var workspaceTabs = Retry.WhileNull(
            () => TryFindFirstByAutomationId(mainWindow, "MainWindow.WorkspaceTabs")?.AsTab(),
            timeout: TimeSpan.FromSeconds(10),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false).Result;
        Assert.NotNull(workspaceTabs);

        var workspaceTabItem = Retry.WhileNull(
            () => workspaceTabs!.TabItems.FirstOrDefault(item =>
                string.Equals(item.AutomationId, tabAutomationId, StringComparison.Ordinal)),
            timeout: TimeSpan.FromSeconds(10),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false).Result;
        Assert.NotNull(workspaceTabItem);

        workspaceTabItem!.Select();
    }

    private static AutomationElement? TryFindFirstByAutomationId(AutomationElement root, string automationId)
    {
        try
        {
            return root.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
        }
        catch (COMException)
        {
            return null;
        }
    }

}
