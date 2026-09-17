using Storyboard.Shared.GameManager;
using Storyboard.Shared.GameServices;
using Storyboard.Shared.GameServices.Events;
using Storyboard.Shared.GameServices.RuntimeContext;
using Storyboard.Shared.GameServices.Spatial;
using Storyboard.Shared.GameStateData;
using Storyboard.Shared.HostContracts;
using Storyboard.Shared.RuntimeContracts;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using System.Text.Json;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Tests;

public sealed class GameManagerTests
{
    [Fact]
    public void LoadGameRuntimeProject_FromRuntimeExport_InitializesRuntime()
    {
        var manager = CreateSut();
        var projectPath = GetBirminghamCleanProjectPath();

        var result = Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager).LoadGameRuntimeProject(projectPath);

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics));
        Assert.True(manager.HasActiveRuntime);
        Assert.Equal(projectPath, Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager).LoadedProjectFilePath);
    }

    [Fact]
    public void LoadGameRuntimeProject_FromDirectRuntimeProjectFile_InitializesRuntime()
    {
        var manager = CreateSut();
        var cleanProjectPath = GetBirminghamCleanProjectPath();

        var result = Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager).LoadGameRuntimeProject(cleanProjectPath);

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics));
        Assert.True(manager.HasActiveRuntime);
        Assert.Equal(cleanProjectPath, Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager).LoadedProjectFilePath);
    }

    [Fact]
    public void ProcessCommand_RaisesOnCommandProcessed_WithReturnedPayload()
    {
        var manager = CreateSut();
        var projectPath = GetBirminghamCleanProjectPath();
        var loadResult = Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager).LoadGameRuntimeProject(projectPath);
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        HostProcessCommandResult? eventResult = null;
        manager.OnCommandProcessed += (_, args) => eventResult = args.Result;

        var result = manager.ProcessCommand("look");

        Assert.NotNull(eventResult);
        Assert.Equal(result.CommandId, eventResult!.CommandId);
        Assert.Equal(result.CommandText, eventResult.CommandText);
        Assert.Equal(result.Success, eventResult.Success);
        Assert.Equal(result.MatchedCommand, eventResult.MatchedCommand);
        Assert.Equal(result.Diagnostics, eventResult.Diagnostics);
    }

    [Fact]
    public void ProcessCommand_WithTechnicalDiagnostics_ExposesRoomChangeInPublicResult()
    {
        var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(manager, CreateNavigateRoomChangeSnapshot(), "in-memory-room-change");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var result = manager.ProcessCommand("go north", includeTechnicalDiagnostics: true);

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics));
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Contains("Host payload: roomChange oldRoom='Atrium'", StringComparison.Ordinal));
    }

    [Fact]
    public void ProcessCommand_WithTechnicalDiagnostics_ExposesRoomDisplayModeInPublicResult()
    {
        var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(manager, CreateNavigateRoomChangeWithOverlayModeSnapshot(), "in-memory-room-display-mode");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var result = manager.ProcessCommand("go north", includeTechnicalDiagnostics: true);

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics));
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Contains("Host payload: newRoom displayMode=Overlay;", StringComparison.Ordinal));
    }

    [Fact]
    public void ProcessCommand_WithTechnicalDiagnostics_ExposesRoomObjectChangesInPublicResult()
    {
        var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(manager, CreateMaterializeCreatedChangeSnapshot(), "in-memory-object-changes");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var result = manager.ProcessCommand("copy mold", includeTechnicalDiagnostics: true);

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics));
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Contains("Host payload: roomObjectChange kind=Added", StringComparison.Ordinal));
    }

    [Fact]
    public void ProcessCommand_InventoryAddedEvent_DoesNotRefireOnLaterNonPlayerInventoryRoomRemoval()
    {
        var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(
            manager,
            CreateInventoryEventNoRefireSnapshot(),
            "in-memory-inventory-no-refire");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var polling = Assert.IsAssignableFrom<ISessionDeltaPolling>(manager);
        var context = new HostRequestContext
        {
            CorrelationId = "corr-inventory-no-refire"
        };

        var baseline = polling.GetSessionBaseline(context, GameDiagnosticsLevel.None);
        var firstResult = manager.ProcessCommand("take apple", includeTechnicalDiagnostics: false);
        Assert.True(firstResult.Success, string.Join(" | ", firstResult.Diagnostics));

        var firstOutputs = CollectOutputsUntilQuiescent(polling, context, baseline.SessionDeltaWatermark, out var firstWatermark);
        Assert.Contains(firstOutputs, line =>
            line.Contains("event:inventory-added", StringComparison.Ordinal)
            || line.Contains("took apple", StringComparison.OrdinalIgnoreCase));

        var secondResult = manager.ProcessCommand("stash rock", includeTechnicalDiagnostics: false);
        Assert.True(secondResult.Success, string.Join(" | ", secondResult.Diagnostics));

        var secondOutputs = CollectOutputsUntilQuiescent(polling, context, firstWatermark, out _);
        Assert.DoesNotContain(secondOutputs, line => line.Contains("event:inventory-added", StringComparison.Ordinal));
        Assert.DoesNotContain(secondOutputs, line => line.Contains("took apple", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProcessCommand_ObjectStateEvents_EmitOnlyOnSuccessfulStateTransitions()
    {
        using var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(
            manager,
            CreateObjectStateEventsSnapshot(),
            "in-memory-object-state-events");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var polling = Assert.IsAssignableFrom<ISessionDeltaPolling>(manager);
        var context = new HostRequestContext
        {
            CorrelationId = "corr-object-state-events"
        };

        var baseline = polling.GetSessionBaseline(context, GameDiagnosticsLevel.None);

        var unlockResult = manager.ProcessCommand("unlock door", includeTechnicalDiagnostics: false);
        Assert.True(unlockResult.Success, string.Join(" | ", unlockResult.Diagnostics));
        var unlockOutputs = CollectOutputsUntilQuiescent(polling, context, baseline.SessionDeltaWatermark, out var unlockWatermark);
        Assert.Contains(unlockOutputs, line =>
            line.Contains("event:item-unlocked", StringComparison.Ordinal)
            || line.Contains("Unlocked Vault Door", StringComparison.Ordinal));

        var openResult = manager.ProcessCommand("open door", includeTechnicalDiagnostics: false);
        Assert.True(openResult.Success, string.Join(" | ", openResult.Diagnostics));
        var openOutputs = CollectOutputsUntilQuiescent(polling, context, unlockWatermark, out var openWatermark);
        Assert.Contains(openOutputs, line =>
            line.Contains("event:item-opened", StringComparison.Ordinal)
            || line.Contains("Opened Vault Door", StringComparison.Ordinal));

        var closeResult = manager.ProcessCommand("close door", includeTechnicalDiagnostics: false);
        Assert.True(closeResult.Success, string.Join(" | ", closeResult.Diagnostics));
        var closeOutputs = CollectOutputsUntilQuiescent(polling, context, openWatermark, out var closeWatermark);
        Assert.Contains(closeOutputs, line =>
            line.Contains("event:item-closed", StringComparison.Ordinal)
            || line.Contains("Closed Vault Door", StringComparison.Ordinal));

        var lockResult = manager.ProcessCommand("lock door", includeTechnicalDiagnostics: false);
        Assert.True(lockResult.Success, string.Join(" | ", lockResult.Diagnostics));
        var lockOutputs = CollectOutputsUntilQuiescent(polling, context, closeWatermark, out var lockWatermark);
        Assert.Contains(lockOutputs, line =>
            line.Contains("event:item-locked", StringComparison.Ordinal)
            || line.Contains("Locked Vault Door", StringComparison.Ordinal));

        var failOpenResult = manager.ProcessCommand("open door", includeTechnicalDiagnostics: false);
        Assert.False(failOpenResult.Success);
        var failOpenOutputs = CollectOutputsUntilQuiescent(polling, context, lockWatermark, out _);
        Assert.DoesNotContain(failOpenOutputs, line => line.Contains("event:item-opened", StringComparison.Ordinal));
        Assert.DoesNotContain(failOpenOutputs, line => line.Contains("event:item-closed", StringComparison.Ordinal));
        Assert.DoesNotContain(failOpenOutputs, line => line.Contains("event:item-locked", StringComparison.Ordinal));
        Assert.DoesNotContain(failOpenOutputs, line => line.Contains("event:item-unlocked", StringComparison.Ordinal));
    }

    [Fact]
    public void ProcessCommand_OpenObject_DirectionalWordInsideObjectNameInGame_ResolvesObjectTarget()
    {
        using var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(
            manager,
            CreateDirectionalWordObjectNameSnapshot(),
            "in-memory-directional-word-object-name");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var polling = Assert.IsAssignableFrom<ISessionDeltaPolling>(manager);
        var context = new HostRequestContext
        {
            CorrelationId = "corr-directional-word-object-name"
        };
        var baseline = polling.GetSessionBaseline(context, GameDiagnosticsLevel.None);

        var result = manager.ProcessCommand("open west door", includeTechnicalDiagnostics: true);
        var outputs = CollectOutputsUntilQuiescent(polling, context, baseline.SessionDeltaWatermark, out _);

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics));
        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => diagnostic.Contains("could not resolve a target object", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(outputs, line => line.Contains("Opened West Door", StringComparison.Ordinal));
    }

    [Fact]
    public void ProcessCommand_GlobalScopeAction_PrecedesGlobalObjectFallback_WhenVerbMatchesBoth()
    {
        var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(manager, CreateGlobalActionPrecedenceSnapshot(), "in-memory-global-action-precedence");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var result = manager.ProcessCommand("inspect", includeTechnicalDiagnostics: true);

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics));
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Contains("Matched scoped action 'GlobalScopeInspect'", StringComparison.Ordinal));
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Contains("Action flow: stopped propagation at this scope.", StringComparison.Ordinal));
        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => diagnostic.Contains("GlobalObjectInspect", StringComparison.Ordinal));
    }

    [Fact]
    public void TryHandleRoomPointIntent_ActiveSelection_IgnoresPassiveObjects()
    {
        var manager = Assert.IsType<Storyboard.Shared.GameManager.GameManager>(CreateSut());
        var solidId = Guid.NewGuid();
        var passiveId = Guid.NewGuid();
        var loadResult = LoadRuntimeSnapshot(manager, 
            CreatePointSelectionSnapshot(solidId, passiveId, includeSolidObject: true),
            "in-memory-point-selection-passive-filter");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var handled = TrySelectPrimaryByPoint(manager, 45, 45, out var failureReason);

        Assert.True(handled, failureReason);
        var presentation = manager.GetCurrentPresentation();
        Assert.Equal(solidId, presentation.ActiveRoomObjectId);
    }

    [Fact]
    public void TryHandleRoomPointIntent_ActiveSelection_OnlyPassiveObjectAtPoint_Fails()
    {
        var manager = Assert.IsType<Storyboard.Shared.GameManager.GameManager>(CreateSut());
        var solidId = Guid.NewGuid();
        var passiveId = Guid.NewGuid();
        var loadResult = LoadRuntimeSnapshot(manager, 
            CreatePointSelectionSnapshot(solidId, passiveId, includeSolidObject: false),
            "in-memory-point-selection-passive-only");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var handled = TrySelectPrimaryByPoint(manager, 45, 45, out var failureReason);

        Assert.False(handled);
        Assert.Contains("No room object occupies cell", failureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeContractsHostShims_GameManagerAndResults_AreCompatibilityAssignable()
    {
        var manager = CreateSut();
        Assert.IsAssignableFrom<IHostRuntimeCommandProcessorClient>(manager);

        var loadResult = LoadRuntimeSnapshot(manager, CreateNavigateRoomChangeSnapshot(), "in-memory-shim");
        Assert.IsAssignableFrom<HostLoadGameRuntimeProjectResult>(loadResult);

        var commandResult = manager.ProcessCommand("go north", includeTechnicalDiagnostics: true);
        Assert.IsAssignableFrom<HostProcessCommandResult>(commandResult);

        var presentationResult = manager.GetCurrentPresentation(GameDiagnosticsLevel.Medium);
        Assert.IsAssignableFrom<HostRuntimePresentationResult>(presentationResult);
        Assert.NotNull(presentationResult.RoomChange?.NewRoom);
    }

    [Fact]
    public void ProcessCommand_NextPhase_EmitsAdvancePhaseChangeWithOrderedTextPresentationSteps()
    {
        using var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(manager, CreatePhaseReasonAndResumeSnapshot(), "in-memory-phase-advance");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var polling = Assert.IsAssignableFrom<ISessionDeltaPolling>(manager);
        var context = new HostRequestContext
        {
            CorrelationId = "corr-phase-advance"
        };

        var baseline = polling.GetSessionBaseline(context, GameDiagnosticsLevel.None);

        var result = manager.ProcessCommand("nextphase", includeTechnicalDiagnostics: false);
        Assert.True(result.Success, string.Join(" | ", result.Diagnostics));

        var delta = polling.GetSessionDeltas(context, baseline.SessionDeltaWatermark, HostSessionDeltaBatchProfile.Medium);
        Assert.Equal(HostSessionDeltaPollResultCode.Success, delta.ResultCode);
        var phaseChange = Assert.IsAssignableFrom<HostCommandPhaseChangeData>(delta.SessionData.PhaseChange);

        Assert.Equal("advance", phaseChange.Reason);
        Assert.False(phaseChange.IsResume ?? false);
        Assert.False(phaseChange.ChangedBook ?? false);
        Assert.False(phaseChange.ChangedChapter ?? false);
        Assert.True(phaseChange.ChangedPage ?? false);

        Assert.Equal(3, phaseChange.OrderedTextPresentationSteps.Count);
        Assert.Equal("Page Two Title", phaseChange.OrderedTextPresentationSteps[0].TitleText);
        Assert.Equal(string.Empty, phaseChange.OrderedTextPresentationSteps[0].BodyText);
        Assert.Equal("text.hudoverlay.fadein.auto.6000", phaseChange.OrderedTextPresentationSteps[0].EffectKey);
        Assert.Equal("HudEdgeCard", phaseChange.OrderedTextPresentationSteps[0].Where);
        Assert.Equal("FadeIn", phaseChange.OrderedTextPresentationSteps[0].How);
        Assert.Equal("Auto", phaseChange.OrderedTextPresentationSteps[0].DismissMode);

        Assert.Equal(string.Empty, phaseChange.OrderedTextPresentationSteps[1].TitleText);
        Assert.Equal("Page Two Prologue", phaseChange.OrderedTextPresentationSteps[1].BodyText);
        Assert.Equal("text.hudoverlay.manualscroll.manualdismiss", phaseChange.OrderedTextPresentationSteps[1].EffectKey);
        Assert.Equal("HudFullscreen", phaseChange.OrderedTextPresentationSteps[1].Where);
        Assert.Equal("ManualScroll", phaseChange.OrderedTextPresentationSteps[1].How);
        Assert.Equal("Manual", phaseChange.OrderedTextPresentationSteps[1].DismissMode);

        Assert.Equal(string.Empty, phaseChange.OrderedTextPresentationSteps[2].TitleText);
        Assert.Equal("Page Two Narrative", phaseChange.OrderedTextPresentationSteps[2].BodyText);
        Assert.Equal("text.narrativedialog.archive", phaseChange.OrderedTextPresentationSteps[2].EffectKey);

        Assert.Equal("Page Two Title", phaseChange.NewPhase.Page.Title);
        Assert.Equal("Page Two Prologue", phaseChange.NewPhase.Page.Prologue);
        Assert.Equal("Page Two Narrative", phaseChange.NewPhase.Page.Narrative);
    }

    [Fact]
    public void ProcessCommand_SetPhase_EmitsSetReason()
    {
        using var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(manager, CreatePhaseReasonAndResumeSnapshot(), "in-memory-phase-set");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var polling = Assert.IsAssignableFrom<ISessionDeltaPolling>(manager);
        var context = new HostRequestContext
        {
            CorrelationId = "corr-phase-set"
        };

        var baseline = polling.GetSessionBaseline(context, GameDiagnosticsLevel.None);

        var result = manager.ProcessCommand("setphase2", includeTechnicalDiagnostics: false);
        Assert.True(result.Success, string.Join(" | ", result.Diagnostics));

        var delta = polling.GetSessionDeltas(context, baseline.SessionDeltaWatermark, HostSessionDeltaBatchProfile.Medium);
        Assert.Equal(HostSessionDeltaPollResultCode.Success, delta.ResultCode);
        var phaseChange = Assert.IsAssignableFrom<HostCommandPhaseChangeData>(delta.SessionData.PhaseChange);

        Assert.Equal("set", phaseChange.Reason);
        Assert.False(phaseChange.IsResume ?? false);
        Assert.True(phaseChange.ChangedPage ?? false);
    }

    [Fact]
    public void ProcessCommand_NextPhase_CrossTierAdvance_EmitsNineOrderedTextSteps()
    {
        using var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(manager, CreateCrossTierPhaseAdvanceSnapshot(), "in-memory-phase-cross-tier-advance");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var polling = Assert.IsAssignableFrom<ISessionDeltaPolling>(manager);
        var context = new HostRequestContext
        {
            CorrelationId = "corr-phase-cross-tier"
        };

        var baseline = polling.GetSessionBaseline(context, GameDiagnosticsLevel.None);

        var result = manager.ProcessCommand("nextphase", includeTechnicalDiagnostics: false);
        Assert.True(result.Success, string.Join(" | ", result.Diagnostics));

        var delta = polling.GetSessionDeltas(context, baseline.SessionDeltaWatermark, HostSessionDeltaBatchProfile.Medium);
        Assert.Equal(HostSessionDeltaPollResultCode.Success, delta.ResultCode);
        var phaseChange = Assert.IsAssignableFrom<HostCommandPhaseChangeData>(delta.SessionData.PhaseChange);

        Assert.True(phaseChange.ChangedBook ?? false);
        Assert.True(phaseChange.ChangedChapter ?? false);
        Assert.True(phaseChange.ChangedPage ?? false);
        Assert.Equal(9, phaseChange.OrderedTextPresentationSteps.Count);

        var expected = new (string? TitleText, string BodyText)[]
        {
            ("Book Two Title", string.Empty),
            (string.Empty, "Book Two Prologue"),
            (string.Empty, "Book Two Narrative"),
            ("Chapter Two Title", string.Empty),
            (string.Empty, "Chapter Two Prologue"),
            (string.Empty, "Chapter Two Narrative"),
            ("Page Two Title", string.Empty),
            (string.Empty, "Page Two Prologue"),
            (string.Empty, "Page Two Narrative")
        };

        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].TitleText, phaseChange.OrderedTextPresentationSteps[i].TitleText);
            Assert.Equal(expected[i].BodyText, phaseChange.OrderedTextPresentationSteps[i].BodyText);
        }
    }

    [Fact]
    public void GetCurrentPresentation_ResumePhase_IncludesTitleAndPrologueContext()
    {
        using var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(manager, CreatePhaseReasonAndResumeSnapshot(), "in-memory-phase-resume");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var presentation = manager.GetCurrentPresentation();
        var phaseChange = Assert.IsAssignableFrom<HostCommandPhaseChangeData>(presentation.PhaseChange);

        Assert.Equal("resume", phaseChange.Reason);
        Assert.True(phaseChange.IsResume ?? false);
        Assert.Equal(9, phaseChange.OrderedTextPresentationSteps.Count);
        Assert.Equal("Book One Title", phaseChange.OrderedTextPresentationSteps[0].TitleText);
        Assert.Equal(string.Empty, phaseChange.OrderedTextPresentationSteps[0].BodyText);
        Assert.Equal("text.hudoverlay.fadein.auto.6000", phaseChange.OrderedTextPresentationSteps[0].EffectKey);
        Assert.Equal("HudEdgeCard", phaseChange.OrderedTextPresentationSteps[0].Where);
        Assert.Equal("FadeIn", phaseChange.OrderedTextPresentationSteps[0].How);

        Assert.Equal(string.Empty, phaseChange.OrderedTextPresentationSteps[8].TitleText);
        Assert.Equal("Page One Narrative", phaseChange.OrderedTextPresentationSteps[8].BodyText);
        Assert.Equal("text.narrativedialog.archive", phaseChange.OrderedTextPresentationSteps[8].EffectKey);

        Assert.Equal("Book One Title", phaseChange.NewPhase.Book.Title);
        Assert.Equal("Book One Prologue", phaseChange.NewPhase.Book.Prologue);
        Assert.Equal("Book One Narrative", phaseChange.NewPhase.Book.Narrative);
        Assert.Equal("Chapter One Title", phaseChange.NewPhase.Chapter.Title);
        Assert.Equal("Chapter One Prologue", phaseChange.NewPhase.Chapter.Prologue);
        Assert.Equal("Chapter One Narrative", phaseChange.NewPhase.Chapter.Narrative);
        Assert.Equal("Page One Title", phaseChange.NewPhase.Page.Title);
        Assert.Equal("Page One Prologue", phaseChange.NewPhase.Page.Prologue);
        Assert.Equal("Page One Narrative", phaseChange.NewPhase.Page.Narrative);
    }

    [Fact]
    public void GetSessionBaseline_IncludesPhaseAmbientSoundCues()
    {
        using var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(manager, CreatePhaseAmbientBaselineSnapshot(), "in-memory-phase-ambient-baseline");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var polling = Assert.IsAssignableFrom<ISessionDeltaPolling>(manager);
        var baseline = polling.GetSessionBaseline(new HostRequestContext
        {
            CorrelationId = "corr-phase-ambient-baseline"
        }, GameDiagnosticsLevel.None);

        var cue = Assert.Single(baseline.SoundCues);
        Assert.Equal(HostCommandSoundCueOperation.Play, cue.Operation);
        Assert.Equal(SoundEffectLane.Ambient, cue.SoundEffectLane);
        Assert.Equal("sound.phase.ambient", cue.SoundEffectKey);
        Assert.Equal("assets/sounds/phase-ambient.mp3", cue.RuntimeAssetRef);
        Assert.Equal(SoundEffectRepeatMode.UntilCanceled, cue.RepeatMode);
        Assert.Equal(SoundEffectReplayPolicy.IgnoreIfAlreadyPlaying, cue.ReplayPolicy);
        Assert.Equal(-1, cue.CommandCorrelationId);
        Assert.Equal(Guid.Empty, cue.ActionId);
        Assert.Equal("SessionBaseline.PhaseAmbient", cue.ResultCode);
        Assert.Equal(0, cue.SequenceIndex);
    }

    [Fact]
    public void ProcessCommand_RequestPath_ReusedCompletedCorrelationId_ReturnsDuplicateCorrelationId()
    {
        var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(manager, CreateNavigateRoomChangeSnapshot(), "in-memory-duplicate-correlation");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var first = manager.ProcessCommand(new HostProcessCommandRequest
        {
            CommandCorrelationId = 0,
            RawCommandText = "go north",
            DiagnosticsLevel = GameDiagnosticsLevel.None
        });

        var second = manager.ProcessCommand(new HostProcessCommandRequest
        {
            CommandCorrelationId = 0,
            RawCommandText = "go north",
            DiagnosticsLevel = GameDiagnosticsLevel.None
        });

        Assert.NotEqual(HostProcessCommandResultCode.DuplicateCorrelationId, first.ResultCode);
        Assert.Equal(HostProcessCommandResultCode.DuplicateCorrelationId, second.ResultCode);
        Assert.False(second.Success);
    }

    [Fact]
    public void ProcessCommand_RequestPath_UnexpectedClarificationAnswer_ReturnsClarificationAnswerMismatch()
    {
        var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(manager, CreateNavigateRoomChangeSnapshot(), "in-memory-unexpected-answer");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var result = manager.ProcessCommand(new HostProcessCommandRequest
        {
            CommandCorrelationId = 1,
            RawCommandText = "go north",
            DiagnosticsLevel = GameDiagnosticsLevel.None,
            ClarificationAnswers =
            [
                new HostClarificationAnswer
                {
                    SlotId = "slot-1",
                    SelectedObjectScopeNodeId = Guid.NewGuid().ToString("D")
                }
            ]
        });

        Assert.Equal(HostProcessCommandResultCode.ClarificationAnswerMismatch, result.ResultCode);
        Assert.False(result.Success);
    }

    [Fact]
    public void ProcessCommand_RequestPath_AmbiguousSynonym_ReturnsClarificationRequired()
    {
        var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(manager, CreateAmbiguousSynonymSnapshot(), "in-memory-ambiguous-synonym");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var result = manager.ProcessCommand(new HostProcessCommandRequest
        {
            CommandCorrelationId = 2,
            RawCommandText = "look key",
            DiagnosticsLevel = GameDiagnosticsLevel.None
        });

        Assert.Equal(HostProcessCommandResultCode.ClarificationRequired, result.ResultCode);
        Assert.True(result.ClarificationRequired);
        Assert.NotNull(result.PendingClarification);
        Assert.Equal("key", result.PendingClarification!.AmbiguousPhraseText);
        Assert.True(result.PendingClarification.Candidates.Count >= 2);
        Assert.All(result.PendingClarification.Candidates, candidate =>
            Assert.False(string.IsNullOrWhiteSpace(candidate.ObjectScopeNodeId)));
    }

    [Fact]
    public void ProcessCommand_RequestPath_PendingClarification_RequiresAnswer()
    {
        var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(manager, CreateAmbiguousSynonymSnapshot(), "in-memory-ambiguous-synonym-missing-answer");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var first = manager.ProcessCommand(new HostProcessCommandRequest
        {
            CommandCorrelationId = 3,
            RawCommandText = "look key",
            DiagnosticsLevel = GameDiagnosticsLevel.None
        });

        Assert.Equal(HostProcessCommandResultCode.ClarificationRequired, first.ResultCode);
        Assert.NotNull(first.PendingClarification);

        var missingAnswer = manager.ProcessCommand(new HostProcessCommandRequest
        {
            CommandCorrelationId = 3,
            RawCommandText = "look key",
            DiagnosticsLevel = GameDiagnosticsLevel.None
        });

        Assert.Equal(HostProcessCommandResultCode.ClarificationAnswerMismatch, missingAnswer.ResultCode);
        Assert.False(missingAnswer.Success);
        Assert.NotNull(missingAnswer.PendingClarification);
    }

    [Fact]
    public void ProcessCommand_RequestPath_PendingClarification_ValidAnswer_CompletesCorrelation()
    {
        var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(manager, CreateAmbiguousSynonymSnapshot(), "in-memory-ambiguous-synonym-valid-answer");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var first = manager.ProcessCommand(new HostProcessCommandRequest
        {
            CommandCorrelationId = 4,
            RawCommandText = "look key",
            DiagnosticsLevel = GameDiagnosticsLevel.None
        });

        Assert.Equal(HostProcessCommandResultCode.ClarificationRequired, first.ResultCode);
        var pending = Assert.IsAssignableFrom<HostPendingClarificationRequest>(first.PendingClarification);
        var selectedCandidate = pending.Candidates[0];

        var second = manager.ProcessCommand(new HostProcessCommandRequest
        {
            CommandCorrelationId = 4,
            RawCommandText = "look key",
            DiagnosticsLevel = GameDiagnosticsLevel.None,
            ClarificationAnswers =
            [
                new HostClarificationAnswer
                {
                    SlotId = pending.SlotId,
                    SelectedObjectScopeNodeId = selectedCandidate.ObjectScopeNodeId
                }
            ]
        });

        Assert.False(second.ClarificationRequired);
        Assert.Null(second.PendingClarification);
        Assert.Equal(HostProcessCommandResultCode.NoMatch, second.ResultCode);

        var duplicate = manager.ProcessCommand(new HostProcessCommandRequest
        {
            CommandCorrelationId = 4,
            RawCommandText = "look key",
            DiagnosticsLevel = GameDiagnosticsLevel.None
        });

        Assert.Equal(HostProcessCommandResultCode.DuplicateCorrelationId, duplicate.ResultCode);
    }

    [Fact]
    public void ProcessCommand_RequestPath_ChainedClarifications_RequireSequentialSlotResolution()
    {
        var manager = CreateSut();
        var loadResult = LoadRuntimeSnapshot(manager, CreateChainedAmbiguousSynonymSnapshot(), "in-memory-chained-ambiguous-synonym");
        Assert.True(loadResult.Success, string.Join(" | ", loadResult.Diagnostics));

        var first = manager.ProcessCommand(new HostProcessCommandRequest
        {
            CommandCorrelationId = 5,
            RawCommandText = "use key on door",
            DiagnosticsLevel = GameDiagnosticsLevel.None
        });

        Assert.Equal(HostProcessCommandResultCode.ClarificationRequired, first.ResultCode);
        var firstPending = Assert.IsAssignableFrom<HostPendingClarificationRequest>(first.PendingClarification);
        Assert.Equal("key", firstPending.AmbiguousPhraseText);
        var firstSelected = firstPending.Candidates[0];

        var second = manager.ProcessCommand(new HostProcessCommandRequest
        {
            CommandCorrelationId = 5,
            RawCommandText = "use key on door",
            DiagnosticsLevel = GameDiagnosticsLevel.None,
            ClarificationAnswers =
            [
                new HostClarificationAnswer
                {
                    SlotId = firstPending.SlotId,
                    SelectedObjectScopeNodeId = firstSelected.ObjectScopeNodeId
                }
            ]
        });

        Assert.Equal(HostProcessCommandResultCode.ClarificationRequired, second.ResultCode);
        var secondPending = Assert.IsAssignableFrom<HostPendingClarificationRequest>(second.PendingClarification);
        Assert.Equal("door", secondPending.AmbiguousPhraseText);
        Assert.NotEqual(firstPending.SlotId, secondPending.SlotId);
        var secondSelected = secondPending.Candidates[0];

        var third = manager.ProcessCommand(new HostProcessCommandRequest
        {
            CommandCorrelationId = 5,
            RawCommandText = "use key on door",
            DiagnosticsLevel = GameDiagnosticsLevel.None,
            ClarificationAnswers =
            [
                new HostClarificationAnswer
                {
                    SlotId = firstPending.SlotId,
                    SelectedObjectScopeNodeId = firstSelected.ObjectScopeNodeId
                },
                new HostClarificationAnswer
                {
                    SlotId = secondPending.SlotId,
                    SelectedObjectScopeNodeId = secondSelected.ObjectScopeNodeId
                }
            ]
        });

        Assert.False(third.ClarificationRequired);
        Assert.Null(third.PendingClarification);
        Assert.Equal(HostProcessCommandResultCode.NoMatch, third.ResultCode);

        var duplicate = manager.ProcessCommand(new HostProcessCommandRequest
        {
            CommandCorrelationId = 5,
            RawCommandText = "use key on door",
            DiagnosticsLevel = GameDiagnosticsLevel.None
        });

        Assert.Equal(HostProcessCommandResultCode.DuplicateCorrelationId, duplicate.ResultCode);
    }

    [Fact]
    public void LoadRuntimeSnapshot_Result_ExposesAuthoredRenderDimensions()
    {
        var manager = CreateSut();
        var snapshot = CreateNavigateRoomChangeSnapshot() with
        {
            AuthoredRenderWidth = 1280,
            AuthoredRenderHeight = 720
        };

        var result = LoadRuntimeSnapshot(manager, snapshot, "in-memory-resolution");

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics));
        Assert.Equal(1280, result.AuthoredRenderWidth);
        Assert.Equal(720, result.AuthoredRenderHeight);
    }

    private static RuntimeGameWorldSnapshot CreateNavigateRoomChangeSnapshot()
    {
        var roomAId = Guid.NewGuid();
        var roomBId = Guid.NewGuid();

        var navigateAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "GoNorth",
            ActionType = CommandActionType.NavigateDirection,
            NoVerbLinkage = false,
            Verbs = new[] { "go" },
            DirectionQualifierText = "north",
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Moved"] = "Moved north"
            },
            NavigatePayload = new RuntimeNavigateActionPayload()
        };

        var roomA = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Atrium",
            ScopeNameInGame: "Atrium",
            ScopeNodeId: roomAId,
            VariableDefinitions: CreateRoomVariableDefinitions(),
            ScopeTokens: new[] { "atrium" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: new[] { navigateAction },
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var roomB = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Library",
            ScopeNameInGame: "Library",
            ScopeNodeId: roomBId,
            VariableDefinitions: CreateRoomVariableDefinitions(),
            ScopeTokens: new[] { "library" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "AreaOne",
            ScopeNameInGame: "AreaOne",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "areaone" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { roomA, roomB },
            AdditionalDirectionalTraversalMappings: new[]
            {
                new RuntimeDirectionalTraversalMapping("north", Direction10.North)
            },
            TraversalLegs: new[]
            {
                new RuntimeTraversalLegDescriptor(
                    SourceRoomId: roomAId,
                    DestinationRoomId: roomBId,
                    Direction: Direction.North,
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
            ProjectName: "ManagerRoomChange",
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: new[] { "go" },
            GlobalDirectionals: new[] { "north" },
            PlayerObjects: Array.Empty<RuntimeScopeNodeDescriptor>(),
            Planets: new[] { planet },
            StartingRoomId: roomAId,
            GlobalDirectionalTraversalMappings: new[]
            {
                new RuntimeDirectionalTraversalMapping("north", Direction10.North)
            },
            ProjectRoomGridCellSize: 40);
    }

    private static RuntimeGameWorldSnapshot CreateGlobalActionPrecedenceSnapshot()
    {
        var roomId = Guid.NewGuid();

        var globalScopeAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "GlobalScopeInspect",
            ActionType = CommandActionType.EchoMessage,
            NoVerbLinkage = false,
            Verbs = new[] { "inspect" },
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "global scope handler"
            },
            EchoPayload = new RuntimeEchoActionPayload()
        };

        var globalObjectAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "GlobalObjectInspect",
            ActionType = CommandActionType.EchoMessage,
            NoVerbLinkage = false,
            Verbs = new[] { "inspect" },
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "global object handler"
            },
            EchoPayload = new RuntimeEchoActionPayload()
        };

        var room = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Atrium",
            ScopeNameInGame: "Atrium",
            ScopeNodeId: roomId,
            VariableDefinitions: CreateRoomVariableDefinitions(),
            ScopeTokens: new[] { "atrium" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "AreaOne",
            ScopeNameInGame: "AreaOne",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "areaone" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { room });

        var country = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Country,
            Name: "CountryOne",
            ScopeNameInGame: "CountryOne",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "countryone" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { area });

        var planet = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Planet,
            Name: "PlanetOne",
            ScopeNameInGame: "PlanetOne",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "planetone" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { country });

        var globalObject = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "Pack",
            ScopeNameInGame: "Pack",
            ScopeNodeId: Guid.NewGuid(),
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "pack" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: new[] { globalObjectAction },
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        return new RuntimeGameWorldSnapshot(
            ProjectName: "GlobalActionPrecedence",
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: new[] { "inspect" },
            GlobalDirectionals: Array.Empty<string>(),
            PlayerObjects: new[] { globalObject },
            Planets: new[] { planet },
            StartingRoomId: roomId,
            ProjectRoomGridCellSize: 40,
            GlobalAvailableActions: new[] { globalScopeAction });
    }

    private static RuntimeGameWorldSnapshot CreateMaterializeCreatedChangeSnapshot()
    {
        var sourceId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        var materializeAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "copy mold",
            ActionType = CommandActionType.MaterializeObjectCopy,
            Verbs = new[] { "copy" },
            NoVerbLinkage = false,
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "A new mold appears."
            },
            MaterializeObjectCopyPayload = new RuntimeMaterializeObjectCopyActionPayload(sourceId)
        };

        var source = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "Mold",
            ScopeNameInGame: "Mold",
            ScopeNodeId: sourceId,
            VariableDefinitions: new[]
            {
                new RuntimePropertyDefinition("isActive", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse),
                new RuntimePropertyDefinition("selectedImagePath", "mold.png", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Unrestricted)
            },
            ScopeTokens: new[] { "mold" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: new[] { materializeAction },
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var room = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Foundry",
            ScopeNameInGame: "Foundry",
            ScopeNodeId: roomId,
            VariableDefinitions: CreateRoomVariableDefinitions(),
            ScopeTokens: new[] { "foundry" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { source });

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "Area",
            ScopeNameInGame: "Area",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "area" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { room });

        var country = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Country,
            Name: "Country",
            ScopeNameInGame: "Country",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "country" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { area });

        var planet = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Planet,
            Name: "Planet",
            ScopeNameInGame: "Planet",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "planet" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { country });

        return new RuntimeGameWorldSnapshot(
            ProjectName: "ManagerObjectChanges",
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: new[] { "copy" },
            GlobalDirectionals: Array.Empty<string>(),
            PlayerObjects: Array.Empty<RuntimeScopeNodeDescriptor>(),
            Planets: new[] { planet },
            StartingRoomId: roomId,
            ProjectRoomGridCellSize: 40);
    }

    private static RuntimeGameWorldSnapshot CreateInventoryEventNoRefireSnapshot()
    {
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var chestId = Guid.NewGuid();
        var appleId = Guid.NewGuid();
        var rockId = Guid.NewGuid();

        var inventoryAddedEventAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "OnInventoryAdded",
            ActionType = CommandActionType.EchoMessage,
            NoVerbLinkage = true,
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "event:inventory-added"
            },
            EchoPayload = new RuntimeEchoActionPayload()
        };

        var takeAppleAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "TakeApple",
            ActionType = CommandActionType.PutObjectInContainer,
            NoVerbLinkage = false,
            Verbs = ["take"],
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "took apple"
            },
            PutObjectInContainerPayload = new RuntimePutObjectInContainerActionPayload("room:Player")
        };

        var stashRockAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "StashRock",
            ActionType = CommandActionType.PutObjectInContainer,
            NoVerbLinkage = false,
            Verbs = ["stash"],
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "stashed rock"
            },
            PutObjectInContainerPayload = new RuntimePutObjectInContainerActionPayload("room:Chest")
        };

        var apple = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "Apple",
            ScopeNameInGame: "Apple",
            ScopeNodeId: appleId,
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("inventoryPoints", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric)
            ],
            ScopeTokens: ["apple"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: [takeAppleAction],
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var rock = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "Rock",
            ScopeNameInGame: "Rock",
            ScopeNodeId: rockId,
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("inventoryPoints", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric)
            ],
            ScopeTokens: ["rock"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: [stashRockAction],
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var chest = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "Chest",
            ScopeNameInGame: "Chest",
            ScopeNodeId: chestId,
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("isContainer", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse),
                new RuntimePropertyDefinition("containerPoints", "20", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric)
            ],
            ScopeTokens: ["chest"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var room = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Atrium",
            ScopeNameInGame: "Atrium",
            ScopeNodeId: roomId,
            VariableDefinitions: CreateRoomVariableDefinitions(),
            ScopeTokens: ["atrium"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [apple, rock, chest]);

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "Area",
            ScopeNameInGame: "Area",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["area"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [room]);

        var country = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Country,
            Name: "Country",
            ScopeNameInGame: "Country",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["country"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [area]);

        var planet = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Planet,
            Name: "Planet",
            ScopeNameInGame: "Planet",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["planet"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [country]);

        var player = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "Player",
            ScopeNameInGame: "Player",
            ScopeNodeId: playerId,
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("isPlayer", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse),
                new RuntimePropertyDefinition("isContainer", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse),
                new RuntimePropertyDefinition("containerPoints", "20", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric)
            ],
            ScopeTokens: ["player"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: [inventoryAddedEventAction],
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
            EventSubscriptions:
            [
                BuildInventoryEventSubscription(
                    RuntimeEventKey.inventory_item_added,
                    inventoryAddedEventAction.Name,
                    sourceMatchMode: SubscriptionSourceMatchMode.ExplicitSourceId,
                    sourceScopeNodeId: playerId)
            ]);

        return new RuntimeGameWorldSnapshot(
            ProjectName: "InventoryEventNoRefire",
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: ["take", "stash"],
            GlobalDirectionals: Array.Empty<string>(),
            PlayerObjects: [player],
            Planets: [planet],
            StartingRoomId: roomId,
            ProjectRoomGridCellSize: 40);
    }

    private static RuntimeGameWorldSnapshot CreateObjectStateEventsSnapshot()
    {
        var roomId = Guid.NewGuid();
        var keyId = Guid.NewGuid();

        var onOpenedAction = CreateEventEchoAction("OnItemOpened", "event:item-opened");
        var onClosedAction = CreateEventEchoAction("OnItemClosed", "event:item-closed");
        var onLockedAction = CreateEventEchoAction("OnItemLocked", "event:item-locked");
        var onUnlockedAction = CreateEventEchoAction("OnItemUnlocked", "event:item-unlocked");

        var door = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "Door",
            ScopeNameInGame: "Vault Door",
            ScopeNodeId: Guid.NewGuid(),
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("isOpen", "false", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse),
                new RuntimePropertyDefinition("isLocked", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse)
            ],
            ScopeTokens: ["door"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: [onOpenedAction, onClosedAction, onLockedAction, onUnlockedAction],
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
            LockOperationRequirements: new RuntimeLockOperationRequirementsDescriptor(
                UnlockKeyRequirements: [new RuntimeLockKeyRequirementDescriptor(keyId, 1)],
                RequireKeyForLockOperation: false),
            EventSubscriptions:
            [
                BuildInventoryEventSubscription(RuntimeEventKey.item_opened, onOpenedAction.Name),
                BuildInventoryEventSubscription(RuntimeEventKey.item_closed, onClosedAction.Name),
                BuildInventoryEventSubscription(RuntimeEventKey.item_locked, onLockedAction.Name),
                BuildInventoryEventSubscription(RuntimeEventKey.item_unlocked, onUnlockedAction.Name)
            ]);

        var unlockDoorAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "UnlockDoor",
            ActionType = CommandActionType.UnlockObject,
            NoVerbLinkage = false,
            Verbs = ["unlock"],
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "Unlocked {currentAction.UnlockedObjectName}",
                ["Failure"] = "Unlock failed"
            }
        };

        var openDoorAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "OpenDoor",
            ActionType = CommandActionType.OpenObject,
            NoVerbLinkage = false,
            Verbs = ["open"],
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "Opened {currentAction.OpenedObjectName}",
                ["Failure"] = "Open failed"
            }
        };

        var closeDoorAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "CloseDoor",
            ActionType = CommandActionType.CloseObject,
            NoVerbLinkage = false,
            Verbs = ["close"],
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "Closed {currentAction.ClosedObjectName}",
                ["Failure"] = "Close failed"
            }
        };

        var lockDoorAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "LockDoor",
            ActionType = CommandActionType.LockObject,
            NoVerbLinkage = false,
            Verbs = ["lock"],
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "Locked {currentAction.LockedObjectName}",
                ["Failure"] = "Lock failed"
            }
        };

        var room = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Atrium",
            ScopeNameInGame: "Atrium",
            ScopeNodeId: roomId,
            VariableDefinitions: CreateRoomVariableDefinitions(),
            ScopeTokens: ["atrium"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: [unlockDoorAction, openDoorAction, closeDoorAction, lockDoorAction],
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [door]);

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "Area",
            ScopeNameInGame: "Area",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["area"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [room]);

        var country = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Country,
            Name: "Country",
            ScopeNameInGame: "Country",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["country"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [area]);

        var planet = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Planet,
            Name: "Planet",
            ScopeNameInGame: "Planet",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["planet"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [country]);

        return new RuntimeGameWorldSnapshot(
            ProjectName: "ObjectStateEvents",
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: ["unlock", "open", "close", "lock"],
            GlobalDirectionals: Array.Empty<string>(),
            PlayerObjects: [CreateKeyNode(keyId, "Copper Key", "key")],
            Planets: [planet],
            StartingRoomId: roomId,
            ProjectRoomGridCellSize: 40);
    }

    private static RuntimeCommandActionDescriptor CreateEventEchoAction(string actionName, string successMessage)
    {
        return new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = actionName,
            ActionType = CommandActionType.EchoMessage,
            NoVerbLinkage = true,
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = successMessage
            },
            EchoPayload = new RuntimeEchoActionPayload()
        };
    }

    private static RuntimeScopeNodeDescriptor CreateKeyNode(Guid id, string name, string token)
    {
        return new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: name,
            ScopeNameInGame: name,
            ScopeNodeId: id,
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("isActive", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse),
                new RuntimePropertyDefinition("quantity", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric)
            ],
            ScopeTokens: [token],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());
    }

    private static RuntimeEventSubscriptionDto BuildInventoryEventSubscription(
        RuntimeEventKey eventKey,
        string actionName,
        SubscriptionSourceMatchMode sourceMatchMode = SubscriptionSourceMatchMode.AnySource,
        Guid? sourceScopeNodeId = null)
    {
        return new RuntimeEventSubscriptionDto
        {
            Id = Guid.NewGuid(),
            EventKey = eventKey.ToString(),
            IsEnabled = true,
            DispatchDisposition = EventSubscriberDispatchDisposition.bubble,
            SubscriptionSourceMatchMode = sourceMatchMode,
            SubscriptionSourceScopeNodeId = sourceScopeNodeId,
            SubscriptionSecondarySourceMatchMode = SubscriptionSourceMatchMode.AnySource,
            ActionBindings =
            [
                new RuntimeEventActionBindingDto
                {
                    Order = 0,
                    IsEnabled = true,
                    Condition = new RuntimeEventBindingConditionDto
                    {
                        QuantityEvaluationMode = RuntimeVariableQuantityEvaluationMode.AllResolvedMustPass,
                        Filters = []
                    },
                    Target = new RuntimeEventBindingTargetDto
                    {
                        ActionName = actionName,
                        StopChainOnFailure = true
                    }
                }
            ]
        };
    }

    private static List<string> CollectOutputsUntilQuiescent(
        ISessionDeltaPolling polling,
        HostRequestContext context,
        string? initialWatermark,
        out string endingWatermark)
    {
        var watermark = string.IsNullOrWhiteSpace(initialWatermark) ? "0" : initialWatermark;
        var outputs = new List<string>();

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var pollResult = polling.GetSessionDeltas(context, watermark, HostSessionDeltaBatchProfile.Hot);
            Assert.Equal(HostSessionDeltaPollResultCode.Success, pollResult.ResultCode);

            var sessionData = Assert.IsAssignableFrom<HostSessionDataEnvelope>(pollResult.SessionData);
            outputs.AddRange(sessionData.OutputLines);

            var nextWatermark = pollResult.SessionDeltaWatermark ?? string.Empty;
            if (string.Equals(nextWatermark, watermark, StringComparison.Ordinal))
            {
                endingWatermark = watermark;
                return outputs;
            }

            watermark = nextWatermark;
        }

        endingWatermark = watermark;
        return outputs;
    }

    private static RuntimeGameWorldSnapshot CreateAmbiguousSynonymSnapshot()
    {
        var roomId = Guid.NewGuid();
        var brassKeyId = Guid.NewGuid();
        var ironKeyId = Guid.NewGuid();

        var room = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Workshop",
            ScopeNameInGame: "Workshop",
            ScopeNodeId: roomId,
            VariableDefinitions: CreateRoomVariableDefinitions(),
            ScopeTokens: new[] { "workshop" },
            AdditionalVerbs: new[] { "look" },
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children:
            [
                new RuntimeScopeNodeDescriptor(
                    ScopeKind: ScopeNodeKind.GameObject,
                    Name: "BrassKey",
                    ScopeNameInGame: "Brass Key",
                    VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
                    ScopeTokens: new[] { "Brass Key", "BrassKey", "key" },
                    AdditionalVerbs: Array.Empty<string>(),
                    AdditionalDirectionals: Array.Empty<string>(),
                    AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
                    CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
                    Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
                    ScopeNodeId: brassKeyId),
                new RuntimeScopeNodeDescriptor(
                    ScopeKind: ScopeNodeKind.GameObject,
                    Name: "IronKey",
                    ScopeNameInGame: "Iron Key",
                    VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
                    ScopeTokens: new[] { "Iron Key", "IronKey", "key" },
                    AdditionalVerbs: Array.Empty<string>(),
                    AdditionalDirectionals: Array.Empty<string>(),
                    AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
                    CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
                    Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
                    ScopeNodeId: ironKeyId)
            ]);

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "AreaOne",
            ScopeNameInGame: "AreaOne",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "areaone" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { room });

        var country = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Country,
            Name: "CountryOne",
            ScopeNameInGame: "CountryOne",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "countryone" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { area });

        var planet = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Planet,
            Name: "PlanetOne",
            ScopeNameInGame: "PlanetOne",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "planetone" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { country });

        return new RuntimeGameWorldSnapshot(
            ProjectName: "AmbiguousSynonym",
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: new[] { "look" },
            GlobalDirectionals: Array.Empty<string>(),
            PlayerObjects: Array.Empty<RuntimeScopeNodeDescriptor>(),
            Planets: new[] { planet },
            StartingRoomId: roomId,
            ProjectRoomGridCellSize: 40);
    }

    private static RuntimeGameWorldSnapshot CreateDirectionalWordObjectNameSnapshot()
    {
        var roomId = Guid.NewGuid();

        var westDoor = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "DoorWest",
            ScopeNameInGame: "West Door",
            ScopeNodeId: Guid.NewGuid(),
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("isOpen", "false", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse)
            ],
            ScopeTokens: ["west door"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var openDoorAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "OpenDoor",
            ActionType = CommandActionType.OpenObject,
            NoVerbLinkage = false,
            Verbs = ["open"],
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "Opened {currentAction.OpenedObjectName}",
                ["Failure"] = "Open failed"
            }
        };

        var room = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "HotelRoom",
            ScopeNameInGame: "Hotel Room",
            ScopeNodeId: roomId,
            VariableDefinitions: CreateRoomVariableDefinitions(),
            ScopeTokens: ["hotel room"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: [openDoorAction],
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [westDoor]);

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "Area",
            ScopeNameInGame: "Area",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["area"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [room]);

        var country = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Country,
            Name: "Country",
            ScopeNameInGame: "Country",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["country"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [area]);

        var planet = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Planet,
            Name: "Planet",
            ScopeNameInGame: "Planet",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["planet"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [country]);

        return new RuntimeGameWorldSnapshot(
            ProjectName: "DirectionalWordObjectName",
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: ["open"],
            GlobalDirectionals: ["west", "east", "north", "south"],
            PlayerObjects: Array.Empty<RuntimeScopeNodeDescriptor>(),
            Planets: [planet],
            StartingRoomId: roomId,
            ProjectRoomGridCellSize: 40);
    }

    private static RuntimeGameWorldSnapshot CreateChainedAmbiguousSynonymSnapshot()
    {
        var roomId = Guid.NewGuid();
        var brassKeyId = Guid.NewGuid();
        var ironKeyId = Guid.NewGuid();
        var redDoorId = Guid.NewGuid();
        var blueDoorId = Guid.NewGuid();

        var room = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Workshop",
            ScopeNameInGame: "Workshop",
            ScopeNodeId: roomId,
            VariableDefinitions: CreateRoomVariableDefinitions(),
            ScopeTokens: new[] { "workshop" },
            AdditionalVerbs: new[] { "use" },
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children:
            [
                new RuntimeScopeNodeDescriptor(
                    ScopeKind: ScopeNodeKind.GameObject,
                    Name: "BrassKey",
                    ScopeNameInGame: "Brass Key",
                    VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
                    ScopeTokens: new[] { "Brass Key", "BrassKey", "key" },
                    AdditionalVerbs: Array.Empty<string>(),
                    AdditionalDirectionals: Array.Empty<string>(),
                    AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
                    CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
                    Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
                    ScopeNodeId: brassKeyId),
                new RuntimeScopeNodeDescriptor(
                    ScopeKind: ScopeNodeKind.GameObject,
                    Name: "IronKey",
                    ScopeNameInGame: "Iron Key",
                    VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
                    ScopeTokens: new[] { "Iron Key", "IronKey", "key" },
                    AdditionalVerbs: Array.Empty<string>(),
                    AdditionalDirectionals: Array.Empty<string>(),
                    AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
                    CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
                    Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
                    ScopeNodeId: ironKeyId),
                new RuntimeScopeNodeDescriptor(
                    ScopeKind: ScopeNodeKind.GameObject,
                    Name: "RedDoor",
                    ScopeNameInGame: "Red Door",
                    VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
                    ScopeTokens: new[] { "Red Door", "RedDoor", "door" },
                    AdditionalVerbs: Array.Empty<string>(),
                    AdditionalDirectionals: Array.Empty<string>(),
                    AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
                    CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
                    Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
                    ScopeNodeId: redDoorId),
                new RuntimeScopeNodeDescriptor(
                    ScopeKind: ScopeNodeKind.GameObject,
                    Name: "BlueDoor",
                    ScopeNameInGame: "Blue Door",
                    VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
                    ScopeTokens: new[] { "Blue Door", "BlueDoor", "door" },
                    AdditionalVerbs: Array.Empty<string>(),
                    AdditionalDirectionals: Array.Empty<string>(),
                    AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
                    CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
                    Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
                    ScopeNodeId: blueDoorId)
            ]);

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "AreaOne",
            ScopeNameInGame: "AreaOne",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "areaone" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { room });

        var country = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Country,
            Name: "CountryOne",
            ScopeNameInGame: "CountryOne",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "countryone" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { area });

        var planet = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Planet,
            Name: "PlanetOne",
            ScopeNameInGame: "PlanetOne",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "planetone" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { country });

        return new RuntimeGameWorldSnapshot(
            ProjectName: "AmbiguousSynonymChained",
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: new[] { "use" },
            GlobalDirectionals: Array.Empty<string>(),
            PlayerObjects: Array.Empty<RuntimeScopeNodeDescriptor>(),
            Planets: new[] { planet },
            StartingRoomId: roomId,
            ProjectRoomGridCellSize: 40);
    }

    private static IReadOnlyList<RuntimePropertyDefinition> CreateRoomVariableDefinitions()
    {
        return
        [
            new RuntimePropertyDefinition(
                "roomGridCellSize",
                "40",
                GamePropertyLifetime.Singleton,
                GamePropertyValueRestriction.Numeric)
        ];
    }

    private static RuntimeGameWorldSnapshot CreateNavigateRoomChangeWithOverlayModeSnapshot()
    {
        var roomAId = Guid.NewGuid();
        var roomBId = Guid.NewGuid();

        var navigateAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "GoNorth",
            ActionType = CommandActionType.NavigateDirection,
            NoVerbLinkage = false,
            Verbs = new[] { "go" },
            DirectionQualifierText = "north",
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Moved"] = "Moved north"
            },
            NavigatePayload = new RuntimeNavigateActionPayload()
        };

        var roomA = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Atrium",
            ScopeNameInGame: "Atrium",
            ScopeNodeId: roomAId,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "atrium" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: new[] { navigateAction },
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var roomB = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Library",
            ScopeNameInGame: "Library",
            ScopeNodeId: roomBId,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "library" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
            RoomDirectionalImages: new[]
            {
                new RuntimeRoomDirectionalImageDescriptor(
                    Slot: RoomImageSlot.North,
                    OverlayRenderOrder: 100,
                    FullImagePath: "images/library-north.png",
                    OverlayOffsetX: 0,
                    OverlayOffsetY: 0,
                    OverlayRotationDegrees: 0)
            },
            RoomDisplayMode: RuntimeRoomImageDisplayMode.Overlay);

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "AreaOne",
            ScopeNameInGame: "AreaOne",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "areaone" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { roomA, roomB },
            AdditionalDirectionalTraversalMappings: new[]
            {
                new RuntimeDirectionalTraversalMapping("north", Direction10.North)
            },
            TraversalLegs: new[]
            {
                new RuntimeTraversalLegDescriptor(
                    SourceRoomId: roomAId,
                    DestinationRoomId: roomBId,
                    Direction: Direction.North,
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
            ProjectName: "ManagerRoomDisplayMode",
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: new[] { "go" },
            GlobalDirectionals: new[] { "north" },
            PlayerObjects: Array.Empty<RuntimeScopeNodeDescriptor>(),
            Planets: new[] { planet },
            StartingRoomId: roomAId,
            GlobalDirectionalTraversalMappings: new[]
            {
                new RuntimeDirectionalTraversalMapping("north", Direction10.North)
            },
            ProjectRoomGridCellSize: 40);
    }

    private static RuntimeGameWorldSnapshot CreatePointSelectionSnapshot(Guid solidObjectId, Guid passiveObjectId, bool includeSolidObject)
    {
        var roomId = Guid.NewGuid();

        var roomObjects = new List<RuntimeScopeNodeDescriptor>
        {
            new RuntimeScopeNodeDescriptor(
                ScopeKind: ScopeNodeKind.GameObject,
                Name: "PassiveTile",
                ScopeNameInGame: "Passive Tile",
                ScopeNodeId: passiveObjectId,
                VariableDefinitions:
                [
                    new RuntimePropertyDefinition("positionX", "40", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                    new RuntimePropertyDefinition("positionY", "40", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                    new RuntimePropertyDefinition("footprintWidthCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                    new RuntimePropertyDefinition("footprintHeightCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                    new RuntimePropertyDefinition("authoredRenderOrder", "3000", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                    new RuntimePropertyDefinition("spatialType", nameof(RuntimeObjectSpatialTypes.PassiveObject), GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Unrestricted)
                ],
                ScopeTokens: ["passive", "tile"],
                AdditionalVerbs: Array.Empty<string>(),
                AdditionalDirectionals: Array.Empty<string>(),
                AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
                CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
                Children: Array.Empty<RuntimeScopeNodeDescriptor>())
        };

        if (includeSolidObject)
        {
            roomObjects.Add(new RuntimeScopeNodeDescriptor(
                ScopeKind: ScopeNodeKind.GameObject,
                Name: "SolidCrate",
                ScopeNameInGame: "Solid Crate",
                ScopeNodeId: solidObjectId,
                VariableDefinitions:
                [
                    new RuntimePropertyDefinition("positionX", "40", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                    new RuntimePropertyDefinition("positionY", "40", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                    new RuntimePropertyDefinition("footprintWidthCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                    new RuntimePropertyDefinition("footprintHeightCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                    new RuntimePropertyDefinition("authoredRenderOrder", "1000", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                    new RuntimePropertyDefinition("spatialType", nameof(RuntimeObjectSpatialTypes.SolidObject), GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Unrestricted)
                ],
                ScopeTokens: ["solid", "crate"],
                AdditionalVerbs: Array.Empty<string>(),
                AdditionalDirectionals: Array.Empty<string>(),
                AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
                CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
                Children: Array.Empty<RuntimeScopeNodeDescriptor>()));
        }

        var room = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Workshop",
            ScopeNameInGame: "Workshop",
            ScopeNodeId: roomId,
            VariableDefinitions: CreateRoomVariableDefinitions(),
            ScopeTokens: new[] { "workshop" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: roomObjects);

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "Area",
            ScopeNameInGame: "Area",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "area" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { room });

        var country = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Country,
            Name: "Country",
            ScopeNameInGame: "Country",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "country" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { area });

        var planet = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Planet,
            Name: "Planet",
            ScopeNameInGame: "Planet",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "planet" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { country });

        return new RuntimeGameWorldSnapshot(
            ProjectName: "PointSelectionPassiveFilter",
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: Array.Empty<string>(),
            GlobalDirectionals: Array.Empty<string>(),
            PlayerObjects: Array.Empty<RuntimeScopeNodeDescriptor>(),
            Planets: new[] { planet },
            StartingRoomId: roomId,
            ProjectRoomGridCellSize: 40);
    }

    private static RuntimeGameWorldSnapshot CreateWaypointPlayerSnapshot(Guid playerObjectId)
    {
        var roomId = Guid.NewGuid();

        var room = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "WaypointRoom",
            ScopeNameInGame: "Waypoint Room",
            ScopeNodeId: roomId,
            VariableDefinitions: CreateRoomVariableDefinitions(),
            ScopeTokens: new[] { "waypointroom" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children:
            [
                new RuntimeScopeNodeDescriptor(
                    ScopeKind: ScopeNodeKind.GameObject,
                    Name: "PlayerAvatar",
                    ScopeNameInGame: "Player Avatar",
                    ScopeNodeId: playerObjectId,
                    VariableDefinitions:
                    [
                        new RuntimePropertyDefinition("positionX", "80", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                        new RuntimePropertyDefinition("positionY", "80", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                        new RuntimePropertyDefinition("footprintWidthCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                        new RuntimePropertyDefinition("footprintHeightCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                        new RuntimePropertyDefinition("spatialType", nameof(RuntimeObjectSpatialTypes.SolidObject), GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Unrestricted),
                        new RuntimePropertyDefinition("isPlayer", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse)
                    ],
                    ScopeTokens: ["player", "avatar"],
                    AdditionalVerbs: Array.Empty<string>(),
                    AdditionalDirectionals: Array.Empty<string>(),
                    AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
                    CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
                    Children: Array.Empty<RuntimeScopeNodeDescriptor>())
            ]);

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "Area",
            ScopeNameInGame: "Area",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "area" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { room });

        var country = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Country,
            Name: "Country",
            ScopeNameInGame: "Country",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "country" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { area });

        var planet = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Planet,
            Name: "Planet",
            ScopeNameInGame: "Planet",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "planet" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { country });

        return new RuntimeGameWorldSnapshot(
            ProjectName: "WaypointPlayerResolution",
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: Array.Empty<string>(),
            GlobalDirectionals: Array.Empty<string>(),
            PlayerObjects: Array.Empty<RuntimeScopeNodeDescriptor>(),
            Planets: new[] { planet },
            StartingRoomId: roomId,
            ProjectRoomGridCellSize: 40);
    }

    private static RuntimeGameWorldSnapshot CreateRestrictedWaypointSnapshot(Guid targetObjectId)
    {
        var roomId = Guid.NewGuid();
        var restrictions = new RuntimeObjectMovementRestrictionsDescriptor(
            firstUnstacked: new Dictionary<string, RuntimeObjectMovementRestrictionRuleDescriptor>(StringComparer.OrdinalIgnoreCase)
            {
                ["N"] = new RuntimeObjectMovementRestrictionRuleDescriptor(MaxDistance: 2, AllowJumpOver: false)
            },
            firstStacked: null,
            subsequentUnstacked: null,
            subsequentStacked: null);

        var target = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "RestrictedMover",
            ScopeNameInGame: "Restricted Mover",
            ScopeNodeId: targetObjectId,
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("positionX", "40", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("positionY", "40", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("footprintWidthCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("footprintHeightCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("authoredRenderOrder", "1000", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("spatialType", nameof(RuntimeObjectSpatialTypes.SolidObject), GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Unrestricted),
                new RuntimePropertyDefinition("isMovable", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse)
            ],
            ScopeTokens: ["restricted", "mover"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
            MovementRestrictions: restrictions);

        return CreateSingleRoomWaypointSnapshot("RestrictedWaypoint", roomId, [target]);
    }

    private static RuntimeGameWorldSnapshot CreateRestrictedWaypointTotalCapSnapshot(Guid targetObjectId)
    {
        var roomId = Guid.NewGuid();
        var perDirectionRules = new Dictionary<string, RuntimeObjectMovementRestrictionRuleDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            ["N"] = new RuntimeObjectMovementRestrictionRuleDescriptor(MaxDistance: 2, AllowJumpOver: false),
            ["NE"] = new RuntimeObjectMovementRestrictionRuleDescriptor(MaxDistance: 2, AllowJumpOver: false),
            ["E"] = new RuntimeObjectMovementRestrictionRuleDescriptor(MaxDistance: 2, AllowJumpOver: false),
            ["SE"] = new RuntimeObjectMovementRestrictionRuleDescriptor(MaxDistance: 2, AllowJumpOver: false),
            ["S"] = new RuntimeObjectMovementRestrictionRuleDescriptor(MaxDistance: 2, AllowJumpOver: false),
            ["SW"] = new RuntimeObjectMovementRestrictionRuleDescriptor(MaxDistance: 2, AllowJumpOver: false),
            ["W"] = new RuntimeObjectMovementRestrictionRuleDescriptor(MaxDistance: 2, AllowJumpOver: false),
            ["NW"] = new RuntimeObjectMovementRestrictionRuleDescriptor(MaxDistance: 2, AllowJumpOver: false)
        };

        var restrictions = new RuntimeObjectMovementRestrictionsDescriptor(
            firstUnstacked: perDirectionRules,
            firstStacked: null,
            subsequentUnstacked: perDirectionRules,
            subsequentStacked: null,
            multiLegMaxTotalDistanceCells: 3);

        var target = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "RestrictedMover",
            ScopeNameInGame: "Restricted Mover",
            ScopeNodeId: targetObjectId,
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("positionX", "40", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("positionY", "40", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("footprintWidthCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("footprintHeightCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("authoredRenderOrder", "1000", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("spatialType", nameof(RuntimeObjectSpatialTypes.SolidObject), GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Unrestricted),
                new RuntimePropertyDefinition("isMovable", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse)
            ],
            ScopeTokens: ["restricted", "mover"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
            MovementRestrictions: restrictions);

        return CreateSingleRoomWaypointSnapshot("RestrictedWaypointTotalCap", roomId, [target]);
    }

    private static RuntimeGameWorldSnapshot CreateWaypointCollisionSnapshot(Guid targetObjectId, Guid blockerObjectId)
    {
        var roomId = Guid.NewGuid();
        var target = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "Mover",
            ScopeNameInGame: "Mover",
            ScopeNodeId: targetObjectId,
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("positionX", "40", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("positionY", "40", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("footprintWidthCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("footprintHeightCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("authoredRenderOrder", "1000", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("spatialType", nameof(RuntimeObjectSpatialTypes.SolidObject), GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Unrestricted),
                new RuntimePropertyDefinition("isMovable", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse)
            ],
            ScopeTokens: ["mover"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var blocker = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "Blocker",
            ScopeNameInGame: "Blocker",
            ScopeNodeId: blockerObjectId,
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("positionX", "120", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("positionY", "40", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("footprintWidthCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("footprintHeightCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("authoredRenderOrder", "1200", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("spatialType", nameof(RuntimeObjectSpatialTypes.SolidObject), GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Unrestricted),
                new RuntimePropertyDefinition("isMovable", "false", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse)
            ],
            ScopeTokens: ["blocker"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        return CreateSingleRoomWaypointSnapshot("WaypointCollision", roomId, [target, blocker]);
    }

    private static RuntimeGameWorldSnapshot CreateWaypointVariantOffsetSnapshot(Guid targetObjectId)
    {
        var roomId = Guid.NewGuid();
        var target = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "VariantMover",
            ScopeNameInGame: "Variant Mover",
            ScopeNodeId: targetObjectId,
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("positionX", "40", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("positionY", "40", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("footprintWidthCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("footprintHeightCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("authoredRenderOrder", "1000", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("spatialType", nameof(RuntimeObjectSpatialTypes.SolidObject), GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Unrestricted),
                new RuntimePropertyDefinition("isMovable", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse),
                new RuntimePropertyDefinition("selectedVariantFullImagePath", "piece.png", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Unrestricted),
                new RuntimePropertyDefinition("imageRotationDegrees", "0", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("effectiveImageScale", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric)
            ],
            ScopeTokens: ["variant", "mover"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
            ImageVariants:
            [
                new RuntimeObjectImageVariantDescriptor(
                    VariantName: "default",
                    FullImagePath: "piece.png",
                    IsDefault: true,
                    ImageLocalAlignmentRotationDegrees: 0,
                    ImageScale: 1,
                    ImageLocalAlignmentOffsetX: 6,
                    ImageLocalAlignmentOffsetY: -4)
            ]);

        return CreateSingleRoomWaypointSnapshot("WaypointVariantOffset", roomId, [target]);
    }

    private static RuntimeGameWorldSnapshot CreateWaypointStackLandingSnapshot(Guid moverObjectId, Guid supportObjectId)
    {
        var roomId = Guid.NewGuid();
        var mover = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "Mover",
            ScopeNameInGame: "Mover",
            ScopeNodeId: moverObjectId,
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("positionX", "40", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("positionY", "40", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("footprintWidthCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("footprintHeightCells", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("authoredRenderOrder", "1000", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("spatialType", nameof(RuntimeObjectSpatialTypes.SolidObject), GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Unrestricted),
                new RuntimePropertyDefinition("isMovable", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse),
                new RuntimePropertyDefinition("isStackable", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse),
                new RuntimePropertyDefinition("stackOrder", "0", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("heightInRoom", "0", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("authoredBaseHeightInRoom", "0", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric)
            ],
            ScopeTokens: ["mover"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var support = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "Support",
            ScopeNameInGame: "Support",
            ScopeNodeId: supportObjectId,
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("positionX", "120", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("positionY", "80", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("footprintWidthCells", "2", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("footprintHeightCells", "2", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("authoredRenderOrder", "1200", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("spatialType", nameof(RuntimeObjectSpatialTypes.SolidObject), GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Unrestricted),
                new RuntimePropertyDefinition("isMovable", "false", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse),
                new RuntimePropertyDefinition("isStackable", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse),
                new RuntimePropertyDefinition("stackOrder", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("heightInRoom", "0", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("authoredBaseHeightInRoom", "0", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric),
                new RuntimePropertyDefinition("objectHeightUnits", "1", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.Numeric)
            ],
            ScopeTokens: ["support"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        return CreateSingleRoomWaypointSnapshot("WaypointStackLanding", roomId, [mover, support]);
    }

    private static RuntimeGameWorldSnapshot CreateSingleRoomWaypointSnapshot(
        string projectName,
        Guid roomId,
        IReadOnlyList<RuntimeScopeNodeDescriptor> roomObjects)
    {
        var room = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "WaypointRoom",
            ScopeNameInGame: "Waypoint Room",
            ScopeNodeId: roomId,
            VariableDefinitions: CreateRoomVariableDefinitions(),
            ScopeTokens: new[] { "waypointroom" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: roomObjects);

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "Area",
            ScopeNameInGame: "Area",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "area" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { room });

        var country = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Country,
            Name: "Country",
            ScopeNameInGame: "Country",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "country" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { area });

        var planet = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Planet,
            Name: "Planet",
            ScopeNameInGame: "Planet",
            ScopeNodeId: null,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: new[] { "planet" },
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: new[] { country });

        return new RuntimeGameWorldSnapshot(
            ProjectName: projectName,
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: Array.Empty<string>(),
            GlobalDirectionals: Array.Empty<string>(),
            PlayerObjects: Array.Empty<RuntimeScopeNodeDescriptor>(),
            Planets: new[] { planet },
            StartingRoomId: roomId,
            ProjectRoomGridCellSize: 40);
    }

    private static RuntimeGameWorldSnapshot CreatePhaseReasonAndResumeSnapshot()
    {
        var roomId = Guid.NewGuid();
        var playerObjectId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var pageOneId = Guid.NewGuid();
        var pageTwoId = Guid.NewGuid();

        var nextPhaseAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "NextPhaseAction",
            ActionType = CommandActionType.NextPhase,
            NoVerbLinkage = false,
            Verbs = ["nextphase"],
            NextPhasePayload = new RuntimeNextPhaseActionPayload()
        };

        var setPhaseAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "SetPhaseAction",
            ActionType = CommandActionType.SetPhase,
            NoVerbLinkage = false,
            Verbs = ["setphase2"],
            SetPhasePayload = new RuntimeSetPhaseActionPayload("book.one.chapter.one.page.two")
        };

        var pageOne = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Page,
            Name: "Page One",
            ScopeNameInGame: "Page One",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["book.one.chapter.one.page.one"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
            ScopeNodeId: pageOneId,
            PhaseTitle: "Page One Title",
            PhaseTitlePresentationCueEffectKey: "text.hudoverlay.fadein.auto.6000",
            PhasePrologue: "Page One Prologue",
            PhaseProloguePresentationCueEffectKey: "text.hudoverlay.manualscroll.manualdismiss",
            PhaseNarrative: "Page One Narrative",
            PhaseNarrativePresentationCueEffectKey: "text.narrativedialog.archive");

        var pageTwo = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Page,
            Name: "Page Two",
            ScopeNameInGame: "Page Two",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["book.one.chapter.one.page.two"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
            ScopeNodeId: pageTwoId,
            PhaseTitle: "Page Two Title",
            PhaseTitlePresentationCueEffectKey: "text.hudoverlay.fadein.auto.6000",
            PhasePrologue: "Page Two Prologue",
            PhaseProloguePresentationCueEffectKey: "text.hudoverlay.manualscroll.manualdismiss",
            PhaseNarrative: "Page Two Narrative",
            PhaseNarrativePresentationCueEffectKey: "text.narrativedialog.archive");

        var chapter = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Chapter,
            Name: "Chapter One",
            ScopeNameInGame: "Chapter One",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["book.one.chapter.one"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [pageOne, pageTwo],
            ScopeNodeId: chapterId,
            PhaseTitle: "Chapter One Title",
            PhaseTitlePresentationCueEffectKey: "text.hudoverlay.fadein.auto.6000",
            PhasePrologue: "Chapter One Prologue",
            PhaseProloguePresentationCueEffectKey: "text.hudoverlay.manualscroll.manualdismiss",
            PhaseNarrative: "Chapter One Narrative",
            PhaseNarrativePresentationCueEffectKey: "text.narrativedialog.archive");

        var book = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Book,
            Name: "Book One",
            ScopeNameInGame: "Book One",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["book.one"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [chapter],
            ScopeNodeId: bookId,
            PhaseTitle: "Book One Title",
            PhaseTitlePresentationCueEffectKey: "text.hudoverlay.fadein.auto.6000",
            PhasePrologue: "Book One Prologue",
            PhaseProloguePresentationCueEffectKey: "text.hudoverlay.manualscroll.manualdismiss",
            PhaseNarrative: "Book One Narrative",
            PhaseNarrativePresentationCueEffectKey: "text.narrativedialog.archive");

        var room = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Room",
            ScopeNameInGame: "Room",
            ScopeNodeId: roomId,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["room"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "Area",
            ScopeNameInGame: "Area",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["area"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [room]);

        var country = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Country,
            Name: "Country",
            ScopeNameInGame: "Country",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["country"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [area]);

        var planet = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Planet,
            Name: "Planet",
            ScopeNameInGame: "Planet",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["planet"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [country]);

        var playerObject = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "Player",
            ScopeNameInGame: "Player",
            ScopeNodeId: playerObjectId,
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("isPlayer", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse)
            ],
            ScopeTokens: ["player"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        return new RuntimeGameWorldSnapshot(
            ProjectName: "PhaseReasonAndResume",
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: ["nextphase", "setphase2"],
            GlobalDirectionals: Array.Empty<string>(),
            PlayerObjects: [playerObject],
            Planets: [planet],
            StartingRoomId: roomId,
            GlobalAvailableActions: [nextPhaseAction, setPhaseAction],
            PhaseBooks: [book],
            StartingPhasePageId: pageOneId,
            ProjectRoomGridCellSize: 40);
    }

    private static RuntimeGameWorldSnapshot CreateCrossTierPhaseAdvanceSnapshot()
    {
        var roomId = Guid.NewGuid();
        var playerObjectId = Guid.NewGuid();
        var pageOneId = Guid.NewGuid();
        var pageTwoId = Guid.NewGuid();

        var nextPhaseAction = new RuntimeCommandActionDescriptor
        {
            Id = Guid.NewGuid(),
            Name = "NextPhaseAction",
            ActionType = CommandActionType.NextPhase,
            NoVerbLinkage = false,
            Verbs = ["nextphase"],
            NextPhasePayload = new RuntimeNextPhaseActionPayload()
        };

        var pageOne = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Page,
            Name: "Page One",
            ScopeNameInGame: "Page One",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["book.one.chapter.one.page.one"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
            ScopeNodeId: pageOneId,
            PhaseTitle: "Page One Title",
            PhasePrologue: "Page One Prologue",
            PhaseNarrative: "Page One Narrative");

        var pageTwo = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Page,
            Name: "Page Two",
            ScopeNameInGame: "Page Two",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["book.two.chapter.two.page.two"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
            ScopeNodeId: pageTwoId,
            PhaseTitle: "Page Two Title",
            PhasePrologue: "Page Two Prologue",
            PhaseNarrative: "Page Two Narrative");

        var chapterOne = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Chapter,
            Name: "Chapter One",
            ScopeNameInGame: "Chapter One",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["book.one.chapter.one"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [pageOne],
            ScopeNodeId: Guid.NewGuid(),
            PhaseTitle: "Chapter One Title",
            PhasePrologue: "Chapter One Prologue",
            PhaseNarrative: "Chapter One Narrative");

        var chapterTwo = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Chapter,
            Name: "Chapter Two",
            ScopeNameInGame: "Chapter Two",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["book.two.chapter.two"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [pageTwo],
            ScopeNodeId: Guid.NewGuid(),
            PhaseTitle: "Chapter Two Title",
            PhasePrologue: "Chapter Two Prologue",
            PhaseNarrative: "Chapter Two Narrative");

        var bookOne = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Book,
            Name: "Book One",
            ScopeNameInGame: "Book One",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["book.one"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [chapterOne],
            ScopeNodeId: Guid.NewGuid(),
            PhaseTitle: "Book One Title",
            PhasePrologue: "Book One Prologue",
            PhaseNarrative: "Book One Narrative");

        var bookTwo = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Book,
            Name: "Book Two",
            ScopeNameInGame: "Book Two",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["book.two"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [chapterTwo],
            ScopeNodeId: Guid.NewGuid(),
            PhaseTitle: "Book Two Title",
            PhasePrologue: "Book Two Prologue",
            PhaseNarrative: "Book Two Narrative");

        var room = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Room",
            ScopeNameInGame: "Room",
            ScopeNodeId: roomId,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["room"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "Area",
            ScopeNameInGame: "Area",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["area"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [room]);

        var country = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Country,
            Name: "Country",
            ScopeNameInGame: "Country",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["country"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [area]);

        var planet = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Planet,
            Name: "Planet",
            ScopeNameInGame: "Planet",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["planet"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [country]);

        var playerObject = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "Player",
            ScopeNameInGame: "Player",
            ScopeNodeId: playerObjectId,
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("isPlayer", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse)
            ],
            ScopeTokens: ["player"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        return new RuntimeGameWorldSnapshot(
            ProjectName: "PhaseCrossTierAdvance",
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: ["nextphase"],
            GlobalDirectionals: Array.Empty<string>(),
            PlayerObjects: [playerObject],
            Planets: [planet],
            StartingRoomId: roomId,
            GlobalAvailableActions: [nextPhaseAction],
            PhaseBooks: [bookOne, bookTwo],
            StartingPhasePageId: pageOneId,
            ProjectRoomGridCellSize: 40);
    }

    private static RuntimeGameWorldSnapshot CreatePhaseAmbientBaselineSnapshot()
    {
        var roomId = Guid.NewGuid();
        var playerObjectId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var phaseAmbientSoundId = Guid.NewGuid();

        var phaseAmbientLibraryEntry = new RuntimeSoundEffectLibraryEntryDto
        {
            SoundEffectId = phaseAmbientSoundId,
            SoundEffectKey = "sound.phase.ambient",
            DisplayName = "Phase Ambient",
            AssetRef = "assets/sounds/phase-ambient.mp3",
            SoundEffectLane = SoundEffectLane.Ambient,
            RepeatMode = SoundEffectRepeatMode.UntilCanceled,
            ReplayPolicy = SoundEffectReplayPolicy.IgnoreIfAlreadyPlaying
        };

        var page = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Page,
            Name: "Page One",
            ScopeNameInGame: "Page One",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["book.one.chapter.one.page.one"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>(),
            ScopeNodeId: pageId,
            PhaseAmbientSoundEffectId: phaseAmbientSoundId,
            PhaseAmbientTimerKey: "phase.ambient.timer",
            PhaseAmbienceMode: PhaseAmbienceMode.Replace,
            SoundEffectLibraryEntries: [phaseAmbientLibraryEntry]);

        var chapter = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Chapter,
            Name: "Chapter One",
            ScopeNameInGame: "Chapter One",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["book.one.chapter.one"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [page],
            ScopeNodeId: chapterId);

        var book = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Book,
            Name: "Book One",
            ScopeNameInGame: "Book One",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["book.one"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [chapter],
            ScopeNodeId: bookId);

        var room = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Room,
            Name: "Room",
            ScopeNameInGame: "Room",
            ScopeNodeId: roomId,
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["room"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        var area = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Area,
            Name: "Area",
            ScopeNameInGame: "Area",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["area"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [room]);

        var country = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Country,
            Name: "Country",
            ScopeNameInGame: "Country",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["country"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [area]);

        var planet = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.Planet,
            Name: "Planet",
            ScopeNameInGame: "Planet",
            VariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            ScopeTokens: ["planet"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: [country]);

        var playerObject = new RuntimeScopeNodeDescriptor(
            ScopeKind: ScopeNodeKind.GameObject,
            Name: "Player",
            ScopeNameInGame: "Player",
            ScopeNodeId: playerObjectId,
            VariableDefinitions:
            [
                new RuntimePropertyDefinition("isPlayer", "true", GamePropertyLifetime.Singleton, GamePropertyValueRestriction.TrueFalse)
            ],
            ScopeTokens: ["player"],
            AdditionalVerbs: Array.Empty<string>(),
            AdditionalDirectionals: Array.Empty<string>(),
            AvailableActions: Array.Empty<RuntimeCommandActionDescriptor>(),
            CommandPhrases: Array.Empty<RuntimeCommandPhraseDescriptor>(),
            Children: Array.Empty<RuntimeScopeNodeDescriptor>());

        return new RuntimeGameWorldSnapshot(
            ProjectName: "PhaseAmbientBaseline",
            GlobalVariableDefinitions: Array.Empty<RuntimePropertyDefinition>(),
            GlobalCommandVerbs: Array.Empty<string>(),
            GlobalDirectionals: Array.Empty<string>(),
            PlayerObjects: [playerObject],
            Planets: [planet],
            StartingRoomId: roomId,
            PhaseBooks: [book],
            StartingPhasePageId: pageId,
            ProjectRoomGridCellSize: 40);
    }

    private static bool TrySelectPrimaryByPoint(
        Storyboard.Shared.GameManager.GameManager manager,
        double x,
        double y,
        out string failureReason)
    {
        failureReason = string.Empty;

        var session = manager.CurrentSession;
        if (session is null)
        {
            failureReason = "No active runtime session is loaded.";
            return false;
        }

        var room = session.CurrentRoom;
        if (room is null)
        {
            failureReason = "Current room is unavailable.";
            return false;
        }

        if (!double.IsFinite(x) || !double.IsFinite(y) || x < 0 || y < 0)
        {
            failureReason = "Point intent coordinates must be finite non-negative values.";
            return false;
        }

        var cellSize = ResolveRoomCellSize(room);
        var column = (int)Math.Floor(x / cellSize);
        var row = (int)Math.Floor(y / cellSize);
        if (column < 0 || row < 0)
        {
            failureReason = "Point is outside room grid bounds.";
            return false;
        }

        var cellId = $"{ToLetters(column)}.{ToLetters(row)}";
        var selected = session
            .EnumerateCurrentRoomObjects(includeNested: true)
            .Where(node => node.ScopeNodeId.HasValue && ScopeOccupiesCell(node, cellId) && !IsPassiveSpatialObject(node))
            .OrderByDescending(ResolveEffectiveRenderOrder)
            .ThenByDescending(ResolveEffectiveHeight)
            .ThenBy(node => node.ScopeNodeId ?? Guid.Empty)
            .FirstOrDefault();

        if (selected is null || !selected.ScopeNodeId.HasValue || selected.ScopeNodeId.Value == Guid.Empty)
        {
            failureReason = $"No room object occupies cell '{cellId}' at point ({x:0.###}, {y:0.###}).";
            return false;
        }

        session.GlobalVariables.UpsertVariable(
            "activeRoomObjectId",
            selected.ScopeNodeId.Value.ToString("D"),
            GamePropertyLifetime.Singleton,
            GamePropertyValueRestriction.Unrestricted);
        session.GlobalVariables.UpsertVariable(
            "activeRoomObjectName",
            ResolveDisplayName(selected),
            GamePropertyLifetime.Singleton,
            GamePropertyValueRestriction.Unrestricted);

        return true;
    }

    private static int ResolveRoomCellSize(GameStateScopeNode room)
    {
        var names = new[] { "roomGridCellSize", "cellSize", "gridCellSize", "roomGridCellSizeOverride" };
        foreach (var name in names)
        {
            if (!room.Variables.TryGetVariable(name, out var value)
                || string.IsNullOrWhiteSpace(value.Value)
                || !int.TryParse(value.Value.Trim(), out var parsed)
                || parsed <= 0)
            {
                continue;
            }

            return parsed;
        }

        throw new InvalidOperationException($"Point selection failed: room '{room.Name}' is missing a valid room grid cell size.");
    }

    private static bool ScopeOccupiesCell(GameStateScopeNode node, string cellId)
    {
        if (string.IsNullOrWhiteSpace(cellId)
            || !node.Variables.TryGetVariable("occupiedCellIds", out var occupied)
            || string.IsNullOrWhiteSpace(occupied.Value))
        {
            return false;
        }

        return occupied.Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(candidate => string.Equals(candidate, cellId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPassiveSpatialObject(GameStateScopeNode node)
    {
        return node.Variables.TryGetVariable("spatialType", out var spatialType)
            && Enum.TryParse<RuntimeObjectSpatialTypes>(spatialType.Value, ignoreCase: true, out var parsed)
            && parsed == RuntimeObjectSpatialTypes.PassiveObject;
    }

    private static int ResolveEffectiveRenderOrder(GameStateScopeNode node)
    {
        if (node.Variables.TryGetVariable("effectiveRenderZOrder", out var effective)
            && int.TryParse(effective.Value, out var parsedEffective))
        {
            return parsedEffective;
        }

        if (node.RenderZOrder > 0)
        {
            return node.RenderZOrder;
        }

        return ResolveInt(node, 0, "authoredRenderOrder", "renderZOrder");
    }

    private static int ResolveEffectiveHeight(GameStateScopeNode node)
    {
        return ResolveInt(node, 0, "effectiveHeightInRoom", "heightInRoom", "authoredBaseHeightInRoom");
    }

    private static int ResolveInt(GameStateScopeNode node, int fallback, params string[] variableNames)
    {
        foreach (var variableName in variableNames)
        {
            if (!node.Variables.TryGetVariable(variableName, out var value)
                || string.IsNullOrWhiteSpace(value.Value)
                || !int.TryParse(value.Value.Trim(), out var parsed))
            {
                continue;
            }

            return parsed;
        }

        return fallback;
    }

    private static string ResolveDisplayName(GameStateScopeNode objectNode)
    {
        if (!string.IsNullOrWhiteSpace(objectNode.NameInGame))
        {
            return objectNode.NameInGame.Trim();
        }

        return objectNode.Name?.Trim() ?? string.Empty;
    }

    private static string ToLetters(int zeroBasedValue)
    {
        var value = Math.Max(0, zeroBasedValue);
        var characters = new Stack<char>();

        do
        {
            characters.Push((char)('A' + (value % 26)));
            value = (value / 26) - 1;
        }
        while (value >= 0);

        return new string(characters.ToArray());
    }

    private static HostLoadGameRuntimeProjectResult LoadRuntimeSnapshot(
        Storyboard.Shared.GameManager.GameManager manager,
        RuntimeGameWorldSnapshot snapshot,
        string sourceLabel = "in-memory")
    {
        var debugger = Assert.IsAssignableFrom<IHostRuntimeGameDebugger>(manager);
        return debugger.LoadRuntimeSnapshot(snapshot, sourceLabel);
    }

    private static HostSessionDataEnvelope PollLatestSessionData(Storyboard.Shared.GameManager.GameManager manager)
    {
        var polling = Assert.IsAssignableFrom<ISessionDeltaPolling>(manager);
        var pollResult = polling.GetSessionDeltas(
            new HostRequestContext(),
            watermark: null,
            batchProfile: HostSessionDeltaBatchProfile.Hot);

        Assert.Equal(HostSessionDeltaPollResultCode.Success, pollResult.ResultCode);
        return Assert.IsAssignableFrom<HostSessionDataEnvelope>(pollResult.SessionData);
    }

    private static Storyboard.Shared.GameManager.GameManager CreateSut()
    {
        IGameCommandPreprocessorService preprocessor = new GameCommandPreprocessorService();
        IActionScriptEvaluationService evaluator = new ActionScriptEvaluationService();
        IRuntimeCommandProcessorService processor = new GameCommandProcessorService(evaluator, preprocessor);
        IGameProjectRuntimeLoaderService loader = new GameProjectRuntimeLoaderService();
        IRuntimeLoadedGameLoader runtimeLoadedGameLoader = new CleanProjectRuntimeLoadedGameLoader(loader);
        return new Storyboard.Shared.GameManager.GameManager(runtimeLoadedGameLoader, processor);
    }

    private static string GetBirminghamCleanProjectPath()
    {
        var root = FindRepositoryRoot();
        return Path.Combine(root, "Samples", "Birmingham", "GameRuntimeJson", "Birmingham.sbr.runtime.json");
    }

    private static string GetChessDemoCleanProjectPath()
    {
        var root = FindRepositoryRoot();
        return Path.Combine(root, "Samples", "ChessDemo", "GameRuntimeJson", "ChessDemo.sbr.runtime.json");
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

}



