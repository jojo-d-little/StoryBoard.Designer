using System.IO;
using System.Windows;

namespace StoryboardDesigner.App.Views;

public partial class SimulatorSetupDialog : Window
{
    public SimulatorSetupDialog(string replayFilePath, double? replaySpeed, string simulatorExecutablePath)
    {
        InitializeComponent();
        ReplayFilePathTextBox.Text = replayFilePath ?? string.Empty;
        ReplaySpeedTextBox.Text = replaySpeed.HasValue ? replaySpeed.Value.ToString("0.###") : string.Empty;
        SimulatorExecutablePathTextBox.Text = simulatorExecutablePath ?? string.Empty;
    }

    public string ReplayFilePath => ReplayFilePathTextBox.Text?.Trim() ?? string.Empty;

    public double? ReplaySpeed { get; private set; }

    public string SimulatorExecutablePath => SimulatorExecutablePathTextBox.Text?.Trim() ?? string.Empty;

    private void BrowseReplay_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Simulator recording (*.sbe.sim.json)|*.sbe.sim.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Select Replay File"
        };

        if (dialog.ShowDialog(this) == true)
        {
            ReplayFilePathTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseExecutable_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Select Simulator Executable"
        };

        if (dialog.ShowDialog(this) == true)
        {
            SimulatorExecutablePathTextBox.Text = dialog.FileName;
        }
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryValidateReplaySpeed(out var replaySpeed))
        {
            return;
        }

        var replayFilePath = ReplayFilePath;
        if (!string.IsNullOrWhiteSpace(replayFilePath) && !File.Exists(replayFilePath))
        {
            System.Windows.MessageBox.Show(this, "Replay file path was provided but does not exist.", "Simulator Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
            ReplayFilePathTextBox.Focus();
            ReplayFilePathTextBox.SelectAll();
            return;
        }

        var executablePath = SimulatorExecutablePath;
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            if (!string.Equals(Path.GetExtension(executablePath), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.MessageBox.Show(this, "Simulator executable path must point to a .exe file.", "Simulator Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
                SimulatorExecutablePathTextBox.Focus();
                SimulatorExecutablePathTextBox.SelectAll();
                return;
            }

            if (!File.Exists(executablePath))
            {
                System.Windows.MessageBox.Show(this, "Simulator executable path was provided but does not exist.", "Simulator Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
                SimulatorExecutablePathTextBox.Focus();
                SimulatorExecutablePathTextBox.SelectAll();
                return;
            }
        }

        ReplaySpeed = replaySpeed;
        DialogResult = true;
    }

    private bool TryValidateReplaySpeed(out double? replaySpeed)
    {
        replaySpeed = null;
        var raw = ReplaySpeedTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!double.TryParse(raw, out var parsed) || parsed <= 0)
        {
            System.Windows.MessageBox.Show(this, "Replay speed must be a positive number.", "Simulator Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
            ReplaySpeedTextBox.Focus();
            ReplaySpeedTextBox.SelectAll();
            return false;
        }

        replaySpeed = parsed;
        return true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
