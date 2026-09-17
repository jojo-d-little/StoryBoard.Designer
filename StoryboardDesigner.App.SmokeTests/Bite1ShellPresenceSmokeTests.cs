using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace StoryboardDesigner.App.SmokeTests;

public sealed class Bite1ShellPresenceSmokeTests
{
    [Fact]
    public void MainWindow_Shell_Anchors_Are_Discoverable_By_AutomationId()
    {
        StaTestRunner.Run(() =>
        {
            var executablePath = AppExecutableLocator.ResolveDesignerExecutablePath();

            using var app = Application.Launch(executablePath);
            try
            {
                using var automation = new UIA3Automation();

                var mainWindow = SmokeUiRetryHelpers.WaitForMainWindow(app, automation);

                Assert.NotNull(mainWindow);

                Assert.NotNull(FindElementByAutomationId(mainWindow!, "MainWindow.HierarchyTree"));
                Assert.NotNull(FindElementByAutomationId(mainWindow!, "MainWindow.WorkspaceTabs"));
                Assert.NotNull(FindElementByAutomationId(mainWindow!, "MainWindow.OutputConsoleList"));

                SmokeUiRetryHelpers.CloseAppWindowOrKill(
                    app,
                    mainWindow,
                    automation,
                    "Designer process did not exit after closing the main window.");
            }
            finally
            {
                if (!app.HasExited)
                {
                    app.Kill();
                }
            }
        });
    }

    private static AutomationElement? FindElementByAutomationId(Window mainWindow, string automationId)
    {
        return Retry.WhileNull(
            () => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            timeout: TimeSpan.FromSeconds(8),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false).Result;
    }
}
