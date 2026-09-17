using System.IO;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace StoryboardDesigner.App.SmokeTests;

public sealed class Bite0LaunchSmokeTests
{
    [Fact]
    public void Launches_MainWindow_And_Closes_Cleanly()
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
                Assert.Contains("Storyboard Designer", mainWindow!.Title, StringComparison.Ordinal);

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
}

internal static class SmokeUiRetryHelpers
{
    private static readonly TimeSpan GracefulExitTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ForcedExitTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ExitPollInterval = TimeSpan.FromMilliseconds(100);

    internal static void AssertAppStillRunning(Application app, string context)
    {
        Assert.False(app.HasExited, $"Designer process exited unexpectedly during smoke test: {context}.");
    }

    internal static string? TryGetWindowTitle(Window window)
    {
        try
        {
            return window.Title;
        }
        catch
        {
            return null;
        }
    }

    internal static Window? WaitForMainWindow(Application app, UIA3Automation automation)
    {
        var window = Retry.WhileNull(
            () => TryGetMainWindow(app, automation),
            timeout: TimeSpan.FromSeconds(20),
            interval: TimeSpan.FromMilliseconds(200),
            throwOnTimeout: false).Result;

        AssertAppStillRunning(app, "waiting for main window");
        return window;
    }

    internal static AutomationElement? WaitForDesktopElementByAutomationId(
        UIA3Automation automation,
        string automationId,
        TimeSpan? timeout = null,
        TimeSpan? interval = null)
    {
        return Retry.WhileNull(
            () => TryFindDesktopElementByAutomationId(automation, automationId),
            timeout: timeout ?? TimeSpan.FromSeconds(10),
            interval: interval ?? TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false).Result;
    }

    internal static void CloseAppWindowOrKill(
        Application app,
        Window mainWindow,
        UIA3Automation? automation = null,
        string failureMessage = "Designer process did not exit during teardown.")
    {
        var mainWindowHandle = TryGetNativeWindowHandle(mainWindow);

        try
        {
            mainWindow.Close();
        }
        catch
        {
        }

        var exited = Retry.WhileFalse(
            () =>
            {
                DismissCloseDialogsIfPresent(app, automation, mainWindowHandle);
                return app.HasExited;
            },
            timeout: GracefulExitTimeout,
            interval: ExitPollInterval,
            throwOnTimeout: false).Result;

        if (!exited && !app.HasExited)
        {
            app.Kill();
            exited = Retry.WhileFalse(
                () => app.HasExited,
                timeout: ForcedExitTimeout,
                interval: ExitPollInterval,
                throwOnTimeout: false).Result;
        }

        Assert.True(exited, failureMessage);
    }

    private static nint TryGetNativeWindowHandle(Window window)
    {
        try
        {
            return window.Properties.NativeWindowHandle.ValueOrDefault;
        }
        catch
        {
            return 0;
        }
    }

    private static void DismissCloseDialogsIfPresent(Application app, UIA3Automation? automation, nint mainWindowHandle)
    {
        if (automation is null || app.HasExited)
        {
            return;
        }

        Window? dialog = null;
        try
        {
            dialog = app.GetAllTopLevelWindows(automation)
                .FirstOrDefault(window => window.Properties.NativeWindowHandle.ValueOrDefault != mainWindowHandle);
        }
        catch
        {
            return;
        }

        if (dialog is null)
        {
            return;
        }

        var dismissalButton =
            dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Don't Save")))?.AsButton()
            ?? dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("&Don't Save")))?.AsButton()
            ?? dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("No")))?.AsButton()
            ?? dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("&No")))?.AsButton()
            ?? dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Yes")))?.AsButton()
            ?? dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("&Yes")))?.AsButton()
            ?? dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("OK")))?.AsButton()
            ?? dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).FirstOrDefault()?.AsButton();

        if (dismissalButton is not null)
        {
            try
            {
                dismissalButton.Invoke();
            }
            catch
            {
            }
        }
    }

    private static Window? TryGetMainWindow(Application app, UIA3Automation automation)
    {
        try
        {
            return app.GetMainWindow(automation);
        }
        catch
        {
            return null;
        }
    }

    internal static AutomationElement? TryFindDesktopElementByAutomationId(UIA3Automation automation, string automationId)
    {
        try
        {
            return automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(automationId));
        }
        catch
        {
            return null;
        }
    }
}

internal static class StaTestRunner
{
    internal static void Run(Action action)
    {
        Exception? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            throw new Xunit.Sdk.XunitException($"STA smoke execution failed: {captured}");
        }
    }
}

internal static class AppExecutableLocator
{
    private const string ExecutableName = "StoryboardDesigner.App.exe";
    private const string ConfigurationOverrideVariable = "STORYBOARD_DESIGNER_CONFIGURATION";
    private const string ExecutablePathOverrideVariable = "STORYBOARD_DESIGNER_APP_EXE";

    internal static string ResolveDesignerExecutablePath()
    {
        var configuredPath = Environment.GetEnvironmentVariable(ExecutablePathOverrideVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fullConfiguredPath = Path.GetFullPath(configuredPath);
            if (File.Exists(fullConfiguredPath))
            {
                return fullConfiguredPath;
            }

            throw new FileNotFoundException(
                $"Environment variable {ExecutablePathOverrideVariable} was set but file does not exist.",
                fullConfiguredPath);
        }

        var solutionRoot = ResolveSolutionRoot();
        var configuration = Environment.GetEnvironmentVariable(ConfigurationOverrideVariable);
        var candidates = BuildCandidatePaths(solutionRoot, configuration);

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            "Could not locate StoryboardDesigner.App.exe. Build StoryboardDesigner.App first or set STORYBOARD_DESIGNER_APP_EXE.",
            candidates[0]);
    }

    internal static string ResolveSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StoryboardDesigner.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate solution root containing StoryboardDesigner.slnx.");
    }

    private static List<string> BuildCandidatePaths(string solutionRoot, string? configuration)
    {
        var configurations = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuration))
        {
            configurations.Add(configuration);
        }

        configurations.Add("Debug");
        configurations.Add("Release");

        return configurations
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(config => Path.Combine(
                solutionRoot,
                "StoryboardDesigner.App",
                "bin",
                config,
                "net8.0-windows",
                ExecutableName))
            .ToList();
    }
}
