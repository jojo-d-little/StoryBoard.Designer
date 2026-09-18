using System.Text.RegularExpressions;

namespace StoryboardDesigner.App.Tests;

public sealed class ArchitectureSeparationGuardrailsTests
{
    private static readonly Regex IdentityPresenceBranchingPattern = new(
        @"\b(RoomId|ObjectId)\.HasValue\b|\b(RoomId|ObjectId)\s+is\s+null\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
