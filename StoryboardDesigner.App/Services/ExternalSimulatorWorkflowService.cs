using System.Diagnostics;
using System.IO;
using System.Windows;
using StoryboardDesigner.App.Views;

namespace StoryboardDesigner.App.Services;

public sealed class ExternalSimulatorWorkflowService : IExternalSimulatorWorkflowService
{
    private readonly IProjectCreationPreferencesService _projectCreationPreferencesService;

    public ExternalSimulatorWorkflowService(IProjectCreationPreferencesService projectCreationPreferencesService)
    {
        _projectCreationPreferencesService = projectCreationPreferencesService;
    }

    public RunExternalSimulatorResult RunSimulator(RunExternalSimulatorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectFilePath) || !File.Exists(request.ProjectFilePath))
        {
            return new RunExternalSimulatorResult
            {
                Success = false,
                StatusMessage = "Run Simulator requires a saved project file path.",
                Diagnostics = ["Project file path was empty or missing."]
            };
        }

        var preferences = _projectCreationPreferencesService.Load();
        if (!TryResolveSimulatorExecutablePath(preferences.SimulatorExecutablePath, out var executablePath, out var executableResolutionError))
        {
            return new RunExternalSimulatorResult
            {
                Success = false,
                StatusMessage = "Unable to resolve simulator executable. Configure it in Tools -> Simulator Setup.",
                Diagnostics = [executableResolutionError]
            };
        }

        var cleanProjectPath = BuildCleanProjectPath(request.ProjectFilePath);
        if (!File.Exists(cleanProjectPath))
        {
            return new RunExternalSimulatorResult
            {
                Success = false,
                StatusMessage = "Runtime export file is missing. Save and export before running Simulator.",
                Diagnostics = [$"Expected runtime project file: {cleanProjectPath}"]
            };
        }

        try
        {
            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(cleanProjectPath);

            if (!string.IsNullOrWhiteSpace(request.ReplayFilePath))
            {
                startInfo.ArgumentList.Add("--replay");
                startInfo.ArgumentList.Add(request.ReplayFilePath.Trim());
            }

            if (request.ReplaySpeed.HasValue)
            {
                startInfo.ArgumentList.Add("--replay-speed");
                startInfo.ArgumentList.Add(request.ReplaySpeed.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            }

            _ = Process.Start(startInfo);
            return new RunExternalSimulatorResult
            {
                Success = true,
                StatusMessage = "Simulator launch requested.",
                LaunchedExecutablePath = executablePath,
                Diagnostics = [$"Project: {cleanProjectPath}"]
            };
        }
        catch (Exception ex)
        {
            return new RunExternalSimulatorResult
            {
                Success = false,
                StatusMessage = "Failed to launch simulator process.",
                LaunchedExecutablePath = executablePath,
                Diagnostics = [ex.Message]
            };
        }
    }

    public SimulatorSetupDialogResult OpenSimulatorSetup(SimulatorSetupDialogRequest request)
    {
        var preferences = _projectCreationPreferencesService.Load();
        var dialog = new SimulatorSetupDialog(
            request.ReplayFilePath,
            request.ReplaySpeed,
            preferences.SimulatorExecutablePath)
        {
            Owner = GetOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            return new SimulatorSetupDialogResult
            {
                AppliedChanges = false,
                StatusMessage = "Simulator setup canceled.",
                ReplayFilePath = request.ReplayFilePath,
                ReplaySpeed = request.ReplaySpeed,
                SimulatorExecutablePath = preferences.SimulatorExecutablePath
            };
        }

        preferences.SimulatorExecutablePath = dialog.SimulatorExecutablePath;
        _projectCreationPreferencesService.Save(preferences);

        return new SimulatorSetupDialogResult
        {
            AppliedChanges = true,
            StatusMessage = "Simulator setup saved.",
            ReplayFilePath = dialog.ReplayFilePath,
            ReplaySpeed = dialog.ReplaySpeed,
            SimulatorExecutablePath = dialog.SimulatorExecutablePath
        };
    }

    private static Window? GetOwnerWindow()
    {
        var app = System.Windows.Application.Current;
        var activeWindow = app?.Windows.OfType<Window>().FirstOrDefault(static window => window.IsActive);
        return activeWindow ?? app?.MainWindow;
    }

    private static string BuildCleanProjectPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        if (baseName.EndsWith(".sbe", StringComparison.OrdinalIgnoreCase))
        {
            baseName = baseName[..^4];
        }

        return Path.Combine(folder, "GameRuntimeJson", $"{baseName}.sbr.runtime.json");
    }

    private static bool TryResolveSimulatorExecutablePath(string configuredExecutablePath, out string resolvedPath, out string failureReason)
    {
        resolvedPath = string.Empty;
        failureReason = string.Empty;

        if (!string.IsNullOrWhiteSpace(configuredExecutablePath))
        {
            var normalized = configuredExecutablePath.Trim();
            if (!File.Exists(normalized))
            {
                failureReason = $"Configured simulator executable path does not exist: {normalized}";
                return false;
            }

            resolvedPath = normalized;
            return true;
        }

        var packagedCandidate = Path.Combine(AppContext.BaseDirectory, "Storyboard.Simulator.exe");
        if (File.Exists(packagedCandidate))
        {
            resolvedPath = packagedCandidate;
            return true;
        }

        var devCandidate = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Storyboard.Simulator",
            "bin",
            "Debug",
            "net8.0-windows",
            "Storyboard.Simulator.exe"));
        if (File.Exists(devCandidate))
        {
            resolvedPath = devCandidate;
            return true;
        }

        failureReason = "No configured executable path and no packaged default simulator executable was found.";
        return false;
    }
}
