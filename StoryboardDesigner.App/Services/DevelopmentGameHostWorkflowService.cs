using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace StoryboardDesigner.App.Services;

/// <summary>
/// Implements the Designer-owned development GameHost process workflow.
/// </summary>
public sealed class DevelopmentGameHostWorkflowService : IDevelopmentGameHostWorkflowService
{
    private const int DefaultReadinessTimeoutSeconds = 20;
    private readonly IProjectCreationPreferencesService _preferencesService;
    private readonly IProcessLauncher _processLauncher;
    private readonly IHostReadinessProbe _readinessProbe;
    private readonly TimeSpan _readinessTimeout;
    private IManagedProcess? _activeProcess;
    private string? _activeRegistrationFilePath;

    public DevelopmentGameHostWorkflowService(
        IProjectCreationPreferencesService preferencesService,
        IProcessLauncher? processLauncher = null,
        IHostReadinessProbe? readinessProbe = null,
        TimeSpan? readinessTimeout = null)
    {
        _preferencesService = preferencesService;
        _processLauncher = processLauncher ?? new ProcessLauncher();
        _readinessProbe = readinessProbe ?? new HostReadinessProbe();
        _readinessTimeout = readinessTimeout ?? TimeSpan.FromSeconds(DefaultReadinessTimeoutSeconds);
    }

    public async Task<DevelopmentGameHostLaunchResult> LaunchAsync(
        DevelopmentGameHostLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectFilePath) || !File.Exists(request.ProjectFilePath))
        {
            return Failure("Development launch requires a saved project file path.", "Project file path was empty or missing.");
        }

        if (string.IsNullOrWhiteSpace(request.RuntimeProjectPath) || !File.Exists(request.RuntimeProjectPath))
        {
            return Failure("Runtime export is missing. Save and export before launching WebPortal.", "Runtime project path was empty or missing.");
        }

        var projectPath = Path.GetFullPath(request.ProjectFilePath.Trim());
        var runtimeProjectPath = Path.GetFullPath(request.RuntimeProjectPath.Trim());
        var identity = DevelopmentLaunchRegistration.CreateIdentity(projectPath);
        var preferences = _preferencesService.Load();
        var developmentUsername = ResolveDevelopmentUsername(preferences.DevelopmentUsername);

        Stop();

        if (!TryResolveExecutablePath(preferences.GameHostExecutablePath, DevelopmentLaunchRegistration.GameHostExecutableEnvironmentVariable, "Storyboard.GameHost.exe", out var hostExecutablePath, out var hostFailure))
        {
            return Failure("Unable to resolve GameHost executable.", hostFailure);
        }

        if (!TryResolveWebPortalRoot(preferences.WebPortalRootPath, out var webPortalRoot, out var webPortalFailure))
        {
            return Failure("Unable to resolve the WebPortal bundle.", webPortalFailure);
        }

        var registrationFilePath = CreateRegistrationFile(identity, runtimeProjectPath);
        int port;
        Uri hostUri;
        ProcessStartInfo startInfo;
        try
        {
            port = ReserveLoopbackPort();
            hostUri = new Uri($"http://127.0.0.1:{port}");
            startInfo = BuildStartInfo(hostExecutablePath, webPortalRoot, registrationFilePath, port);
        }
        catch (Exception ex)
        {
            TryDeleteRegistrationFile(registrationFilePath);
            return Failure("Failed to prepare GameHost launch.", ex.Message, registrationFilePath);
        }

        var launchDiagnostics = BuildLaunchDiagnostics(startInfo);
        IManagedProcess process;
        try
        {
            process = _processLauncher.Start(startInfo);
        }
        catch (Exception ex)
        {
            TryDeleteRegistrationFile(registrationFilePath);
            return Failure("Failed to start GameHost process.", ex.Message, registrationFilePath, launchDiagnostics);
        }

        _activeProcess = process;
        _activeRegistrationFilePath = registrationFilePath;

        HostReadinessResult readiness;
        try
        {
            readiness = await _readinessProbe.WaitUntilReadyAsync(
                hostUri,
                () => process.HasExited,
                _readinessTimeout,
                cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            Stop();
            throw;
        }

        if (!readiness.IsReady)
        {
            Stop();
            return Failure("GameHost did not become ready.", readiness.Message, registrationFilePath, launchDiagnostics);
        }

        return BuildSuccess(identity, hostUri, registrationFilePath, developmentUsername, launchDiagnostics);
    }

    public void Stop()
    {
        try
        {
            _activeProcess?.Kill();
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(_activeRegistrationFilePath))
            {
                TryDeleteRegistrationFile(_activeRegistrationFilePath);
            }

            _activeProcess = null;
            _activeRegistrationFilePath = null;
        }
    }

    private static ProcessStartInfo BuildStartInfo(
        string hostExecutablePath,
        string webPortalRoot,
        string registrationFilePath,
        int port)
    {
        var workingDirectory = Path.GetDirectoryName(hostExecutablePath) ?? AppContext.BaseDirectory;
        var startInfo = new ProcessStartInfo(hostExecutablePath)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            // Development launches intentionally expose the GameHost console so its live
            // logging is visible while the WebPortal is being exercised.
            CreateNoWindow = false
        };

        startInfo.ArgumentList.Add("--GameHost:Transport:Port");
        startInfo.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--GameHost:Transport:BindPolicy");
        startInfo.ArgumentList.Add("LoopbackOnly");
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment[DevelopmentLaunchRegistration.DiscoveryJsonPathEnvironmentVariable] = registrationFilePath;
        startInfo.Environment[DevelopmentLaunchRegistration.WebPortalRootEnvironmentVariable] = webPortalRoot;
        return startInfo;
    }

    private static string CreateRegistrationFile(DevelopmentLaunchIdentity identity, string runtimeProjectPath)
    {
        var directory = Path.Combine(Path.GetTempPath(), "StoryboardDesigner", "DevelopmentLaunch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "runtime-game-registrations.json");
        File.WriteAllText(path, DevelopmentLaunchRegistration.Serialize(identity, runtimeProjectPath));
        return path;
    }

    private static int ReserveLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static bool TryResolveExecutablePath(
        string configuredPath,
        string environmentVariable,
        string executableName,
        out string resolvedPath,
        out string failureReason)
    {
        resolvedPath = string.Empty;
        failureReason = string.Empty;
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            candidates.Add(configuredPath.Trim());
        }

        var environmentPath = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            candidates.Add(environmentPath.Trim());
        }

        candidates.Add(Path.Combine(AppContext.BaseDirectory, executableName));
        candidates.Add(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "StoryBoard.GameEngine",
            "Storyboard.GameHost",
            "bin", "Debug", "net8.0",
            executableName)));
        candidates.Add(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "StoryBoard.GameEngine",
            "Storyboard.GameHost",
            "bin", "Release", "net8.0",
            executableName)));

        resolvedPath = candidates.FirstOrDefault(File.Exists) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(resolvedPath))
        {
            resolvedPath = Path.GetFullPath(resolvedPath);
            return true;
        }

        failureReason = $"Configure GameHostExecutablePath or {environmentVariable}; searched the packaged path and local GameEngine Debug/Release outputs.";
        return false;
    }

    private static bool TryResolveWebPortalRoot(
        string configuredPath,
        out string resolvedPath,
        out string failureReason)
    {
        resolvedPath = string.Empty;
        failureReason = string.Empty;
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            candidates.Add(configuredPath.Trim());
        }

        var environmentPath = Environment.GetEnvironmentVariable(DevelopmentLaunchRegistration.WebPortalRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            candidates.Add(environmentPath.Trim());
        }

        candidates.Add(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "StoryBoard.WebPortal",
            "Storyboard.WebPortal",
            "dist")));

        resolvedPath = candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(path => Directory.Exists(path) && File.Exists(Path.Combine(path, "index.html"))) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(resolvedPath))
        {
            return true;
        }

        failureReason = $"Configure WebPortalRootPath or {DevelopmentLaunchRegistration.WebPortalRootEnvironmentVariable}; the folder must contain index.html.";
        return false;
    }

    private static DevelopmentGameHostLaunchResult BuildSuccess(
        DevelopmentLaunchIdentity identity,
        Uri hostUri,
        string registrationFilePath,
        string developmentUsername,
        IReadOnlyList<string> launchDiagnostics)
    {
        return new DevelopmentGameHostLaunchResult
        {
            Success = true,
            StatusMessage = "Development GameHost is ready; opening WebPortal.",
            HostUri = hostUri,
            BrowserUri = BuildBrowserUri(hostUri, developmentUsername),
            DevelopmentUsername = developmentUsername,
            LaunchDiagnostics = launchDiagnostics,
            RegistrationFilePath = registrationFilePath,
            GameId = identity.GameId,
            GameKey = identity.GameKey
        };
    }

    private static Uri BuildBrowserUri(Uri hostUri, string developmentUsername)
    {
        var browserUri = new UriBuilder(new Uri(hostUri, "/client/"));
        browserUri.Query = string.Join(
            "&",
            $"{DevelopmentLaunchRegistration.WebPortalLaunchModeQueryParameter}={Uri.EscapeDataString(DevelopmentLaunchRegistration.DevelopmentSimulatorLaunchMode)}",
            $"{DevelopmentLaunchRegistration.DevelopmentUsernameQueryParameter}={Uri.EscapeDataString(developmentUsername)}",
            $"{DevelopmentLaunchRegistration.AutoStartSessionQueryParameter}=true");
        return browserUri.Uri;
    }

    private static string ResolveDevelopmentUsername(string? configuredUsername)
    {
        return string.IsNullOrWhiteSpace(configuredUsername)
            ? DevelopmentLaunchRegistration.DefaultDevelopmentUsername
            : configuredUsername.Trim();
    }

    private static DevelopmentGameHostLaunchResult Failure(
        string statusMessage,
        string diagnostic,
        string? registrationFilePath = null,
        IReadOnlyList<string>? launchDiagnostics = null)
    {
        return new DevelopmentGameHostLaunchResult
        {
            Success = false,
            StatusMessage = statusMessage,
            Diagnostics = [diagnostic],
            LaunchDiagnostics = launchDiagnostics ?? Array.Empty<string>(),
            RegistrationFilePath = registrationFilePath
        };
    }

    private static IReadOnlyList<string> BuildLaunchDiagnostics(ProcessStartInfo startInfo)
    {
        var executablePath = Path.GetFullPath(startInfo.FileName);
        var arguments = startInfo.ArgumentList.ToArray();
        var lines = new List<string>
        {
            $"[DEV-HOST] Executable: {executablePath}",
            $"[DEV-HOST] Working directory: {startInfo.WorkingDirectory}",
            $"[DEV-HOST] Console window: {(startInfo.CreateNoWindow ? "hidden" : "visible")}",
            $"[DEV-HOST] Command: {FormatCommandLine(executablePath, arguments)}",
            $"[DEV-HOST] Argument count: {arguments.Length}"
        };

        for (var index = 0; index < arguments.Length; index++)
        {
            lines.Add($"[DEV-HOST] Argument[{index}]: {arguments[index]}");
        }

        lines.Add("[DEV-HOST] Environment overrides:");
        foreach (var variableName in new[]
        {
            "DOTNET_ENVIRONMENT",
            "ASPNETCORE_ENVIRONMENT",
            DevelopmentLaunchRegistration.DiscoveryJsonPathEnvironmentVariable,
            DevelopmentLaunchRegistration.WebPortalRootEnvironmentVariable
        })
        {
            if (startInfo.Environment.TryGetValue(variableName, out var value))
            {
                lines.Add($"[DEV-HOST] Environment[{variableName}]: {value}");
            }
        }

        return lines;
    }

    private static string FormatCommandLine(string executablePath, IEnumerable<string> arguments)
    {
        return string.Join(
            " ",
            new[] { executablePath }.Concat(arguments).Select(static value => $"\"{value.Replace("\"", "\\\"")}\""));
    }

    private static void TryDeleteRegistrationFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        catch
        {
            // Cleanup is best effort; the launch result already contains the path for diagnostics.
        }
    }
}
