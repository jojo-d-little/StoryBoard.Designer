using System.Diagnostics;
using System.Text.Json;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class DevelopmentGameHostWorkflowServiceTests
{
    [Fact]
    public async Task LaunchAsync_WritesEstablishedRegistrationAndStartsReadyHost()
    {
        using var fixture = LaunchFixture.Create();
        var process = new FakeManagedProcess(4312);
        var launcher = new FakeProcessLauncher(process);
        var readiness = new FakeReadinessProbe(new HostReadinessResult { IsReady = true, Message = "ready" });
        var service = new DevelopmentGameHostWorkflowService(
            fixture.Preferences,
            launcher,
            readiness,
            TimeSpan.FromSeconds(1));

        var result = await service.LaunchAsync(new DevelopmentGameHostLaunchRequest
        {
            ProjectFilePath = fixture.ProjectFilePath,
            RuntimeProjectPath = fixture.RuntimeProjectPath
        });

        Assert.True(result.Success);
        Assert.Equal("/client/", result.BrowserUri?.AbsolutePath);
        Assert.Equal("?mode=devsimulator&username=designer-dev&autoStartSession=true", result.BrowserUri?.Query);
        Assert.Equal("designer-dev", result.DevelopmentUsername);
        Assert.NotNull(result.GameId);
        Assert.NotNull(result.GameKey);
        Assert.True(File.Exists(result.RegistrationFilePath));
        Assert.Single(launcher.Started);
        Assert.Equal("Development", launcher.Started[0].Environment["DOTNET_ENVIRONMENT"]);
        Assert.Equal(fixture.WebPortalRoot, launcher.Started[0].Environment[DevelopmentLaunchRegistration.WebPortalRootEnvironmentVariable]);
        Assert.Contains("LoopbackOnly", launcher.Started[0].ArgumentList);
        Assert.Contains("--GameHost:Transport:Port", launcher.Started[0].ArgumentList);
        Assert.Equal(readiness.LastHostUri, result.HostUri);
        Assert.False(launcher.Started[0].UseShellExecute);
        Assert.False(launcher.Started[0].CreateNoWindow);
        Assert.Contains(result.LaunchDiagnostics, line => line.Contains(launcher.Started[0].FileName, StringComparison.Ordinal));
        Assert.Contains(result.LaunchDiagnostics, line => line.Contains("Console window: visible", StringComparison.Ordinal));
        Assert.Contains(result.LaunchDiagnostics, line => line.Contains("Argument[0]: --GameHost:Transport:Port", StringComparison.Ordinal));
        Assert.Contains(result.LaunchDiagnostics, line => line.Contains(DevelopmentLaunchRegistration.DiscoveryJsonPathEnvironmentVariable, StringComparison.Ordinal));
        Assert.Contains(result.LaunchDiagnostics, line => line.Contains(DevelopmentLaunchRegistration.WebPortalRootEnvironmentVariable, StringComparison.Ordinal));

        using var document = JsonDocument.Parse(File.ReadAllText(result.RegistrationFilePath!));
        var entry = Assert.Single(document.RootElement.GetProperty("games").EnumerateArray());
        Assert.Equal(result.GameId!.Value, entry.GetProperty("gameId").GetGuid());
        Assert.Equal(result.GameKey, entry.GetProperty("gameKey").GetString());
        Assert.Equal(fixture.RuntimeProjectPath, entry.GetProperty("runtimeProjectPath").GetString());
        Assert.True(entry.GetProperty("isEnabled").GetBoolean());
        Assert.Equal("local-tenant", entry.GetProperty("tenantId").GetString());
        Assert.Equal("local-org", entry.GetProperty("orgId").GetString());

        service.Stop();

        Assert.True(process.KillCalled);
        Assert.False(File.Exists(result.RegistrationFilePath));
    }

    [Fact]
    public async Task LaunchAsync_ReadinessFailureKillsHostAndCleansRegistration()
    {
        using var fixture = LaunchFixture.Create();
        var process = new FakeManagedProcess(4313);
        var launcher = new FakeProcessLauncher(process);
        var readiness = new FakeReadinessProbe(new HostReadinessResult
        {
            IsReady = false,
            Message = "registration failed"
        });
        var service = new DevelopmentGameHostWorkflowService(
            fixture.Preferences,
            launcher,
            readiness,
            TimeSpan.FromSeconds(1));

        var result = await service.LaunchAsync(new DevelopmentGameHostLaunchRequest
        {
            ProjectFilePath = fixture.ProjectFilePath,
            RuntimeProjectPath = fixture.RuntimeProjectPath
        });

        Assert.False(result.Success);
        Assert.Contains("registration failed", result.Diagnostics);
        Assert.True(process.KillCalled);
        Assert.False(File.Exists(result.RegistrationFilePath));
        Assert.NotEmpty(result.LaunchDiagnostics);
    }

    [Fact]
    public async Task LaunchAsync_StartsFreshHostForEachRun()
    {
        using var fixture = LaunchFixture.Create();
        var firstProcess = new FakeManagedProcess(4314);
        var secondProcess = new FakeManagedProcess(4315);
        var launcher = new FakeProcessLauncher(firstProcess, secondProcess);
        var readiness = new FakeReadinessProbe(new HostReadinessResult { IsReady = true });
        var service = new DevelopmentGameHostWorkflowService(
            fixture.Preferences,
            launcher,
            readiness,
            TimeSpan.FromSeconds(1));
        var request = new DevelopmentGameHostLaunchRequest
        {
            ProjectFilePath = fixture.ProjectFilePath,
            RuntimeProjectPath = fixture.RuntimeProjectPath
        };

        var first = await service.LaunchAsync(request);
        var second = await service.LaunchAsync(request);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(2, launcher.Started.Count);
        Assert.NotEqual(first.RegistrationFilePath, second.RegistrationFilePath);
        Assert.True(firstProcess.KillCalled);
        Assert.False(secondProcess.KillCalled);

        service.Stop();
        Assert.True(secondProcess.KillCalled);
    }

    private sealed class LaunchFixture : IDisposable
    {
        private LaunchFixture(string root)
        {
            Root = root;
            ProjectFilePath = Path.Combine(root, "Project Folder With Spaces", "Example.sbe.json");
            RuntimeProjectPath = Path.Combine(root, "Project Folder With Spaces", "GameRuntimeJson", "Example.sbr.runtime.json");
            HostExecutablePath = Path.Combine(root, "GameHost", "Storyboard.GameHost.exe");
            WebPortalRoot = Path.Combine(root, "WebPortal Dist");
            Directory.CreateDirectory(Path.GetDirectoryName(ProjectFilePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(RuntimeProjectPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(HostExecutablePath)!);
            Directory.CreateDirectory(WebPortalRoot);
            File.WriteAllText(ProjectFilePath, "{}");
            File.WriteAllText(RuntimeProjectPath, "{}");
            File.WriteAllText(HostExecutablePath, string.Empty);
            File.WriteAllText(Path.Combine(WebPortalRoot, "index.html"), "<html />");
            Preferences = new FakePreferences
            {
                GameHostExecutablePath = HostExecutablePath,
                WebPortalRootPath = WebPortalRoot,
                DevelopmentUsername = "designer-dev"
            };
        }

        public string Root { get; }
        public string ProjectFilePath { get; }
        public string RuntimeProjectPath { get; }
        public string HostExecutablePath { get; }
        public string WebPortalRoot { get; }
        public FakePreferences Preferences { get; }

        public static LaunchFixture Create() => new(Path.Combine(Path.GetTempPath(), "StoryboardDesignerTests", Guid.NewGuid().ToString("N")));

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch
            {
                // Best effort test cleanup.
            }
        }
    }

    private sealed class FakePreferences : IProjectCreationPreferencesService
    {
        public string GameHostExecutablePath { get; init; } = string.Empty;
        public string WebPortalRootPath { get; init; } = string.Empty;
        public string DevelopmentUsername { get; init; } = DevelopmentLaunchRegistration.DefaultDevelopmentUsername;
        public ProjectCreationPreferences Load() => new()
        {
            GameHostExecutablePath = GameHostExecutablePath,
            WebPortalRootPath = WebPortalRootPath,
            DevelopmentUsername = DevelopmentUsername
        };

        public void Save(ProjectCreationPreferences preferences)
        {
        }
    }

    private sealed class FakeProcessLauncher : IProcessLauncher
    {
        private readonly IReadOnlyList<IManagedProcess> _processes;
        public List<ProcessStartInfo> Started { get; } = [];

        public FakeProcessLauncher(params IManagedProcess[] processes)
        {
            _processes = processes;
        }

        public IManagedProcess Start(ProcessStartInfo startInfo)
        {
            Started.Add(startInfo);
            return _processes[Started.Count - 1];
        }
    }

    private sealed class FakeManagedProcess : IManagedProcess
    {
        public FakeManagedProcess(int processId)
        {
            ProcessId = processId;
        }

        public int? ProcessId { get; }
        public bool HasExited { get; private set; }
        public bool KillCalled { get; private set; }

        public void Kill()
        {
            KillCalled = true;
            HasExited = true;
        }
    }

    private sealed class FakeReadinessProbe : IHostReadinessProbe
    {
        private readonly HostReadinessResult _result;

        public FakeReadinessProbe(HostReadinessResult result)
        {
            _result = result;
        }

        public Uri? LastHostUri { get; private set; }

        public Task<HostReadinessResult> WaitUntilReadyAsync(
            Uri hostUri,
            Func<bool> isProcessExited,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            LastHostUri = hostUri;
            return Task.FromResult(_result);
        }
    }
}
