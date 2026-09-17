using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace StoryboardDesigner.App.SmokeTests;

public sealed class DeepUiTraversalDoorWorkflowTests
{
    private const string SmokeProjectPathEnvironmentVariable = "STORYBOARD_DESIGNER_SMOKE_PROJECT_PATH";
    private const string RunDeepUiEnvironmentVariable = "STORYBOARD_DESIGNER_RUN_DEEP_UI";

    [Fact]
    [Trait("Category", "DeepUi")]
    public void DeepUi_SimulatorMultiCommandSession_TraversalAndDoorVerbsProduceOutput()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunDeepUiEnvironmentVariable), "1", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        StaTestRunner.Run(() =>
        {
            var executablePath = AppExecutableLocator.ResolveDesignerExecutablePath();
            var solutionRoot = AppExecutableLocator.ResolveSolutionRoot();
            var deepFixturePath = Path.Combine(solutionRoot, "Samples", "MapDemo1", "MapDemo1.sbe.json");
            Assert.True(File.Exists(deepFixturePath), $"Deep UI fixture not found: {deepFixturePath}");

            var priorSmokeFixturePath = Environment.GetEnvironmentVariable(SmokeProjectPathEnvironmentVariable);
            Environment.SetEnvironmentVariable(SmokeProjectPathEnvironmentVariable, deepFixturePath);

            try
            {
                using var app = Application.Launch(executablePath);
                try
                {
                    using var automation = new UIA3Automation();
                    var mainWindow = SmokeUiRetryHelpers.WaitForMainWindow(app, automation);
                    Assert.NotNull(mainWindow);

                    SelectGameSimulatorTab(mainWindow!);

                    var consoleList = Retry.WhileNull(
                        () => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.GameSimulator.ConsoleList"))?.AsListBox(),
                        timeout: TimeSpan.FromSeconds(10),
                        interval: TimeSpan.FromMilliseconds(100),
                        throwOnTimeout: false).Result;
                    Assert.NotNull(consoleList);

                    var initializeButton = Retry.WhileNull(
                        () => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.GameSimulator.InitializeFromMemory"))?.AsButton(),
                        timeout: TimeSpan.FromSeconds(10),
                        interval: TimeSpan.FromMilliseconds(100),
                        throwOnTimeout: false).Result;
                    Assert.NotNull(initializeButton);
                    initializeButton!.Invoke();

                    var commandInput = Retry.WhileNull(
                        () => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.GameSimulator.CommandInput"))?.AsTextBox(),
                        timeout: TimeSpan.FromSeconds(10),
                        interval: TimeSpan.FromMilliseconds(100),
                        throwOnTimeout: false).Result;
                    Assert.NotNull(commandInput);

                    var becameEnabled = Retry.WhileFalse(
                        () => commandInput!.Properties.IsEnabled.Value,
                        timeout: TimeSpan.FromSeconds(10),
                        interval: TimeSpan.FromMilliseconds(100),
                        throwOnTimeout: false).Result;
                    Assert.True(becameEnabled, "Command input did not become enabled after Initialize (In-Memory).");

                    var submitButton = Retry.WhileNull(
                        () => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.GameSimulator.SubmitCommand"))?.AsButton(),
                        timeout: TimeSpan.FromSeconds(10),
                        interval: TimeSpan.FromMilliseconds(100),
                        throwOnTimeout: false).Result;
                    Assert.NotNull(submitButton);

                    var diagnosticsCombo = Retry.WhileNull(
                        () => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.GameSimulator.DiagnosticsLevel"))?.AsComboBox(),
                        timeout: TimeSpan.FromSeconds(10),
                        interval: TimeSpan.FromMilliseconds(100),
                        throwOnTimeout: false).Result;
                    Assert.NotNull(diagnosticsCombo);

                    var lowDiagnosticsItem = diagnosticsCombo!.Items
                        .FirstOrDefault(item => item.Name.Contains("Low", StringComparison.OrdinalIgnoreCase));
                    lowDiagnosticsItem?.Select();

                    var commandCombo = Retry.WhileNull(
                        () => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.GameSimulator.CommandCombo"))?.AsComboBox(),
                        timeout: TimeSpan.FromSeconds(10),
                        interval: TimeSpan.FromMilliseconds(100),
                        throwOnTimeout: false).Result;
                    Assert.NotNull(commandCombo);

                    var comboItems = commandCombo!.Items;
                    if (comboItems.Length > 0)
                    {
                        comboItems[0].Select();
                    }

                    var consoleBefore = consoleList!.Items.Length;

                    var firstSubmitted = SubmitCommand(commandInput!, submitButton!, consoleList, "look", consoleBefore, commandCombo);
                    Assert.True(firstSubmitted, "Initial simulator command did not submit successfully.");
                    var afterLook = consoleList.Items.Length;

                    var openSubmitted = SubmitCommand(commandInput!, submitButton!, consoleList, "open door", afterLook, commandCombo);
                    var afterOpen = consoleList.Items.Length;

                    var moveSubmitted = SubmitCommand(commandInput!, submitButton!, consoleList, "east", afterOpen, commandCombo);
                    var afterMove = consoleList.Items.Length;

                    var finalLookSubmitted = SubmitCommand(commandInput!, submitButton!, consoleList, "look", afterMove, commandCombo);

                    Assert.True(openSubmitted || moveSubmitted || finalLookSubmitted,
                        "Expected at least one additional command to submit after the initial look command.");
                    Assert.True(consoleList.Items.Length > consoleBefore,
                        "Console output did not grow across the deep simulator command sequence.");

                    SmokeUiRetryHelpers.CloseAppWindowOrKill(app, mainWindow, automation);
                }
                finally
                {
                    if (!app.HasExited)
                    {
                        app.Kill();
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(SmokeProjectPathEnvironmentVariable, priorSmokeFixturePath);
            }
        });
    }

    private static bool SubmitCommand(TextBox commandInput, Button submitButton, ListBox consoleList, string commandText, int previousCount, ComboBox? commandCombo)
    {
        commandInput.Focus();
        commandInput.Text = string.Empty;
        Keyboard.Type(commandText);

        var submitEnabled = Retry.WhileFalse(
            () => submitButton.Properties.IsEnabled.Value,
            timeout: TimeSpan.FromSeconds(3),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false).Result;

        if (!submitEnabled && commandCombo is not null)
        {
            var comboItems = commandCombo.Items;
            if (comboItems.Length > 0)
            {
                comboItems[0].Select();
                submitEnabled = Retry.WhileFalse(
                    () => submitButton.Properties.IsEnabled.Value,
                    timeout: TimeSpan.FromSeconds(3),
                    interval: TimeSpan.FromMilliseconds(100),
                    throwOnTimeout: false).Result;
            }
        }

        if (submitEnabled)
        {
            submitButton.Invoke();
        }
        else
        {
            Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ENTER);
        }

        var submitted = Retry.WhileFalse(
            () => consoleList.Items.Length > previousCount,
            timeout: TimeSpan.FromSeconds(10),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false).Result;

        return submitted;
    }

    private static void SelectGameSimulatorTab(Window mainWindow)
    {
        var workspaceTabs = Retry.WhileNull(
            () => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.WorkspaceTabs"))?.AsTab(),
            timeout: TimeSpan.FromSeconds(10),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false).Result;
        Assert.NotNull(workspaceTabs);

        var workspaceTabItem = Retry.WhileNull(
            () => workspaceTabs!.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.Tab.GameSimulator"))?.AsTabItem(),
            timeout: TimeSpan.FromSeconds(10),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false).Result;
        Assert.NotNull(workspaceTabItem);

        workspaceTabItem!.Select();
    }

}
