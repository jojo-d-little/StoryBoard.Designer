using System.Reflection;
using System.Text.Json;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelDevelopmentLaunchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RunDevelopment_SavesAndExportsLatestEdits_PreservingLegacyReplayData(bool legacyReplay)
    {
        using var fixture = new ProducerFixture(legacyReplay);
        var observedNames = new List<string>();
        fixture.Workflow.OnLaunch = request =>
        {
            Assert.Equal(fixture.ProjectPath, request.ProjectFilePath);
            Assert.Contains("Runtime Output With Spaces", request.RuntimeProjectPath);
            using var runtime = JsonDocument.Parse(File.ReadAllText(request.RuntimeProjectPath));
            observedNames.Add(runtime.RootElement.GetProperty("name").GetString()!);
            return Task.FromResult(ReadyResult());
        };

        fixture.ViewModel.Project.Name = "First edit";
        fixture.ViewModel.NotifyProjectEdited();
        fixture.ViewModel.RunDevelopmentCommand.Execute(null);
        Assert.Contains("Development WebPortal launched", fixture.ViewModel.ExportStatus);

        fixture.ViewModel.Project.Name = "Second edit";
        fixture.ViewModel.NotifyProjectEdited();
        fixture.ViewModel.RunDevelopmentCommand.Execute(null);

        Assert.Equal(new[] { "First edit", "Second edit" }, observedNames);
        Assert.Empty(fixture.Ui.WarningMessages);
        Assert.Contains("mode=devsimulator", fixture.Ui.LastOpenedUri);
        var reloaded = new JsonExportService().TryLoadProjectModel(fixture.ProjectPath)!;
        Assert.Equal("Second edit", reloaded.Name);
        Assert.Equal(legacyReplay ? "missing legacy recording.sbe.sim.json" : string.Empty, reloaded.SimulatorReplayFilePath);
        Assert.Equal(legacyReplay ? 1.75 : null, reloaded.SimulatorReplaySpeed);
        fixture.ViewModel.ShutdownDevelopmentLaunch();
        Assert.Equal(1, fixture.Workflow.StopCount);
    }

    [Fact]
    public void RunDevelopment_UnsavedProject_DoesNotLaunch()
    {
        var workflow = new WorkflowStub();
        var ui = new ProjectUiServiceStub();
        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(
            new ProjectModel(), new TreeContextInteractionServiceStub(), ui, developmentWorkflow: workflow);

        bundle.ViewModel.RunDevelopmentCommand.Execute(null);

        Assert.Equal(0, workflow.LaunchCount);
        Assert.Contains(ui.WarningMessages, message => message.Contains("Save the project before launching WebPortal."));
    }

    [Fact]
    public void RunDevelopment_BlockingValidation_DoesNotLaunch()
    {
        var workflow = new WorkflowStub();
        var ui = new ProjectUiServiceStub { ContinueSaveWithValidation = true };
        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(
            new ProjectModel(), new TreeContextInteractionServiceStub(), ui, developmentWorkflow: workflow);
        typeof(MainWindowViewModel).GetField("_projectFilePath", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(bundle.ViewModel, Path.Combine(Path.GetTempPath(), "invalid.sbe.json"));

        bundle.ViewModel.RunDevelopmentCommand.Execute(null);

        Assert.Equal(0, workflow.LaunchCount);
        Assert.NotEmpty(ui.WarningMessages);
        Assert.True(bundle.ViewModel.RunDevelopmentCommand.CanExecute(null));
    }

    [Fact]
    public void RunDevelopment_BrowserFailure_StopsHostAndReportsDetails()
    {
        using var fixture = new ProducerFixture(false);
        fixture.Ui.OpenUriInDefaultBrowserResult = false;
        fixture.Ui.OpenUriInDefaultBrowserErrorMessage = "No browser association";

        fixture.ViewModel.RunDevelopmentCommand.Execute(null);

        Assert.Equal(1, fixture.Workflow.LaunchCount);
        Assert.Equal(1, fixture.Workflow.StopCount);
        Assert.Contains(fixture.Ui.WarningMessages, message => message.Contains("No browser association"));
        Assert.True(fixture.ViewModel.RunDevelopmentCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RunDevelopment_FailureOrException_RestoresCommandAndReportsDetails(bool throws)
    {
        using var fixture = new ProducerFixture(false);
        fixture.Workflow.OnLaunch = _ => throws
            ? throw new IOException("Host launch fault")
            : Task.FromResult(new DevelopmentGameHostLaunchResult
            {
                Success = false,
                StatusMessage = "Host not ready",
                Diagnostics = ["Host launch fault"]
            });

        fixture.ViewModel.RunDevelopmentCommand.Execute(null);

        Assert.Equal(string.Empty, fixture.Ui.LastOpenedUri);
        Assert.Contains(fixture.Ui.WarningMessages, message => message.Contains("Host launch fault"));
        Assert.Equal(throws ? 1 : 0, fixture.Workflow.StopCount);
        Assert.True(fixture.ViewModel.RunDevelopmentCommand.CanExecute(null));
    }

    [Fact]
    public void RunDevelopment_WhileLaunching_IsDisabledUntilCompletion()
    {
        using var fixture = new ProducerFixture(false);
        var pending = new TaskCompletionSource<DevelopmentGameHostLaunchResult>();
        fixture.Workflow.OnLaunch = _ => pending.Task;
        fixture.ViewModel.RunDevelopmentCommand.Execute(null);
        Assert.False(fixture.ViewModel.RunDevelopmentCommand.CanExecute(null));
        pending.SetResult(ReadyResult());
        Assert.True(SpinWait.SpinUntil(() => fixture.ViewModel.RunDevelopmentCommand.CanExecute(null), TimeSpan.FromSeconds(5)));
        Assert.Equal(1, fixture.Workflow.LaunchCount);
    }

    private static DevelopmentGameHostLaunchResult ReadyResult() => new()
    {
        Success = true,
        BrowserUri = new Uri("http://127.0.0.1:4567/client/?mode=devsimulator&username=dev&autoStartSession=true"),
        GameKey = "designer.test"
    };

    private sealed class WorkflowStub : IDevelopmentGameHostWorkflowService
    {
        internal int LaunchCount { get; private set; }
        internal int StopCount { get; private set; }
        internal Func<DevelopmentGameHostLaunchRequest, Task<DevelopmentGameHostLaunchResult>> OnLaunch { get; set; }
            = _ => Task.FromResult(ReadyResult());

        public Task<DevelopmentGameHostLaunchResult> LaunchAsync(DevelopmentGameHostLaunchRequest request, CancellationToken cancellationToken = default)
        {
            LaunchCount++;
            return OnLaunch(request);
        }

        public void Stop() => StopCount++;
    }

    private sealed class ProducerFixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "Designer retirement producer", Guid.NewGuid().ToString("N"));
        internal string ProjectPath { get; }
        internal WorkflowStub Workflow { get; } = new();
        internal ProjectUiServiceStub Ui { get; } = new() { ContinueSaveWithValidation = true };
        internal MainWindowViewModel ViewModel { get; }

        internal ProducerFixture(bool legacyReplay)
        {
            var export = new JsonExportService();
            ProjectPath = export.CreateProjectSkeleton(Root, "Retirement");
            var room = new Room { Name = "Room" };
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
            export.SaveProjectModel(ProjectPath, new ProjectModel
            {
                Name = "Retirement", StartingPlanetName = planet.Name, PlayerCharacterObjectName = player.Name,
                GameObjects = [player], Planets = [planet], RuntimeExportDirectory = "../Runtime Output With Spaces",
                SimulatorReplayFilePath = legacyReplay ? "missing legacy recording.sbe.sim.json" : string.Empty,
                SimulatorReplaySpeed = legacyReplay ? 1.75 : null
            });
            ViewModel = new MainWindowViewModel(
                export, new RecentProjectsServiceStub(), new MainWindowDialogWorkflowServiceStub(), Ui,
                new RoomDesignerDirectionImageDialogServiceStub(), new TreeContextInteractionServiceStub(),
                developmentGameHostWorkflowService: Workflow);
            Assert.True(ViewModel.OpenProject(ProjectPath));
        }

        public void Dispose()
        {
            ViewModel.PersistUiStateSnapshot();
            Directory.Delete(Root, recursive: true);
        }
    }
}
