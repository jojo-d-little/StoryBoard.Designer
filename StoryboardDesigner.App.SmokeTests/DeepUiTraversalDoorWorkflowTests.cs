using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using Storyboard.Shared.HostContracts;
using Storyboard.Shared.HostContracts.Transport;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using Xunit.Abstractions;

namespace StoryboardDesigner.App.SmokeTests;

[Collection("Designer UI Smoke")]
public sealed class DeepUiTraversalDoorWorkflowTests(ITestOutputHelper output)
{
    [DeepUiFact]
    [Trait("Category", "DeepUi")]
    public void DevelopmentWebPortal_RealProducer_RelaunchesLatestExportAndBrowserSession()
    {
        StaTestRunner.Run(() =>
        {
            var siblings = Directory.GetParent(AppExecutableLocator.ResolveSolutionRoot())!.FullName;
            var hostPath = Environment.GetEnvironmentVariable("STORYBOARD_GAMEHOST_EXECUTABLE_PATH")
                ?? Path.Combine(siblings, "StoryBoard.GameEngine", "Storyboard.GameHost", "bin", "Debug", "net8.0", "Storyboard.GameHost.exe");
            var portalRoot = Environment.GetEnvironmentVariable("STORYBOARD_WEBPORTAL_ROOT")
                ?? Path.Combine(siblings, "StoryBoard.WebPortal", "Storyboard.WebPortal", "dist");
            Assert.True(File.Exists(hostPath), $"Build/install GameHost or set STORYBOARD_GAMEHOST_EXECUTABLE_PATH: {hostPath}");
            Assert.True(File.Exists(Path.Combine(portalRoot, "index.html")), $"Build/install Portal or set STORYBOARD_WEBPORTAL_ROOT: {portalRoot}");
            var playwrightModule = Environment.GetEnvironmentVariable("STORYBOARD_DESIGNER_PLAYWRIGHT_MODULE")
                ?? Path.Combine(siblings, "StoryBoard.WebPortal", "Storyboard.WebPortal", "node_modules", "playwright");
            Assert.True(File.Exists(Path.Combine(playwrightModule, "package.json")), $"Install Playwright/Chromium or set STORYBOARD_DESIGNER_PLAYWRIGHT_MODULE: {playwrightModule}");
            var evidenceRoot = Path.Combine(AppExecutableLocator.ResolveSolutionRoot(), "StoryboardDesigner.App.SmokeTests", "TestResults", "retirement-browser", Guid.NewGuid().ToString("N"));
            output.WriteLine($"Browser evidence: {evidenceRoot}");
            var root = Path.Combine(Path.GetTempPath(), "Designer retirement real producer", Guid.NewGuid().ToString("N"));
            var isolatedHost = Path.Combine(root, "Installed Host");
            CopyHost(Path.GetDirectoryName(hostPath)!, isolatedHost);
            var catalogPath = Path.Combine(isolatedHost, "Config", "runtime-game-registrations.json");
            var catalogBefore = File.ReadAllBytes(catalogPath);
            output.WriteLine($"Fixture: {root}");
            output.WriteLine($"Host source: {hostPath}; SHA256: {Hash(File.ReadAllBytes(hostPath))}");
            output.WriteLine($"Portal: {portalRoot}; index SHA256: {Hash(File.ReadAllBytes(Path.Combine(portalRoot, "index.html")))}");
            var preferences = new ProjectCreationPreferencesService();
            preferences.Save(new ProjectCreationPreferences
            {
                GameHostExecutablePath = Path.Combine(isolatedHost, Path.GetFileName(hostPath)),
                WebPortalRootPath = portalRoot, DevelopmentUsername = "dev",
                SimulatorExecutablePath = "retired simulator does not exist.exe"
            });
            var workflow = new ObservedWorkflow(new DevelopmentGameHostWorkflowService(preferences));
            try
            {
                foreach (var legacyReplay in new[] { false, true })
                {
                    var export = new JsonExportService();
                    var projectPath = CreateProject(export, root, legacyReplay);
                    var ui = new LaunchUi(playwrightModule, Path.Combine(evidenceRoot, legacyReplay ? "legacy" : "fresh"));
                    var vm = new MainWindowViewModel(export, new RecentProjectsService(), new MainWindowDialogWorkflowService(), ui,
                        new RoomDesignerDirectionImageDialogService(), new TreeContextInteractionService(),
                        developmentGameHostWorkflowService: workflow, projectCreationPreferencesService: preferences);
                    Assert.True(vm.OpenProject(projectPath));
                    Guid? previousSession = null;
                    DevelopmentGameHostLaunchResult? previousLaunch = null;
                    for (var edit = 1; edit <= 2; edit++)
                    {
                        var title = $"Retirement {(legacyReplay ? "legacy" : "fresh")} edit {edit}";
                        // Exercise the actual authoring model, command, validation, exporter and Host service.
                        vm.Project.Name = title;
                        vm.Project.Planets.Single().Countries.Single().Areas.Single().Rooms.Single().AvailableActions.Single().EchoMessage = title;
                        ui.ExpectedText = title;
                        vm.NotifyProjectEdited();
                        vm.RunDevelopmentCommand.Execute(null);
                        PumpUntil(() => vm.RunDevelopmentCommand.CanExecute(null), TimeSpan.FromSeconds(35));
                        Assert.True(ui.Warnings.Count == 0, string.Join(Environment.NewLine, ui.Warnings));
                        Assert.Contains("Development WebPortal launched", vm.ExportStatus);
                        output.WriteLine(ui.LastBrowserEvidence);
                        var launch = Assert.IsType<DevelopmentGameHostLaunchResult>(workflow.LastResult);
                        Assert.True(launch.Success);
                        Assert.Equal("dev", launch.DevelopmentUsername);
                        var identity = DevelopmentLaunchRegistration.CreateIdentity(projectPath);
                        Assert.Equal(identity.GameId, launch.GameId);
                        Assert.Equal(identity.GameKey, launch.GameKey);
                        using var registration = JsonDocument.Parse(File.ReadAllText(launch.RegistrationFilePath!));
                        var runtimePath = registration.RootElement.GetProperty("games")[0].GetProperty("runtimeProjectPath").GetString()!;
                        Assert.Contains("Runtime Output With Spaces", runtimePath);
                        using var runtime = JsonDocument.Parse(File.ReadAllText(runtimePath));
                        Assert.Equal(title, runtime.RootElement.GetProperty("name").GetString());
                        using var http = new HttpClient { BaseAddress = launch.HostUri, Timeout = TimeSpan.FromSeconds(5) };
                        var portalHtml = http.GetStringAsync("/client/").GetAwaiter().GetResult();
                        Assert.Contains("Storyboard Web Portal", portalHtml);
                        var script = Regex.Match(portalHtml, "<script[^>]+src=\"([^\"]+)\"").Groups[1].Value;
                        Assert.NotEmpty(script);
                        Assert.NotEmpty(http.GetStringAsync(new Uri(launch.BrowserUri!, script)).GetAwaiter().GetResult());
                        var auth = Post<HostAuthenticateResult>(http, HostTransportRoutes.Identity.Authenticate,
                            new HostAuthenticateRequest { Context = Context(), Username = "dev" });
                        Assert.True(auth.Result.Success, JsonSerializer.Serialize(auth.Result));
                        var games = Post<HostDiscoverGamesResult>(http, HostTransportRoutes.Discovery.DiscoverGames,
                            new HostDiscoverGamesRequest { Context = Context(auth.CredentialHandle), MaxItems = 10 });
                        Assert.True(games.Result.Success, JsonSerializer.Serialize(games.Result));
                        var game = Assert.Single(games.Games);
                        Assert.Equal(title, game.DisplayName);
                        Assert.Equal(identity.GameId, game.GameId);
                        Assert.Equal(identity.GameKey, game.GameKey);

                        // Only the real browser/Portal creates a session; this test never calls StartSession.
                        HostListSessionsResult? sessions = null;
                        PumpUntil(() =>
                        {
                            sessions = Post<HostListSessionsResult>(http, HostTransportRoutes.Session.List,
                                new HostListSessionsRequest { Context = Context(auth.CredentialHandle), GameId = game.GameId, MaxItems = 10 });
                            return sessions.Result.Success && sessions.Sessions.Count > 0;
                        }, TimeSpan.FromSeconds(35));
                        var session = Assert.Single(sessions!.Sessions);
                        Assert.Equal(game.GameId, session.GameId);
                        Assert.Equal(game.GameKey, session.GameKey);
                        Assert.NotEqual(previousSession, session.SessionId);
                        var assetPath = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(runtimePath)!, "*.png", SearchOption.AllDirectories));
                        var assetContext = Context(auth.CredentialHandle);
                        assetContext.SessionId = session.SessionId;
                        var asset = Post<HostGetAssetResult>(http, HostTransportRoutes.Assets.Get, new HostGetAssetRequest
                        {
                            Context = assetContext, GameId = game.GameId, GameKey = game.GameKey,
                            RelativeLocator = Path.GetRelativePath(Path.GetDirectoryName(runtimePath)!, assetPath).Replace('\\', '/')
                        });
                        Assert.True(asset.Result.Success, JsonSerializer.Serialize(asset.Result));
                        Assert.Equal(File.ReadAllBytes(assetPath), asset.PayloadBytes);
                        output.WriteLine($"{title}: Host={launch.HostUri}; game={game.GameId}/{game.GameKey}; Portal session={session.SessionId}; runtime={runtimePath}; registration={launch.RegistrationFilePath}");
                        if (previousLaunch is not null)
                        {
                            Assert.False(File.Exists(previousLaunch.RegistrationFilePath));
                            AssertHostStopped(previousLaunch.HostUri!);
                        }
                        previousSession = session.SessionId;
                        previousLaunch = launch;
                    }
                    var loaded = export.TryLoadProjectModel(projectPath)!;
                    Assert.Equal(legacyReplay ? "missing legacy recording.sbe.sim.json" : string.Empty, loaded.SimulatorReplayFilePath);
                    Assert.Equal(legacyReplay ? 1.75 : null, loaded.SimulatorReplaySpeed);
                    vm.ShutdownDevelopmentLaunch();
                    Assert.False(File.Exists(previousLaunch!.RegistrationFilePath));
                    AssertHostStopped(previousLaunch.HostUri!);
                }
                Assert.Equal(catalogBefore, File.ReadAllBytes(catalogPath));
                Assert.Equal("retired simulator does not exist.exe", preferences.Load().SimulatorExecutablePath);
                output.WriteLine($"Durable catalog unchanged: SHA256 {Hash(catalogBefore)}. All four managed launches stopped and registrations removed.");
            }
            finally
            {
                workflow.Stop();
                try { Directory.Delete(root, recursive: true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    output.WriteLine($"Temporary fixture retained after cleanup: {root}; {ex.Message}");
                }
            }
        });
    }

    private static string CreateProject(JsonExportService export, string root, bool legacyReplay)
    {
        var path = export.CreateProjectSkeleton(root, legacyReplay ? "Legacy project" : "Fresh project");
        var imagePath = Path.Combine(Path.GetDirectoryName(path)!, "assets", "retirement.png");
        File.WriteAllBytes(imagePath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII="));
        var room = new Room
        {
            Name = "Room",
            AvailableActions = [new CommandAction { Name = "Look", ActionType = CommandActionType.EchoMessage, Verbs = ["look"], EchoMessage = "Initial content" }],
            Images = [new RoomImageEntry { Slot = RoomImageSlot.Default, Image = new RoomImageVariant { FullImagePath = imagePath } }]
        };
        var area = new Area { Name = "Area", Rooms = [room], StartingRoomId = room.Id };
        var country = new Country { Name = "Country", Areas = [area], StartingAreaName = area.Name };
        var planet = new Planet { Name = "Planet", Countries = [country], StartingCountryName = country.Name };
        var player = new GameObject
        {
            Name = "Player", IsContainer = true, ContainerPointsDefaultValue = 5,
            Variables = [new GamePropertyDefinition
            {
                Name = "isPlayer", DefaultValue = "true", Lifetime = GamePropertyLifetime.Singleton,
                ValueRestriction = GamePropertyValueRestriction.TrueFalse
            }]
        };
        export.SaveProjectModel(path, new ProjectModel
        {
            Name = "Retirement", StartingPlanetName = planet.Name, PlayerCharacterObjectName = player.Name,
            CommandVerbs = ["look"],
            Planets = [planet], GameObjects = [player], RuntimeExportDirectory = "../Runtime Output With Spaces " + legacyReplay,
            SimulatorReplayFilePath = legacyReplay ? "missing legacy recording.sbe.sim.json" : string.Empty,
            SimulatorReplaySpeed = legacyReplay ? 1.75 : null
        });
        return path;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static HostRequestContext Context(string handle = "") => new()
    {
        RequestId = Guid.NewGuid(), PrincipalHandle = handle, TenantId = "local-tenant", OrgId = "local-org"
    };

    private static T Post<T>(HttpClient http, string route, object request)
    {
        using var response = http.PostAsJsonAsync(route, request).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        return response.Content.ReadFromJsonAsync<T>().GetAwaiter().GetResult()!;
    }

    private static void AssertHostStopped(Uri uri)
    {
        PumpUntil(() => !IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(endpoint => endpoint.Port == uri.Port),
            TimeSpan.FromSeconds(5));
    }

    private static void PumpUntil(Func<bool> completed, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!completed() && DateTime.UtcNow < deadline)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(150) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }
        Assert.True(completed(), "Timed out waiting for the development launch or browser-created Portal session.");
    }

    private static void CopyHost(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var directory in Directory.GetDirectories(source))
        {
            var name = Path.GetFileName(directory);
            if (name is not ("Logs" or "artifacts")) CopyHost(directory, Path.Combine(destination, name));
        }
    }

    private sealed class ObservedWorkflow(IDevelopmentGameHostWorkflowService inner) : IDevelopmentGameHostWorkflowService
    {
        internal DevelopmentGameHostLaunchResult? LastResult { get; private set; }
        public async Task<DevelopmentGameHostLaunchResult> LaunchAsync(DevelopmentGameHostLaunchRequest request, CancellationToken cancellationToken = default)
            => LastResult = await inner.LaunchAsync(request, cancellationToken);
        public void Stop() => inner.Stop();
    }

    private sealed class LaunchUi(string playwrightModule, string evidenceDirectory) : IProjectUiService
    {
        private int _browserCount;
        internal string ExpectedText { get; set; } = string.Empty;
        internal string LastBrowserEvidence { get; private set; } = string.Empty;
        internal List<string> Warnings { get; } = [];
        public bool TryOpenUriInDefaultBrowser(string uri, out string errorMessage)
        {
            // Keep production browser behavior unchanged; exercise the same launch URI in an isolated real browser.
            var start = new ProcessStartInfo("node")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "DevelopmentPortalBrowserProbe.cjs"));
            start.ArgumentList.Add(playwrightModule);
            start.ArgumentList.Add(uri);
            start.ArgumentList.Add(Path.Combine(evidenceDirectory, (++_browserCount).ToString()));
            start.ArgumentList.Add(ExpectedText);
            using var browserProbe = Process.Start(start)!;
            var stdout = browserProbe.StandardOutput.ReadToEndAsync();
            var stderr = browserProbe.StandardError.ReadToEndAsync();
            if (!browserProbe.WaitForExit(45000))
            {
                browserProbe.Kill(entireProcessTree: true);
                errorMessage = "Portal browser probe timed out.";
                return false;
            }
            LastBrowserEvidence = stdout.GetAwaiter().GetResult();
            errorMessage = stderr.GetAwaiter().GetResult();
            return browserProbe.ExitCode == 0;
        }
        public void ShowWarning(string message, string title) => Warnings.Add($"{title}: {message}");
        public void ShowError(string message, string title) => Warnings.Add($"{title}: {message}");
        public void ShowInformation(string message, string title) { }
        public bool Confirm(string message, string title) => true;
        public bool ConfirmContinueWithValidationSummary(string workflowName, int issueCount, int errorCount, int warningCount) => true;
        public bool ConfirmContinueSaveWithValidation(int errorCount, int warningCount, string reportFilePath) => true;
        public void ShowValidationReport(string reportTitle, IReadOnlyList<ProjectValidationIssue> issues, string reportFilePath, Action<ProjectValidationIssue>? navigateToIssue = null) { }
        public CreateProjectDialogRequest BuildCreateProjectDialogRequest() => throw new NotSupportedException();
        public bool TryGetCreateProjectInput(CreateProjectDialogRequest request, out CreateProjectDialogResult result) => throw new NotSupportedException();
        public void SaveCreateProjectDialogPreferences(CreateProjectDialogRequest request, CreateProjectDialogResult result) => throw new NotSupportedException();
        public bool TryGetSaveAsProjectInput(out string projectFolder, out string projectName) => throw new NotSupportedException();
        public bool TryGetOpenProjectPath(out string projectFilePath) => throw new NotSupportedException();
        public bool TryGetImportGlobalNodeProjectPath(out string projectFilePath) => throw new NotSupportedException();
        public bool TryGetImportGlobalNodeSelection(GlobalImportSelectionRequest request, out GlobalImportSelectionResult selection) => throw new NotSupportedException();
        public bool TryGetValidationRunSelection(ValidationRunDialogRequest request, out ValidationRunDialogResult selection) => throw new NotSupportedException();
        public AutoSaveBeforeGameStateInitResponse PromptAutoSaveBeforeGameStateInitialization() => throw new NotSupportedException();
        public bool TryGetRuntimeVariableValue(string variableName, string currentValue, out string value) => throw new NotSupportedException();
        public bool TryGetSaveOutputLogPath(out string filePath) => throw new NotSupportedException();
        public bool TryRunSourceImageManagement(SourceImageManagementDialogRequest request, out SourceImageManagementDialogResult result) => throw new NotSupportedException();
        public bool TryOpenTextFileInDefaultEditor(string filePath, out string errorMessage) => throw new NotSupportedException();
        public bool TryGetDefaultEchoMessagesRefreshTarget(bool projectTargetAvailable, out DefaultEchoMessagesRefreshTarget target) => throw new NotSupportedException();
    }
}
