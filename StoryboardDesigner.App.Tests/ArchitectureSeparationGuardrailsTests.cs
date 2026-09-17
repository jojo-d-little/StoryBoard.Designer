using System.Text.RegularExpressions;

namespace StoryboardDesigner.App.Tests;

public sealed class ArchitectureSeparationGuardrailsTests
{
    private static readonly Regex IdentityPresenceBranchingPattern = new(
        @"\b(RoomId|ObjectId)\.HasValue\b|\b(RoomId|ObjectId)\s+is\s+null\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void SimulatorProject_DoesNotReferenceDesignerProjectOrNamespace()
    {
        var root = FindRepositoryRoot();
        var simulatorProjectPath = Path.Combine(root, "Storyboard.Simulator", "Storyboard.Simulator.csproj");
        var simulatorProjectText = File.ReadAllText(simulatorProjectPath);

        Assert.DoesNotContain("StoryboardDesigner.App", simulatorProjectText, StringComparison.Ordinal);
        Assert.Contains("..\\Storyboard.GameEngine\\Storyboard.GameEngine.csproj", simulatorProjectText, StringComparison.Ordinal);
    }

    [Fact]
    public void SimulatorSource_DoesNotUseDesignerNamespaces()
    {
        var root = FindRepositoryRoot();
        var simulatorRoot = Path.Combine(root, "Storyboard.Simulator");
        var sourceFiles = Directory.EnumerateFiles(simulatorRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
                           && !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase));

        foreach (var sourceFile in sourceFiles)
        {
            var text = File.ReadAllText(sourceFile);
            Assert.DoesNotContain("StoryboardDesigner.App", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SimulatorStartup_ComposesSharedRuntimeManager()
    {
        var root = FindRepositoryRoot();
        var appStartupPath = Path.Combine(root, "Storyboard.Simulator", "App.xaml.cs");
        var appStartupText = File.ReadAllText(appStartupPath);

        Assert.Contains("using Storyboard.Shared.GameManager;", appStartupText, StringComparison.Ordinal);
        Assert.Contains("RuntimeHostManagerFactory.Create()", appStartupText, StringComparison.Ordinal);
        Assert.DoesNotContain("new GameManager(", appStartupText, StringComparison.Ordinal);
        Assert.DoesNotContain("StoryboardDesigner.App", appStartupText, StringComparison.Ordinal);
    }

    [Fact]
    public void HostCompositionAndViewModels_UseRuntimeContractsHostManagerInterface()
    {
        var root = FindRepositoryRoot();

        var designerCompositionPath = Path.Combine(root, "StoryboardDesigner.App", "Composition", "DesignerAppCompositionFactory.cs");
        var designerViewModelPath = Path.Combine(root, "StoryboardDesigner.App", "ViewModels", "MainWindowViewModel.cs");
        var simulatorStartupPath = Path.Combine(root, "Storyboard.Simulator", "App.xaml.cs");
        var simulatorViewModelPath = Path.Combine(root, "Storyboard.Simulator", "ViewModels", "SimulatorViewModel.cs");

        var designerCompositionText = File.ReadAllText(designerCompositionPath);
        var designerViewModelText = File.ReadAllText(designerViewModelPath);
        var simulatorStartupText = File.ReadAllText(simulatorStartupPath);
        var simulatorViewModelText = File.ReadAllText(simulatorViewModelPath);

        Assert.DoesNotContain("using Storyboard.Shared.HostContracts;", designerCompositionText, StringComparison.Ordinal);
        Assert.DoesNotContain("IHostRuntimeCommandProcessorClient", designerCompositionText, StringComparison.Ordinal);

        Assert.DoesNotContain("using Storyboard.Shared.HostContracts;", designerViewModelText, StringComparison.Ordinal);
        Assert.DoesNotContain("IHostRuntimeCommandProcessorClient", designerViewModelText, StringComparison.Ordinal);

        Assert.Contains("using Storyboard.Shared.HostContracts;", simulatorStartupText, StringComparison.Ordinal);
        Assert.Contains("ISimulatorRuntimeCommandClient", simulatorStartupText, StringComparison.Ordinal);
        Assert.DoesNotContain("IHostRuntimeCommandProcessorClient", simulatorStartupText, StringComparison.Ordinal);

        Assert.Contains("using Storyboard.Shared.HostContracts;", simulatorViewModelText, StringComparison.Ordinal);
        Assert.Contains("ISimulatorRuntimeCommandClient", simulatorViewModelText, StringComparison.Ordinal);
        Assert.DoesNotContain("IHostRuntimeCommandProcessorClient", simulatorViewModelText, StringComparison.Ordinal);
    }

    [Fact]
    public void SimulatorViewModel_CurrentSessionUsage_IsLimitedToApprovedRuntimeStateProjection()
    {
        var root = FindRepositoryRoot();
        var simulatorViewModelPath = Path.Combine(root, "Storyboard.Simulator", "ViewModels", "SimulatorViewModel.cs");
        var lines = File.ReadAllLines(simulatorViewModelPath);

        var currentSessionLines = lines
            .Select((line, index) => new { LineNumber = index + 1, Text = line })
            .Where(entry => entry.Text.Contains("CurrentSession", StringComparison.Ordinal))
            .ToList();

        var approvedPattern = "var session = _gameStateSessionDebugger?.CurrentSession;";
        var disallowedMatches = currentSessionLines
            .Where(entry => !entry.Text.Contains(approvedPattern, StringComparison.Ordinal))
            .ToList();

        Assert.True(
            disallowedMatches.Count == 0,
            $"Simulator CurrentSession usage escaped the approved allowlist. Disallowed matches:{Environment.NewLine}"
            + string.Join(Environment.NewLine, disallowedMatches.Select(entry => $"L{entry.LineNumber}: {entry.Text.Trim()}")));

        var approvedMatches = currentSessionLines
            .Where(entry => entry.Text.Contains(approvedPattern, StringComparison.Ordinal))
            .ToList();
        Assert.Single(approvedMatches);
    }

    [Fact]
    public void HostCommandProcessedEventArgs_RemainsSingleHostContractDeclaration()
    {
        var root = FindRepositoryRoot();
        var sharedRoots = new[]
        {
            Path.Combine(root, "Storyboard.GameEngine"),
            Path.Combine(root, "Storyboard.Shared.Contracts")
        };
        var declarationToken = "public sealed partial class HostCommandProcessedEventArgs";

        var sourceFiles = sharedRoots
            .SelectMany(sharedRoot => Directory.EnumerateFiles(sharedRoot, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
                           && !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase));

        var declarationFiles = sourceFiles
            .Where(path => File.ReadAllText(path).Contains(declarationToken, StringComparison.Ordinal))
            .ToList();

        Assert.Single(declarationFiles);

        var expectedPath = Path.Combine(root, "Storyboard.Shared.Contracts", "HostContracts", "HostCommandDtos", "HostCommandProcessedEventArgs_contract.cs");
        Assert.Equal(expectedPath, declarationFiles[0]);

        var declarationText = File.ReadAllText(declarationFiles[0]);
        Assert.Contains("namespace Storyboard.Shared.HostContracts;", declarationText, StringComparison.Ordinal);
        Assert.Contains("public required HostProcessCommandResult Result { get; set; }", declarationText, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedManager_DoesNotDependOnDesignerModelTypes()
    {
        var root = FindRepositoryRoot();
        var sharedManagerPath = Path.Combine(root, "Storyboard.GameEngine", "GameManager", "GameManager.cs");
        var sharedManagerText = File.ReadAllText(sharedManagerPath);

        Assert.DoesNotContain("StoryboardDesigner.App.Models", sharedManagerText, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectModel", sharedManagerText, StringComparison.Ordinal);
        Assert.DoesNotContain("IScopedAwareNode", sharedManagerText, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeScopeKind", sharedManagerText, StringComparison.Ordinal);
        Assert.DoesNotContain("AppRuntimeCommandModelBridge", sharedManagerText, StringComparison.Ordinal);
    }

    [Fact]
    public void PreprocessorPath_DoesNotDependOnDesignerScopeTypes()
    {
        var root = FindRepositoryRoot();
        var preprocessRequestPath = FindExistingFile(
            root,
            Path.Combine("Storyboard.GameEngine", "GameServices", "Commands", "GameCommandPreprocessRequest.cs"),
            Path.Combine("Storyboard.GameEngine", "GameServices", "GameCommandPreprocessRequest.cs"),
            Path.Combine("StoryboardDesigner.App", "GameServices", "GameCommandPreprocessRequest.cs"));
        var preprocessorPath = FindExistingFile(
            root,
            Path.Combine("Storyboard.GameEngine", "GameServices", "Commands", "GameCommandPreprocessorService.cs"),
            Path.Combine("Storyboard.GameEngine", "GameServices", "GameCommandPreprocessorService.cs"),
            Path.Combine("StoryboardDesigner.App", "GameServices", "GameCommandPreprocessorService.cs"));

        var preprocessRequestText = File.ReadAllText(preprocessRequestPath);
        var preprocessorText = File.ReadAllText(preprocessorPath);

        Assert.DoesNotContain("StoryboardDesigner.App.Models", preprocessRequestText, StringComparison.Ordinal);
        Assert.DoesNotContain("StoryboardDesigner.App.Models", preprocessorText, StringComparison.Ordinal);
        Assert.DoesNotContain("IScopedAwareNode", preprocessRequestText, StringComparison.Ordinal);
        Assert.DoesNotContain("IScopedAwareNode", preprocessorText, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeScopeKind", preprocessorText, StringComparison.Ordinal);
    }

    [Fact]
    public void ProcessorPreprocessCallSites_DoNotUseScopeAdapter()
    {
        var root = FindRepositoryRoot();
        var processorPath = FindExistingFile(
            root,
            Path.Combine("Storyboard.GameEngine", "GameServices", "Commands", "GameCommandProcessorService.cs"),
            Path.Combine("Storyboard.GameEngine", "GameServices", "GameCommandProcessorService.cs"));
        var processorText = File.ReadAllText(processorPath);

        Assert.DoesNotContain("AsRuntimeScopeNode(", processorText, StringComparison.Ordinal);
        Assert.Contains("CurrentScopeNode = roomNode", processorText, StringComparison.Ordinal);
        Assert.Contains("CurrentScopeNode = runtimeChild", processorText, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedRuntimeProcessor_DoesNotDependOnDesignerModelGraphTypes()
    {
        var root = FindRepositoryRoot();
        var processorPath = FindExistingFile(
            root,
            Path.Combine("Storyboard.GameEngine", "GameServices", "Commands", "GameCommandProcessorService.cs"),
            Path.Combine("Storyboard.GameEngine", "GameServices", "GameCommandProcessorService.cs"));
        var processorText = File.ReadAllText(processorPath);

        Assert.DoesNotContain("ProjectModel", processorText, StringComparison.Ordinal);
        Assert.DoesNotContain("IScopedAwareNode", processorText, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeScopeKind", processorText, StringComparison.Ordinal);
        Assert.DoesNotContain("HostContext", processorText, StringComparison.Ordinal);
        Assert.DoesNotContain("AppRuntimeCommandModelBridge", processorText, StringComparison.Ordinal);
        Assert.DoesNotContain("AppRuntimeCommandProcessorBridge", processorText, StringComparison.Ordinal);
        Assert.DoesNotContain("TryBuildSynonymClarification(", processorText, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveSynonymCandidates(", processorText, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePath_DoesNotReintroduceAppSpecificRequestSubclass()
    {
        var root = FindRepositoryRoot();
        var deletedRequestPath = Path.Combine(root, "StoryboardDesigner.App", "GameServices", "AppRuntimeCommandProcessingRequest.cs");

        Assert.False(File.Exists(deletedRequestPath), "AppRuntimeCommandProcessingRequest.cs should remain deleted.");
    }

    [Fact]
    public void RuntimePath_DoesNotReintroduceAppCommandProcessorBridgeFiles()
    {
        var root = FindRepositoryRoot();
        var appBridgePath = Path.Combine(root, "StoryboardDesigner.App", "GameServices", "AppRuntimeCommandProcessorBridge.cs");
        var appModelBridgePath = Path.Combine(root, "StoryboardDesigner.App", "GameServices", "AppRuntimeCommandModelBridge.cs");

        Assert.False(File.Exists(appBridgePath), "AppRuntimeCommandProcessorBridge.cs should remain deleted.");
        Assert.False(File.Exists(appModelBridgePath), "AppRuntimeCommandModelBridge.cs should remain deleted.");
    }

    [Fact]
    public void RuntimePath_DoesNotReintroduceSessionModelCompatibilityExtensions()
    {
        var root = FindRepositoryRoot();
        var compatibilityPath = Path.Combine(root, "StoryboardDesigner.App", "GameStateData", "GameStateSessionModelCompatibilityExtensions.cs");

        Assert.False(File.Exists(compatibilityPath), "GameStateSessionModelCompatibilityExtensions.cs should remain deleted.");
    }

    [Fact]
    public void RuntimePath_DoesNotReintroduceLegacyProjectModelRuntimeSnapshotMapper()
    {
        var root = FindRepositoryRoot();
        var mapperPath = Path.Combine(root, "StoryboardDesigner.App", "GameServices", "ProjectModelRuntimeSnapshotMapper.cs");

        Assert.False(File.Exists(mapperPath), "ProjectModelRuntimeSnapshotMapper.cs should remain deleted.");
    }

    [Fact]
    public void DesignerJsonExportService_DoesNotReintroduceAreaFolderExportSurface()
    {
        var root = FindRepositoryRoot();
        var jsonExportContractPath = Path.Combine(root, "StoryboardDesigner.App", "Services", "IJsonExportService.cs");
        var jsonExportImplementationPath = Path.Combine(root, "StoryboardDesigner.App", "Services", "JsonExportService.cs");
        var removedAreaMapDtoPath = Path.Combine(root, "StoryboardDesigner.App", "Serialization", "AreaMapExportDto.cs");
        var removedRoomExportDtoPath = Path.Combine(root, "StoryboardDesigner.App", "Serialization", "RoomExportDto.cs");

        var jsonExportContractText = File.ReadAllText(jsonExportContractPath);
        var jsonExportImplementationText = File.ReadAllText(jsonExportImplementationPath);

        Assert.DoesNotContain("ExportAreaToFolder(", jsonExportContractText, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportAreaToFolder(", jsonExportImplementationText, StringComparison.Ordinal);
        Assert.DoesNotContain("SerializeAreaMap(", jsonExportContractText, StringComparison.Ordinal);
        Assert.DoesNotContain("SerializeAreaMap(", jsonExportImplementationText, StringComparison.Ordinal);
        Assert.DoesNotContain("SerializeRoom(", jsonExportContractText, StringComparison.Ordinal);
        Assert.DoesNotContain("SerializeRoom(", jsonExportImplementationText, StringComparison.Ordinal);
        Assert.False(File.Exists(removedAreaMapDtoPath), "AreaMapExportDto.cs should remain deleted.");
        Assert.False(File.Exists(removedRoomExportDtoPath), "RoomExportDto.cs should remain deleted.");
    }

    [Fact]
    public void DesignerValidationOrchestration_UsesExecutionLayerProcessingContract()
    {
        var root = FindRepositoryRoot();
        var fileCommandsPath = Path.Combine(root, "StoryboardDesigner.App", "ViewModels", "MainWindowViewModel.FileCommands.cs");
        var fileCommandsText = File.ReadAllText(fileCommandsPath);

        Assert.Contains("ValidationIssueRunProcessor.Process(_project, executionResult.Issues, request)", fileCommandsText, StringComparison.Ordinal);
        Assert.DoesNotContain("private static IReadOnlyList<ProjectValidationIssue> FilterValidationIssuesForScope", fileCommandsText, StringComparison.Ordinal);
        Assert.DoesNotContain("private static IReadOnlyList<ProjectValidationIssue> ApplyCompletionMode", fileCommandsText, StringComparison.Ordinal);
    }

    [Fact]
    public void DesignerFileCommands_RouteCoreFileWorkflowsThroughShellOrchestrator()
    {
        var root = FindRepositoryRoot();
        var fileCommandsPath = Path.Combine(root, "StoryboardDesigner.App", "ViewModels", "MainWindowViewModel.FileCommands.cs");
        var fileCommandsText = File.ReadAllText(fileCommandsPath);

        Assert.Contains("orchestrator.CreateProjectAsync(", fileCommandsText, StringComparison.Ordinal);
        Assert.Contains("orchestrator.OpenProjectAsync(", fileCommandsText, StringComparison.Ordinal);
        Assert.Contains("orchestrator.SaveProjectAsync(", fileCommandsText, StringComparison.Ordinal);
        Assert.Contains("orchestrator.SaveProjectAsAsync(", fileCommandsText, StringComparison.Ordinal);
        Assert.Contains("CloseProjectAsync", File.ReadAllText(Path.Combine(root, "StoryboardDesigner.App", "Orchestration", "MainWindowShellOrchestrator.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void DesignerComposition_WiresShellDiagnosticsSinkAndAttachesOrchestrator()
    {
        var root = FindRepositoryRoot();
        var compositionFactoryPath = Path.Combine(root, "StoryboardDesigner.App", "Composition", "DesignerAppCompositionFactory.cs");
        var compositionFactoryText = File.ReadAllText(compositionFactoryPath);

        Assert.Contains("new MainWindowOutputConsoleShellDiagnosticsSink(mainViewModel)", compositionFactoryText, StringComparison.Ordinal);
        Assert.Contains("new SaveGuardPolicyService()", compositionFactoryText, StringComparison.Ordinal);
        Assert.Contains("new MainWindowShellOrchestrator(mainViewModel, diagnosticsSink, saveGuardPolicyService)", compositionFactoryText, StringComparison.Ordinal);
        Assert.Contains("mainViewModel.AttachShellOrchestrator(shellOrchestrator)", compositionFactoryText, StringComparison.Ordinal);
    }

    [Fact]
    public void DesignerShellOrchestrator_UsesSaveGuardPolicyServiceForCloseWorkflow()
    {
        var root = FindRepositoryRoot();
        var orchestratorPath = Path.Combine(root, "StoryboardDesigner.App", "Orchestration", "MainWindowShellOrchestrator.cs");
        var orchestratorText = File.ReadAllText(orchestratorPath);

        Assert.Contains("ISaveGuardPolicyService", orchestratorText, StringComparison.Ordinal);
        Assert.Contains("EvaluateCloseProjectPolicy(", orchestratorText, StringComparison.Ordinal);
        Assert.Contains("CloseWorkflowSaveGuardDecisionKind.Block", orchestratorText, StringComparison.Ordinal);
        Assert.Contains("CloseWorkflowSaveGuardDecisionKind.AttemptSave", orchestratorText, StringComparison.Ordinal);
    }

    [Fact]
    public void DesignerFileCommands_DoNotUseSynchronousAwaiterBlockingForOrchestratorCalls()
    {
        var root = FindRepositoryRoot();
        var fileCommandsPath = Path.Combine(root, "StoryboardDesigner.App", "ViewModels", "MainWindowViewModel.FileCommands.cs");
        var fileCommandsText = File.ReadAllText(fileCommandsPath);

        Assert.DoesNotContain(".GetAwaiter().GetResult()", fileCommandsText, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedRuntime_IdentityPresenceBranchingPatternCount_DoesNotIncrease()
    {
        var root = FindRepositoryRoot();
        var expectedCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [Path.Combine("Storyboard.GameEngine", "GameServices", "Actions", "GameActions", "RuntimeActionExecutable", "RuntimeCommandActionExecutor.BreakCompositeItemExecutableAction.cs")] = 4,
            [Path.Combine("Storyboard.GameEngine", "GameServices", "Actions", "GameActions", "RuntimeActionExecutable", "RuntimeCommandActionExecutor.BuildCompositeByPartsExecutableAction.cs")] = 3,
            [Path.Combine("Storyboard.GameEngine", "GameServices", "Actions", "GameActions", "RuntimeActionExecutable", "RuntimeCommandActionExecutor.BuildCompositeByTargetExecutableAction.cs")] = 3,
            [Path.Combine("Storyboard.GameEngine", "GameServices", "Actions", "GameActions", "RuntimeActionExecutable", "RuntimeCommandActionExecutor.NavigateDirectionExecutableAction.cs")] = 1,
            [Path.Combine("Storyboard.GameEngine", "GameServices", "Commands", "GameCommandProcessorService.cs")] = 0
        };

        foreach (var (relativePath, expectedCount) in expectedCounts)
        {
            var fullPath = Path.Combine(root, relativePath);
            var text = File.ReadAllText(fullPath);
            var actualCount = IdentityPresenceBranchingPattern.Matches(text).Count;

            Assert.True(
                actualCount <= expectedCount,
                $"Identity-presence branching pattern count increased in {relativePath}. Expected <= {expectedCount}, actual {actualCount}.");
        }
    }

    [Fact]
    public void SharedProject_ClassInterfaceFiles_FollowOneTopLevelTypePerFileAndNameMatch()
    {
        var root = FindRepositoryRoot();
        var sharedRoot = Path.Combine(root, "Storyboard.GameEngine");

        var declarationPattern = new Regex(
            "^(public|internal|private|protected)?\\s*(sealed|abstract|static|partial|readonly)?\\s*(sealed|abstract|static|partial|readonly)?\\s*(class|interface)\\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        var sourceFiles = Directory.EnumerateFiles(sharedRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
                           && !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase));

        foreach (var sourceFile in sourceFiles)
        {
            var declarations = new List<(string Kind, string Name, bool IsPartial, int Line)>();
            var lines = File.ReadAllLines(sourceFile);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length > 0 && char.IsWhiteSpace(line[0]))
                {
                    continue;
                }

                var match = declarationPattern.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var isPartial = line.Contains(" partial ", StringComparison.Ordinal)
                    || line.Contains("partial class", StringComparison.Ordinal)
                    || line.Contains("partial interface", StringComparison.Ordinal);

                declarations.Add((
                    Kind: match.Groups[4].Value,
                    Name: match.Groups[5].Value,
                    IsPartial: isPartial,
                    Line: i + 1));
            }

            Assert.True(
                declarations.Count <= 1,
                $"Expected at most one top-level class/interface declaration per file in Storyboard.GameEngine. File '{sourceFile}' has {declarations.Count} declarations: {string.Join(", ", declarations.Select(d => $"{d.Kind} {d.Name}@{d.Line}"))}.");

            if (declarations.Count == 0)
            {
                continue;
            }

            var declaration = declarations[0];
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFile);

            if (declaration.IsPartial)
            {
                Assert.True(
                    fileNameWithoutExtension.StartsWith(declaration.Name, StringComparison.Ordinal),
                    $"Expected partial type file name to start with type name. File '{sourceFile}' should start with '{declaration.Name}'.");
            }
            else
            {
                Assert.Equal(
                    declaration.Name,
                    fileNameWithoutExtension);
            }
        }
    }

    [Fact]
    public void DesignerProject_ClassInterfaceFiles_FollowOneTopLevelTypePerFileAndNameMatch()
    {
        var root = FindRepositoryRoot();
        var designerRoot = Path.Combine(root, "StoryboardDesigner.App");

        var declarationPattern = new Regex(
            "^(public|internal|private|protected)?\\s*(sealed|abstract|static|partial|readonly)?\\s*(sealed|abstract|static|partial|readonly)?\\s*(class|interface)\\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        var sourceFiles = Directory.EnumerateFiles(designerRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
                           && !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)
                           && !path.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase));

        foreach (var sourceFile in sourceFiles)
        {
            var declarations = new List<(string Kind, string Name, bool IsPartial, int Line)>();
            var lines = File.ReadAllLines(sourceFile);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length > 0 && char.IsWhiteSpace(line[0]))
                {
                    continue;
                }

                var match = declarationPattern.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var isPartial = line.Contains(" partial ", StringComparison.Ordinal)
                    || line.Contains("partial class", StringComparison.Ordinal)
                    || line.Contains("partial interface", StringComparison.Ordinal);

                declarations.Add((
                    Kind: match.Groups[4].Value,
                    Name: match.Groups[5].Value,
                    IsPartial: isPartial,
                    Line: i + 1));
            }

            Assert.True(
                declarations.Count <= 1,
                $"Expected at most one top-level class/interface declaration per file in StoryboardDesigner.App. File '{sourceFile}' has {declarations.Count} declarations: {string.Join(", ", declarations.Select(d => $"{d.Kind} {d.Name}@{d.Line}"))}.");

            if (declarations.Count == 0)
            {
                continue;
            }

            var declaration = declarations[0];
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFile);

            if (declaration.IsPartial)
            {
                Assert.True(
                    fileNameWithoutExtension.StartsWith(declaration.Name, StringComparison.Ordinal),
                    $"Expected partial type file name to start with type name. File '{sourceFile}' should start with '{declaration.Name}'.");
            }
            else
            {
                Assert.Equal(
                    declaration.Name,
                    fileNameWithoutExtension);
            }
        }
    }

    [Fact]
    public void DesignerMainWindow_DoesNotOwnPendingDestinationArrowState()
    {
        var root = FindRepositoryRoot();
        var mainWindowCodeBehindPath = Path.Combine(root, "StoryboardDesigner.App", "MainWindow.xaml.cs");
        var areaMapCanvasPath = Path.Combine(root, "StoryboardDesigner.App", "Views", "Controls", "AreaMapCanvas.xaml.cs");
        var areaMapWorkspacePath = Path.Combine(root, "StoryboardDesigner.App", "Views", "Controls", "AreaMapDesignerWorkspace.xaml.cs");

        var mainWindowCodeBehindText = File.ReadAllText(mainWindowCodeBehindPath);
        var areaMapCanvasText = File.ReadAllText(areaMapCanvasPath);
        var areaMapWorkspaceText = File.ReadAllText(areaMapWorkspacePath);

        Assert.DoesNotContain("_pendingDestinationArrow", mainWindowCodeBehindText, StringComparison.Ordinal);
        Assert.DoesNotContain("TryApplyPendingDestinationForPlacement(", mainWindowCodeBehindText, StringComparison.Ordinal);
        Assert.DoesNotContain("SetPendingDestinationArrow(", mainWindowCodeBehindText, StringComparison.Ordinal);
        Assert.DoesNotContain("ClearPendingDestinationArrow(", mainWindowCodeBehindText, StringComparison.Ordinal);

        Assert.Contains("AreaMapDesignerWorkspaceControl?.ClearPendingDestinationMode()", mainWindowCodeBehindText, StringComparison.Ordinal);
        Assert.Contains("private NavigationArrowViewModel? _pendingDestinationArrow;", areaMapCanvasText, StringComparison.Ordinal);
        Assert.Contains("internal bool ClearPendingDestinationMode()", areaMapWorkspaceText, StringComparison.Ordinal);
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

    private static string FindExistingFile(string root, params string[] relativePaths)
    {
        foreach (var relativePath in relativePaths)
        {
            var fullPath = Path.Combine(root, relativePath);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        throw new FileNotFoundException($"Could not find expected file in any known location: {string.Join(", ", relativePaths)}");
    }
}
