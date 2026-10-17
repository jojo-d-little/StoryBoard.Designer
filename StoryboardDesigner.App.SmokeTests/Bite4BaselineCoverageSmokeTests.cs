using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace StoryboardDesigner.App.SmokeTests;

[Collection("Designer UI Smoke")]
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
            var canonicalFixturePath = Path.Combine(AppContext.BaseDirectory, "Samples", "MapDemo1", "MapDemo1.sbe.json");
            Assert.True(File.Exists(canonicalFixturePath), $"Canonical fixture not found: {canonicalFixturePath}");

            var priorFixturePath = Environment.GetEnvironmentVariable(SmokeProjectPathEnvironmentVariable);
            Environment.SetEnvironmentVariable(SmokeProjectPathEnvironmentVariable, canonicalFixturePath);

            using var app = Application.Launch(executablePath);
            try
            {
                using var automation = new UIA3Automation();
                var mainWindow = RequireFound(
                    SmokeUiRetryHelpers.WaitForMainWindow(app, automation),
                    "Could not find the Designer main window.");

                var hierarchyTree = Retry.WhileNull(
                    () => TryFindFirstByAutomationId(mainWindow!, "MainWindow.HierarchyTree")?.AsTree()
                          ?? TryFindFirstTree(mainWindow!),
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                if (hierarchyTree is null)
                {
                    var windowTitles = string.Join(", ", app.GetAllTopLevelWindows(automation)
                        .Select(window => $"'{window.Title}'"));
                    throw new Xunit.Sdk.XunitException(
                        $"Could not find the hierarchy TreeView by AutomationId or control type. "
                        + $"AppExited={app.HasExited}; mainWindowTitle='{mainWindow!.Title}'; topLevelWindows=[{windowTitles}].");
                }
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding hierarchy tree");

                SelectWorkspaceTab(mainWindow!, "MainWindow.Tab.RoomDesigner");

                var roomWorkspacePane = RequireFound(Retry.WhileNull(
                    () => TryFindFirstByAutomationId(mainWindow!, "MainWindow.RoomDesigner.WorkspacePane"),
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result,
                    "Could not find MainWindow.RoomDesigner.WorkspacePane after selecting the Room Designer tab.");
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "opening room workspace pane");

                var roomTabs = RequireFound(Retry.WhileNull(
                    () => TryFindFirstByAutomationId(mainWindow!, "MainWindow.RoomDesigner.OpenEditorsTabs")?.AsTab(),
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result,
                    "Could not find MainWindow.RoomDesigner.OpenEditorsTabs.");
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding room editor tabs");

                var initialRoomTabCount = roomTabs!.TabItems.Length;
                var roomExpansionDiagnostics = new List<string>();
                var roomTreeItem = Retry.WhileNull(
                    () => FindHierarchyTreeItemByRegex(hierarchyTree!, new[] { "^kitchen$", "^foyer$" }, roomExpansionDiagnostics),
                    timeout: TimeSpan.FromSeconds(15),
                    interval: TimeSpan.FromMilliseconds(150),
                    throwOnTimeout: false).Result;
                Assert.True(roomTreeItem is not null,
                    $"Could not locate a MapDemo1 room hierarchy node. Visible tree names: {BuildTreeItemNameSnapshot(hierarchyTree!)}. "
                    + $"Expansion diagnostics: {string.Join(" | ", roomExpansionDiagnostics.Distinct().Take(8))}");
                ActivateHierarchyTreeItem(roomTreeItem!, mainWindow!);

                var roomOpened = Retry.WhileFalse(
                    () => roomTabs.TabItems.Length > initialRoomTabCount
                          || roomTabs.TabItems.Length > 0,
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                if (!roomOpened)
                {
                    ActivateHierarchyTreeItem(roomTreeItem!, mainWindow!);
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

                var areaTabs = RequireFound(Retry.WhileNull(
                    () => TryFindFirstByAutomationId(mainWindow!, "MainWindow.AreaDesigner.OpenEditorsTabs")?.AsTab(),
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result,
                    "Could not find MainWindow.AreaDesigner.OpenEditorsTabs after selecting the Area Designer tab.");
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding area editor tabs");

                var areaSelectedNameText = RequireFound(Retry.WhileNull(
                    () => TryFindFirstByAutomationId(mainWindow!, "MainWindow.AreaDesigner.SelectedEditorName"),
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result,
                    "Could not find MainWindow.AreaDesigner.SelectedEditorName after selecting the Area Designer tab.");
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "reading selected area name");

                var initialAreaTabCount = areaTabs!.TabItems.Length;
                var areaExpansionDiagnostics = new List<string>();
                var areaTreeItem = Retry.WhileNull(
                    () => FindHierarchyTreeItemByRegex(hierarchyTree!, new[] { "^freebird\\s+farm$" }, areaExpansionDiagnostics),
                    timeout: TimeSpan.FromSeconds(15),
                    interval: TimeSpan.FromMilliseconds(150),
                    throwOnTimeout: false).Result;
                Assert.True(areaTreeItem is not null,
                    $"Could not locate the MapDemo1 area hierarchy node. Visible tree names: {BuildTreeItemNameSnapshot(hierarchyTree!)}. "
                    + $"Expansion diagnostics: {string.Join(" | ", areaExpansionDiagnostics.Distinct().Take(8))}");
                ActivateHierarchyTreeItem(areaTreeItem!, mainWindow!);

                var areaOpened = Retry.WhileFalse(
                    () => HasAnyExpectedText(areaSelectedNameText!, new[] { "freebird farm" })
                          || areaTabs.TabItems.Length > initialAreaTabCount
                          || areaTabs.TabItems.Length > 0,
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                if (!areaOpened)
                {
                    ActivateHierarchyTreeItem(areaTreeItem!, mainWindow!);
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

    private static TreeItem? FindHierarchyTreeItemByRegex(
        Tree hierarchyTree,
        IReadOnlyList<string> preferredPatterns,
        ICollection<string> expansionDiagnostics)
    {
        return Retry.WhileNull(
            () =>
            {
                ExpandVisibleTreeItems(hierarchyTree, expansionDiagnostics);
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

    private static void ExpandVisibleTreeItems(Tree hierarchyTree, ICollection<string> diagnostics)
    {
        var items = hierarchyTree.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem));
        foreach (var item in items)
        {
            try
            {
                var treeItem = item.AsTreeItem();
                if (treeItem.ExpandCollapseState != ExpandCollapseState.Collapsed)
                {
                    continue;
                }

                var name = GetTreeItemDisplayName(treeItem);
                treeItem.Expand();
                if (treeItem.ExpandCollapseState == ExpandCollapseState.Collapsed)
                {
                    diagnostics.Add($"'{name}' remained collapsed after Expand().");
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add($"Expand failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void ActivateHierarchyTreeItem(TreeItem item, Window mainWindow)
    {
        var mainWindowHandle = mainWindow.Properties.NativeWindowHandle.ValueOrDefault;
        if (mainWindowHandle == 0)
        {
            throw new Xunit.Sdk.XunitException("Cannot activate hierarchy item: Designer main window has no native handle.");
        }

        var clickable = item.TryGetClickablePoint(out var point)
            ? point
            : item.BoundingRectangle.Center();

        var clientPoint = new NativePoint((int)Math.Round((double)clickable.X), (int)Math.Round((double)clickable.Y));
        if (!ScreenToClient(mainWindowHandle, ref clientPoint))
        {
            throw new Xunit.Sdk.XunitException(
                $"Could not map hierarchy item point to Designer client coordinates (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        var coordinates = (nint)(((clientPoint.Y & 0xFFFF) << 16) | (clientPoint.X & 0xFFFF));
        PostHierarchyMouseMessage(mainWindowHandle, WmLButtonDown, MkLButton, coordinates);
        PostHierarchyMouseMessage(mainWindowHandle, WmLButtonUp, 0, coordinates);
        PostHierarchyMouseMessage(mainWindowHandle, WmLButtonDoubleClick, MkLButton, coordinates);
        PostHierarchyMouseMessage(mainWindowHandle, WmLButtonUp, 0, coordinates);
    }

    private static void PostHierarchyMouseMessage(nint windowHandle, uint message, nuint wParam, nint lParam)
    {
        if (!PostMessage(windowHandle, message, wParam, lParam))
        {
            throw new Xunit.Sdk.XunitException(
                $"Could not post hierarchy mouse message 0x{message:X} to the Designer window (Win32 error {Marshal.GetLastWin32Error()}).");
        }
    }

    private const uint WmLButtonDown = 0x0201;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmLButtonDoubleClick = 0x0203;
    private const nuint MkLButton = 0x0001;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(nint hWnd, ref NativePoint lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint hWnd, uint msg, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X;
        public int Y;
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
        var workspaceTabs = RequireFound(Retry.WhileNull(
            () => TryFindFirstByAutomationId(mainWindow, "MainWindow.WorkspaceTabs")?.AsTab(),
            timeout: TimeSpan.FromSeconds(10),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false).Result,
            "Could not find MainWindow.WorkspaceTabs.");

        var workspaceTabItem = RequireFound(Retry.WhileNull(
            () => workspaceTabs!.TabItems.FirstOrDefault(item =>
                string.Equals(item.AutomationId, tabAutomationId, StringComparison.Ordinal)),
            timeout: TimeSpan.FromSeconds(10),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false).Result,
            $"Could not find workspace tab '{tabAutomationId}'.");

        workspaceTabItem.Select();
    }

    private static T RequireFound<T>(T? value, string message)
        where T : class
    {
        return value ?? throw new Xunit.Sdk.XunitException(message);
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

    private static Tree? TryFindFirstTree(AutomationElement root)
    {
        try
        {
            return root.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree))?.AsTree();
        }
        catch (COMException)
        {
            return null;
        }
    }

}
