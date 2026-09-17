using System.Text;
using System.Text.Json;
using Storyboard.Shared.GameManager;
using Storyboard.Shared.GameServices;
using Storyboard.Shared.GameStateData;
using Storyboard.Shared.HostContracts;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using Xunit.Sdk;

namespace StoryboardDesigner.App.Tests;

public sealed class GameSimulatorPlaybackRegressionTests
{
    private static readonly bool UpdatePlaybackSnapshots = string.Equals(
        Environment.GetEnvironmentVariable("UPDATE_PLAYBACK_SNAPSHOTS"),
        "1",
        StringComparison.OrdinalIgnoreCase);

    public static IEnumerable<object[]> PlaybackCases()
    {
        var definitions = LoadDefinitions();
        foreach (var definition in definitions)
        {
            yield return new object[] { definition };
        }
    }

    [Theory]
    [MemberData(nameof(PlaybackCases))]
    public void Replay_RecordedSession_OutputLinesMatchAtEachStep(PlaybackCaseDefinition definition)
    {
        var manager = CreateGameManager();
        var polling = Assert.IsAssignableFrom<ISessionDeltaPolling>(manager);
        var loadResult = Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager).LoadGameRuntimeProject(definition.ProjectFile);
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var recording = LoadRecording(definition.ReplayFile);
        Assert.NotEmpty(recording.Steps);

        var updatedSteps = new List<RecordingStep>();
        var context = new HostRequestContext
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid().ToString("N"),
            ClientSentUtc = DateTimeOffset.UtcNow
        };
        var currentWatermark = polling.GetSessionDeltas(context, watermark: null, HostSessionDeltaBatchProfile.Medium).SessionDeltaWatermark;

        foreach (var step in recording.Steps.OrderBy(static s => s.Sequence))
        {
            var playbackRequest = BuildPlaybackRequest(step);
            var result = playbackRequest is null
                ? manager.ProcessCommand(context, step.CommandText, diagnosticsLevel: GameDiagnosticsLevel.None)
                : manager.ProcessCommand(context, playbackRequest);
            var actualSuccess = result.Success;

            var delta = polling.GetSessionDeltas(context, currentWatermark, HostSessionDeltaBatchProfile.Medium);
            currentWatermark = delta.SessionDeltaWatermark;
            var actualOutputLines = delta.SessionData?.OutputLines ?? [];
            var actualDiagnostics = result.Diagnostics
                .Concat(delta.SessionData?.Diagnostics ?? [])
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var actualMoveLegTelemetry = ExtractMoveLegTelemetry(delta.SessionData);

            if (UpdatePlaybackSnapshots)
            {
                updatedSteps.Add(step with
                {
                    OutputLines = actualOutputLines.ToList(),
                    Diagnostics = actualDiagnostics.ToList(),
                    MoveLegTelemetry = actualMoveLegTelemetry.ToList(),
                    Success = actualSuccess
                });

                continue;
            }

            if (!actualOutputLines.SequenceEqual(step.OutputLines, StringComparer.Ordinal))
            {
                var replayProbe = new List<string>
                {
                    $"initialPoll: result={delta.ResultCode}; watermark={currentWatermark ?? "(null)"}; diagnostics={delta.Diagnostics.Count}"
                };

                for (var diagnosticIndex = 0; diagnosticIndex < delta.Diagnostics.Count; diagnosticIndex++)
                {
                    replayProbe.Add($"initialPoll.diagnostic[{diagnosticIndex + 1}] {delta.Diagnostics[diagnosticIndex]}");
                }

                replayProbe.AddRange(ProbeAdditionalDeltaPolls(
                    polling,
                    context,
                    currentWatermark,
                    maxPolls: 3));

                throw new XunitException(BuildStepMismatchMessage(
                    "Output lines",
                    definition,
                    step,
                    actualOutputLines,
                    actualMoveLegTelemetry,
                    actualDiagnostics,
                    actualSuccess,
                    replayProbe));
            }

            if (definition.Strict && actualSuccess != step.Success)
            {
                throw new XunitException(BuildStepMismatchMessage(
                    "Success flag",
                    definition,
                    step,
                    actualOutputLines,
                    actualMoveLegTelemetry,
                    actualDiagnostics,
                    actualSuccess));
            }

            if (definition.Strict && !actualDiagnostics.SequenceEqual(step.Diagnostics, StringComparer.Ordinal))
            {
                var expectedDiagnostics = NormalizeDiagnosticsForComparison(step.Diagnostics);
                var normalizedActualDiagnostics = NormalizeDiagnosticsForComparison(actualDiagnostics);
                if (normalizedActualDiagnostics.SequenceEqual(expectedDiagnostics, StringComparer.Ordinal))
                {
                    continue;
                }

                throw new XunitException(BuildStepMismatchMessage(
                    "Diagnostics",
                    definition,
                    step,
                    actualOutputLines,
                    actualMoveLegTelemetry,
                    normalizedActualDiagnostics,
                    actualSuccess));
            }

            if (definition.Strict && !MoveLegTelemetrySequenceEqual(actualMoveLegTelemetry, step.MoveLegTelemetry))
            {
                throw new XunitException(BuildStepMismatchMessage(
                    "Move-leg telemetry",
                    definition,
                    step,
                    actualOutputLines,
                    actualMoveLegTelemetry,
                    actualDiagnostics,
                    actualSuccess));
            }
        }

        if (UpdatePlaybackSnapshots)
        {
            var updatedRecording = recording with
            {
                CreatedAtUtc = DateTime.UtcNow.ToString("O"),
                Steps = updatedSteps
            };
            SaveRecording(definition.ReplayFile, updatedRecording);
        }
    }

    [Fact]
    public void Replay_VerticalTraversal_InMemoryScenario_OutputLinesMatchAtEachStep()
    {
        var manager = CreateGameManager();
        var debugger = Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager);
        var loadResult = debugger.LoadRuntimeSnapshot(CreateVerticalReplaySnapshot(), "in-memory-vertical-replay");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var polling = Assert.IsAssignableFrom<ISessionDeltaPolling>(manager);
        var context = new HostRequestContext
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid().ToString("N"),
            ClientSentUtc = DateTimeOffset.UtcNow
        };

        var baseline = polling.GetSessionBaseline(context, GameDiagnosticsLevel.None);
        var watermark = baseline.SessionDeltaWatermark;

        var steps = new[]
        {
            new { Command = "go up", ExpectedRoomName = "Upper" },
            new { Command = "go down", ExpectedRoomName = "Ground" }
        };

        foreach (var step in steps)
        {
            var result = manager.ProcessCommand(context, step.Command, diagnosticsLevel: GameDiagnosticsLevel.None);
            Assert.True(result.Success, string.Join(" | ", result.Diagnostics));

            var delta = polling.GetSessionDeltas(context, watermark, HostSessionDeltaBatchProfile.Medium);
            watermark = delta.SessionDeltaWatermark;
            Assert.Equal(HostSessionDeltaPollResultCode.Success, delta.ResultCode);

            var presentation = manager.GetCurrentPresentation(context, GameDiagnosticsLevel.None);
            Assert.Equal(step.ExpectedRoomName, presentation.RoomChange?.NewRoom?.Name);
        }
    }

    private static bool MoveLegTelemetrySequenceEqual(
        IReadOnlyList<HostCommandMoveLegTelemetry> actual,
        IReadOnlyList<HostCommandMoveLegTelemetry> expected)
    {
        if (actual.Count != expected.Count)
        {
            return false;
        }

        for (var i = 0; i < actual.Count; i++)
        {
            if (!MoveLegTelemetryEquals(actual[i], expected[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<HostCommandMoveLegTelemetry> ExtractMoveLegTelemetry(HostSessionDataEnvelope? sessionData)
    {
        if (sessionData is null || sessionData.RoomObjectChanges.Count == 0)
        {
            return [];
        }

        return sessionData.RoomObjectChanges
            .SelectMany(static change => change.MoveLegTelemetry ?? [])
            .ToList();
    }

    private static bool MoveLegTelemetryEquals(HostCommandMoveLegTelemetry left, HostCommandMoveLegTelemetry right)
    {
        return left.TargetObjectId == right.TargetObjectId
               && string.Equals(left.TargetObjectName, right.TargetObjectName, StringComparison.Ordinal)
               && left.LegIndex == right.LegIndex
               && string.Equals(left.RequestedDirection, right.RequestedDirection, StringComparison.Ordinal)
               && left.RequestedDistanceInCells == right.RequestedDistanceInCells
               && left.AppliedDistanceInCells == right.AppliedDistanceInCells
               && left.Success == right.Success
               && string.Equals(left.ResultCode, right.ResultCode, StringComparison.Ordinal)
               && left.FromX == right.FromX
               && left.FromY == right.FromY
               && left.ToX == right.ToX
               && left.ToY == right.ToY
               && left.TravelVisualizationMode == right.TravelVisualizationMode;
    }

    private static HostProcessCommandRequest? BuildPlaybackRequest(RecordingStep step)
    {
        var hasExplicitPlaybackRequest = step.RequestCommandCorrelationId.HasValue
            || !string.IsNullOrWhiteSpace(step.RequestRawCommandText)
            || step.RequestClarificationAnswers.Count > 0;
        if (!hasExplicitPlaybackRequest)
        {
            return null;
        }

        var rawCommandText = string.IsNullOrWhiteSpace(step.RequestRawCommandText)
            ? step.CommandText?.Trim() ?? string.Empty
            : step.RequestRawCommandText.Trim();
        var commandCorrelationId = step.RequestCommandCorrelationId ?? step.Sequence;

        var answers = step.RequestClarificationAnswers
            .Where(answer => !string.IsNullOrWhiteSpace(answer.SlotId)
                             && !string.IsNullOrWhiteSpace(answer.SelectedObjectScopeNodeId))
            .Select(static answer => (HostClarificationAnswer)new HostClarificationAnswer
            {
                SlotId = answer.SlotId,
                SelectedObjectScopeNodeId = answer.SelectedObjectScopeNodeId
            })
            .ToList();

        return new HostProcessCommandRequest
        {
            CommandCorrelationId = commandCorrelationId,
            RawCommandText = rawCommandText,
            DiagnosticsLevel = GameDiagnosticsLevel.None,
            ClarificationAnswers = answers
        };
    }

    private static string BuildStepMismatchMessage(
        string mismatchKind,
        PlaybackCaseDefinition definition,
        RecordingStep step,
        IReadOnlyList<string> actualOutputLines,
        IReadOnlyList<HostCommandMoveLegTelemetry> actualMoveLegTelemetry,
        IReadOnlyList<string> actualDiagnostics,
        bool actualSuccess,
        IReadOnlyList<string>? replayProbe = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Playback mismatch ({mismatchKind}) in case '{definition.Name}'.");
        builder.AppendLine($"Project: {definition.ProjectFile}");
        builder.AppendLine($"Replay: {definition.ReplayFile}");
        builder.AppendLine($"Strict mode: {definition.Strict}");
        builder.AppendLine($"Step sequence: {step.Sequence}");
        builder.AppendLine($"Command: {step.CommandText}");
        builder.AppendLine($"Recorded success: {step.Success}; actual success: {actualSuccess}");
        builder.AppendLine("Recorded output lines:");
        AppendLines(builder, step.OutputLines);
        builder.AppendLine("Actual output lines:");
        AppendLines(builder, actualOutputLines);
        builder.AppendLine("Recorded diagnostics:");
        AppendLines(builder, step.Diagnostics);
        builder.AppendLine("Actual diagnostics:");
        AppendLines(builder, actualDiagnostics);
        builder.AppendLine("Recorded move-leg telemetry:");
        AppendMoveLegTelemetry(builder, step.MoveLegTelemetry);
        builder.AppendLine("Actual move-leg telemetry:");
        AppendMoveLegTelemetry(builder, actualMoveLegTelemetry);
        if (replayProbe is { Count: > 0 })
        {
            builder.AppendLine("Additional delta-poll probe:");
            foreach (var line in replayProbe)
            {
                builder.AppendLine($"  {line}");
            }
        }
        return builder.ToString();
    }

    private static IReadOnlyList<string> ProbeAdditionalDeltaPolls(
        ISessionDeltaPolling polling,
        HostRequestContext context,
        string? startingWatermark,
        int maxPolls)
    {
        var lines = new List<string>();
        var watermark = startingWatermark;

        for (var index = 1; index <= Math.Max(1, maxPolls); index++)
        {
            var poll = polling.GetSessionDeltas(context, watermark, HostSessionDeltaBatchProfile.Medium);
            var nextWatermark = poll.SessionDeltaWatermark;
            var outputLines = poll.SessionData?.OutputLines ?? [];

            lines.Add(
                $"probe#{index}: result={poll.ResultCode}; watermark={watermark ?? "(null)"}->{nextWatermark ?? "(null)"}; outputs={outputLines.Count}; diagnostics={poll.Diagnostics.Count}");

            for (var diagnosticIndex = 0; diagnosticIndex < poll.Diagnostics.Count; diagnosticIndex++)
            {
                lines.Add($"probe#{index}.diagnostic[{diagnosticIndex + 1}] {poll.Diagnostics[diagnosticIndex]}");
            }

            for (var outputIndex = 0; outputIndex < outputLines.Count; outputIndex++)
            {
                lines.Add($"probe#{index}.output[{outputIndex + 1}] {outputLines[outputIndex]}");
            }

            watermark = nextWatermark;
        }

        return lines;
    }

    private static void AppendMoveLegTelemetry(StringBuilder builder, IReadOnlyList<HostCommandMoveLegTelemetry> telemetry)
    {
        if (telemetry.Count == 0)
        {
            builder.AppendLine("  (none)");
            return;
        }

        foreach (var leg in telemetry.OrderBy(static leg => leg.LegIndex))
        {
            builder.AppendLine(
                $"  [leg {leg.LegIndex + 1}] dir={leg.RequestedDirection}; req={leg.RequestedDistanceInCells}; app={leg.AppliedDistanceInCells}; success={leg.Success}; code={leg.ResultCode}; target={leg.TargetObjectName} ({leg.TargetObjectId?.ToString("D") ?? "(none)"})");
        }
    }

    private static void AppendLines(StringBuilder builder, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            builder.AppendLine("  (none)");
            return;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            builder.AppendLine($"  [{i + 1}] {lines[i]}");
        }
    }

    private static IReadOnlyList<string> NormalizeDiagnosticsForComparison(IReadOnlyList<string> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return diagnostics;
        }

        // Legacy recordings may include connector-frame diagnostics that are intentionally retired.
        // Timer completion diagnostics are asynchronous (think-loop pacing dependent) and are
        // validated by dedicated timer delta-polling contract tests.
        return diagnostics
            .Where(static diagnostic => !diagnostic.StartsWith("Resolved two-object frame '", StringComparison.Ordinal))
            .Where(static diagnostic => !diagnostic.StartsWith("timerCompletion:", StringComparison.Ordinal))
            .Where(static diagnostic => !diagnostic.StartsWith("StartTimer replaced existing timer '", StringComparison.Ordinal))
            .ToList();
    }

    private static SimulatorRecording LoadRecording(string recordingFilePath)
    {
        var json = File.ReadAllText(recordingFilePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<SimulatorRecording>(json, options)
            ?? throw new InvalidOperationException($"Unable to deserialize simulator recording '{recordingFilePath}'.");
    }

    private static void SaveRecording(string recordingFilePath, SimulatorRecording recording)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(recording, options);
        File.WriteAllText(recordingFilePath, json + Environment.NewLine);
    }

    private static IHostRuntimeCommandProcessorClient CreateGameManager()
    {
        IGameCommandPreprocessorService preprocessor = new GameCommandPreprocessorService();
        IActionScriptEvaluationService evaluator = new ActionScriptEvaluationService();
        IRuntimeCommandProcessorService processor = new GameCommandProcessorService(evaluator, preprocessor);
        IGameProjectRuntimeLoaderService loader = new GameProjectRuntimeLoaderService();
        IRuntimeLoadedGameLoader runtimeLoadedGameLoader = new CleanProjectRuntimeLoadedGameLoader(loader);
        return new Storyboard.Shared.GameManager.GameManager(runtimeLoadedGameLoader, processor);
    }

    private static RuntimeGameWorldSnapshot CreateVerticalReplaySnapshot()
    {
        var roomAId = Guid.NewGuid();
        var roomBId = Guid.NewGuid();

        var navigateAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "Go",
            ActionType = CommandActionType.NavigateDirection,
            NoVerbLinkage = false,
            Verbs = new[] { "go" },
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Moved"] = "moved {move.navigatedDirection} to {currentRoom.Name}"
            },
            NavigatePayload = new RuntimeNavigateActionPayload()
        };

        var roomA = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Ground",
            ScopeNameInGame: "Ground",
            ScopeNodeId: roomAId,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "ground" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: new[] { navigateAction },
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var roomB = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Upper",
            ScopeNameInGame: "Upper",
            ScopeNodeId: roomBId,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "upper" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: new[] { navigateAction },
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "Tower",
            ScopeNameInGame: "Tower",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "tower" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: new[] { "up", "down" },
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { roomA, roomB },
            AdditionalDirectionalTraversalMappings: new[]
            {
                new RuntimeDirectionalTraversalMapping("up", Direction10.Up),
                new RuntimeDirectionalTraversalMapping("down", Direction10.Down)
            },
            TraversalLegs: new[]
            {
                new RuntimeTraversalLegDescriptor(
                    SourceRoomId: roomAId,
                    DestinationRoomId: roomBId,
                    Direction: Direction10.Up,
                    DefaultIsPassable: true),
                new RuntimeTraversalLegDescriptor(
                    SourceRoomId: roomBId,
                    DestinationRoomId: roomAId,
                    Direction: Direction10.Down,
                    DefaultIsPassable: true)
            });

        var country = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Country,
            Name: "Midlands",
            ScopeNameInGame: "Midlands",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "midlands" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { area });

        var planet = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Planet,
            Name: "Earth",
            ScopeNameInGame: "Earth",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "earth" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { country });

        return new RuntimeGameWorldSnapshot(
            ProjectName: "VerticalReplay",
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: new[] { "go" },
            GlobalDirectionals: new[] { "up", "down" },
            PlayerObjects: Array.Empty<RuntimeScopeNodeDescriptor>(),
            Planets: new[] { planet },
            StartingRoomId: roomAId,
            ProjectRoomGridCellSize: 40);
    }

    private static IReadOnlyList<PlaybackCaseDefinition> LoadDefinitions()
    {
        var manifestPath = Path.Combine(
            FindRepositoryRoot(),
            "StoryboardDesigner.App.Tests",
            "SampleProjectData",
            "simulator-playback",
            "playback-test-cases.json");

        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException($"Playback case manifest not found: '{manifestPath}'.");
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var manifestJson = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<PlaybackCaseManifest>(manifestJson, options)
            ?? throw new InvalidOperationException($"Unable to deserialize playback case manifest '{manifestPath}'.");

        var root = FindRepositoryRoot();
        return manifest.Cases
            .Where(static testCase => !testCase.Disabled)
            .Select(testCase => testCase with
            {
                ProjectFile = ResolvePath(root, testCase.ProjectFile),
                ReplayFile = ResolvePath(root, testCase.ReplayFile)
            })
            .ToList();
    }

    private static string ResolvePath(string repositoryRoot, string configuredPath)
    {
        var trimmed = configuredPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Playback case contains an empty path.");
        }

        if (Path.IsPathRooted(trimmed))
        {
            return trimmed;
        }

        return Path.GetFullPath(Path.Combine(repositoryRoot, trimmed));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var marker = Path.Combine(current.FullName, "StoryboardDesigner.slnx");
            if (File.Exists(marker))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root from test execution directory.");
    }

    private sealed record SimulatorRecording
    {
        public string SchemaVersion { get; init; } = "1.0";
        public string ProjectName { get; init; } = string.Empty;
        public string CreatedAtUtc { get; init; } = string.Empty;
        public List<RecordingStep> Steps { get; init; } = new();
    }

    private sealed class PlaybackCaseManifest
    {
        public List<PlaybackCaseDefinition> Cases { get; init; } = new();
    }

    public sealed record PlaybackCaseDefinition
    {
        public string Name { get; init; } = string.Empty;
        public string ProjectFile { get; init; } = string.Empty;
        public string ReplayFile { get; init; } = string.Empty;
        public bool Strict { get; init; }
        public bool Disabled { get; init; }

        public override string ToString() => Name;
    }

    private sealed record RecordingStep
    {
        public int Sequence { get; init; }
        public string EnteredAtUtc { get; init; } = string.Empty;
        public int DeltaMsFromPrevious { get; init; }
        public string CommandText { get; init; } = string.Empty;
        public int? RequestCommandCorrelationId { get; init; }
        public string RequestRawCommandText { get; init; } = string.Empty;
        public List<RecordedClarificationAnswer> RequestClarificationAnswers { get; init; } = new();
        public List<string> OutputLines { get; init; } = new();
        public List<string> Diagnostics { get; init; } = new();
        public List<HostCommandMoveLegTelemetry> MoveLegTelemetry { get; init; } = new();
        public bool Success { get; init; }
    }

    private sealed record RecordedClarificationAnswer
    {
        public string SlotId { get; init; } = string.Empty;
        public string SelectedObjectScopeNodeId { get; init; } = string.Empty;
    }
}
