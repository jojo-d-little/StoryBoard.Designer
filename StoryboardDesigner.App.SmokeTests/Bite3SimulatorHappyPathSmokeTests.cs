using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace StoryboardDesigner.App.SmokeTests;

[Collection("Designer UI Smoke")]
public sealed class Bite3SimulatorHappyPathSmokeTests
{
    private const string SmokeProjectPathEnvironmentVariable = "STORYBOARD_DESIGNER_SMOKE_PROJECT_PATH";
    private const int WindowMessageClose = 0x0010;

    [Fact]
    public void RunDevelopmentWebPortal_UnsavedProject_ShowsActionableWarning()
    {
        StaTestRunner.Run(() =>
        {
            var executablePath = AppExecutableLocator.ResolveDesignerExecutablePath();
            var priorSmokeFixture = Environment.GetEnvironmentVariable(SmokeProjectPathEnvironmentVariable);
            Environment.SetEnvironmentVariable(SmokeProjectPathEnvironmentVariable, null);
            using var app = Application.Launch(executablePath);
            try
            {
                using var automation = new UIA3Automation();

                var mainWindow = SmokeUiRetryHelpers.WaitForMainWindow(app, automation);
                Assert.NotNull(mainWindow);
                var mainWindowHandle = mainWindow!.Properties.NativeWindowHandle.ValueOrDefault;

                var runDevelopmentMenuItem = Retry.WhileNull(
                    () => mainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.Menu.RunDevelopmentWebPortal"))?.AsMenuItem(),
                    timeout: TimeSpan.FromSeconds(10),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.NotNull(runDevelopmentMenuItem);
                runDevelopmentMenuItem!.Invoke();

                var appStillRunning = Retry.WhileFalse(
                    () => !app.HasExited,
                    timeout: TimeSpan.FromSeconds(5),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.True(appStillRunning, "Designer exited unexpectedly after invoking Run Development WebPortal.");

                var warning = Retry.WhileNull(
                    () => mainWindow.ModalWindows.Concat(app.GetAllTopLevelWindows(automation)).FirstOrDefault(w => w.Title == "Run Development WebPortal"),
                    timeout: TimeSpan.FromSeconds(8),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
                Assert.NotNull(warning);
                Assert.NotNull(warning!.FindFirstDescendant(cf => cf.ByName("Save the project before launching WebPortal.")));
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
