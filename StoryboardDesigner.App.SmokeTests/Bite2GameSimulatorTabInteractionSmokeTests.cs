using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace StoryboardDesigner.App.SmokeTests;

public sealed class Bite2GameSimulatorTabInteractionSmokeTests
{
    [Fact]
    public void Can_Find_LegacySimulator_And_DevelopmentWebPortal_Menu_EntryPoints()
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
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "opening simulator tab smoke shell");

                var runSimulatorMenuItem = Retry.WhileNull(
                    () => mainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.Menu.RunSimulator")),
                    timeout: TimeSpan.FromSeconds(8),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.NotNull(runSimulatorMenuItem);
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding run simulator menu item");

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

                var simulatorSetupMenuItem = Retry.WhileNull(
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

                        return mainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.Menu.Tools.SimulatorSetup"))
                            ?? desktop.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.Menu.Tools.SimulatorSetup"))
                            ?? mainWindow.FindFirstDescendant(cf => cf.ByName("Simulator Setup"))
                            ?? desktop.FindFirstDescendant(cf => cf.ByName("Simulator Setup"));
                    },
                    timeout: TimeSpan.FromSeconds(8),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;

                Assert.NotNull(simulatorSetupMenuItem);
                SmokeUiRetryHelpers.AssertAppStillRunning(app, "finding simulator setup menu item");

                SmokeUiRetryHelpers.CloseAppWindowOrKill(
                    app,
                    mainWindow,
                    automation,
                    "Designer process did not exit after simulator tab interaction smoke test.");
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
