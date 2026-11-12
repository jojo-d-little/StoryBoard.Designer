using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace StoryboardDesigner.App.SmokeTests;

[Collection("Designer UI Smoke")]
public sealed class Bite2GameSimulatorTabInteractionSmokeTests
{
    [Fact]
    public void Only_DevelopmentWebPortal_Launch_And_Setup_Are_Reachable()
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
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "opening development launch smoke shell");

                Assert.Null(mainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.Menu.RunSimulator")));

                var runDevelopmentMenuItem = Retry.WhileNull(
                    () => mainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.Menu.RunDevelopmentWebPortal")),
                    timeout: TimeSpan.FromSeconds(8),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.NotNull(runDevelopmentMenuItem);
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding run development WebPortal menu item");

                var toolsMenuItem = Retry.WhileNull(
                    () => mainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.Menu.Tools"))?.AsMenuItem(),
                    timeout: TimeSpan.FromSeconds(8),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.NotNull(toolsMenuItem);
                toolsMenuItem!.Expand();

                var desktop = automation.GetDesktop();

                var developmentSetupMenuItem = Retry.WhileNull(
                    () =>
                    {
                        try
                        {
                            toolsMenuItem.Expand();
                        }
                        catch
                        {
                            // Ignore transient UIA expand failures and continue probing.
                        }

                        return mainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.Menu.Tools.DevelopmentWebPortalSetup"))
                            ?? desktop.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.Menu.Tools.DevelopmentWebPortalSetup"))
                            ?? mainWindow.FindFirstDescendant(cf => cf.ByName("Development WebPortal Setup"))
                            ?? desktop.FindFirstDescendant(cf => cf.ByName("Development WebPortal Setup"));
                    },
                    timeout: TimeSpan.FromSeconds(8),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;

                Assert.NotNull(developmentSetupMenuItem);
                Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.Menu.Tools.SimulatorSetup")));
                Assert.Null(desktop.FindFirstDescendant(cf => cf.ByName("Simulator Setup")));
                developmentSetupMenuItem!.AsMenuItem().Invoke();
                var setupDialog = Retry.WhileNull(
                    () => mainWindow.ModalWindows.Concat(app.GetAllTopLevelWindows(automation)).FirstOrDefault(w => w.Title == "Development WebPortal Setup"),
                    timeout: TimeSpan.FromSeconds(8),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.NotNull(setupDialog);
                Assert.NotNull(setupDialog!.FindFirstDescendant(cf => cf.ByAutomationId("DevelopmentLaunchSetup.GameHostExecutablePath")));
                Assert.NotNull(setupDialog.FindFirstDescendant(cf => cf.ByAutomationId("DevelopmentLaunchSetup.WebPortalRootPath")));
                setupDialog.FindFirstDescendant(cf => cf.ByName("Cancel"))!.AsButton().Invoke();
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding development WebPortal setup menu item");

                SmokeUiRetryHelpers.CloseAppWindowOrKill(
                    app,
                    mainWindow,
                    automation,
                    "Designer process did not exit after development launch interaction smoke test.");
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
}
