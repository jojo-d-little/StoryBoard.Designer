using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace StoryboardDesigner.App.SmokeTests;

public sealed class Bite3SimulatorHappyPathSmokeTests
{
    private const string SmokeProjectPathEnvironmentVariable = "STORYBOARD_DESIGNER_SMOKE_PROJECT_PATH";
    private const int WindowMessageClose = 0x0010;

    [Fact]
    public void RunSimulator_MenuEntryPoint_DoesNotClose_Designer()
    {
        StaTestRunner.Run(() =>
        {
            var executablePath = AppExecutableLocator.ResolveDesignerExecutablePath();
            var canonicalFixturePath = Path.Combine(AppContext.BaseDirectory, "Samples", "MapDemo1", "MapDemo1.sbe.json");
            Assert.True(File.Exists(canonicalFixturePath), $"Canonical fixture not found: {canonicalFixturePath}");

            var priorSmokeFixture = Environment.GetEnvironmentVariable(SmokeProjectPathEnvironmentVariable);
            Environment.SetEnvironmentVariable(SmokeProjectPathEnvironmentVariable, canonicalFixturePath);
            using var app = Application.Launch(executablePath);
            try
            {
                using var automation = new UIA3Automation();

                var mainWindow = SmokeUiRetryHelpers.WaitForMainWindow(app, automation);
                Assert.NotNull(mainWindow);
                var mainWindowHandle = mainWindow!.Properties.NativeWindowHandle.ValueOrDefault;

                var runSimulatorMenuItem = Retry.WhileNull(
                    () => mainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.Menu.RunSimulator"))?.AsMenuItem(),
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.NotNull(runSimulatorMenuItem);
                runSimulatorMenuItem!.Click();

                var appStillRunning = Retry.WhileFalse(
                    () => !app.HasExited,
                    timeout: TimeSpan.FromSeconds(5),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.True(appStillRunning, "Designer exited unexpectedly after invoking Run Simulator.");

                DismissModalDialogIfPresent(app, automation, mainWindowHandle);

                RequestCloseMainWindow(mainWindowHandle);

                var exited = Retry.WhileFalse(
                    () =>
                    {
                        DismissModalDialogIfPresent(app, automation, mainWindowHandle);
                        if (!app.HasExited)
                        {
                            RequestCloseMainWindow(mainWindowHandle);
                        }

                        return app.HasExited;
                    },
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.True(exited, "Designer process did not exit after closing the main window.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(SmokeProjectPathEnvironmentVariable, priorSmokeFixture);
                var hasExited = true;
                try
                {
                    hasExited = app.HasExited;
                }
                catch (InvalidOperationException)
                {
                    hasExited = true;
                }

                if (!hasExited)
                {
                    app.Kill();
                }
            }
        });
    }

    private static void RequestCloseMainWindow(nint mainWindowHandle)
    {
        if (mainWindowHandle == 0)
        {
            return;
        }

        _ = PostMessage(mainWindowHandle, WindowMessageClose, IntPtr.Zero, IntPtr.Zero);
    }

    private static void DismissModalDialogIfPresent(Application app, UIA3Automation automation, nint mainWindowHandle)
    {
        if (app.HasExited)
        {
            return;
        }

        var dialog = app.GetAllTopLevelWindows(automation)
            .FirstOrDefault(window =>
                window.Properties.NativeWindowHandle.ValueOrDefault != mainWindowHandle);

        if (dialog is null)
        {
            return;
        }

        var primaryButton = dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("OK")))?.AsButton()
            ?? dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Yes")))?.AsButton()
            ?? dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("&Yes")))?.AsButton()
            ?? dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("No")))?.AsButton()
            ?? dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("&No")))?.AsButton()
            ?? dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).FirstOrDefault()?.AsButton();

        if (primaryButton is not null)
        {
            primaryButton.Invoke();
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PostMessage(nint hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
