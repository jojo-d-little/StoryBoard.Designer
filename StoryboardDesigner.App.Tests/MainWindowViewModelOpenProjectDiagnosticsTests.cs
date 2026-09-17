using System.Text;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelOpenProjectDiagnosticsTests
{
    [Fact]
    public void OpenProject_WritesDesignerLoadDiagnosticsCsv_WithRotationNormalizationWarningOnly()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "DesignerLoadDiagnostics.sbe.json");
            var projectJson = """
            {
              "name": "DesignerLoadDiagnostics"
            }
            """;
            File.WriteAllText(projectFilePath, projectJson, Encoding.UTF8);

                        var globalsFilePath = Path.Combine(tempRoot, "DesignerLoadDiagnostics.sbe.globals.json");
                        File.WriteAllText(globalsFilePath, "{}", Encoding.UTF8);

            var project = new ProjectModel
            {
                Name = "DesignerLoadDiagnostics",
                RoomTemplates =
                [
                    new Room
                    {
                        Name = "Template Room",
                        GameObjects =
                        [
                            new GameObject
                            {
                                Name = "Rotated",
                                ImageRotationDegrees = 47
                            }
                        ]
                    }
                ]
            };

            var projectUi = new ProjectUiServiceStub();
            var treeContext = new TreeContextInteractionServiceStub();
            var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(project, treeContext, projectUi);
            bundle.JsonExport.ProjectModelToLoad = project;

            var opened = bundle.ViewModel.OpenProject(projectFilePath);

            Assert.True(opened);

            var reportPath = Path.Combine(tempRoot, "DesignerLoadDiagnostics.designer-load-diagnostics.csv");
            Assert.True(File.Exists(reportPath));

            var report = File.ReadAllText(reportPath, Encoding.UTF8);
            Assert.Contains("timestampUtc,severity,event,sourcePath,message", report, StringComparison.Ordinal);
            Assert.Contains("Project load completed.", report, StringComparison.Ordinal);
            Assert.DoesNotContain("default verbs were injected", report, StringComparison.Ordinal);
            Assert.DoesNotContain("default directionals were injected", report, StringComparison.Ordinal);
            Assert.Contains("Snapped 1 room object rotation value(s) to cardinal quarter-turn angles.", report, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void OpenProject_FailsWhenGlobalNodeMissing_EvenIfRootHasLegacyGlobalFields()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "LegacyRootFallback.sbe.json");
            var projectJson = """
            {
              "name": "LegacyRootFallback",
              "commandVerbs": ["inspect"],
              "availableGameActions": []
            }
            """;
            File.WriteAllText(projectFilePath, projectJson, Encoding.UTF8);

            var project = new ProjectModel
            {
                Name = "LegacyRootFallback"
            };

            var projectUi = new ProjectUiServiceStub();
            var treeContext = new TreeContextInteractionServiceStub();
            var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(project, treeContext, projectUi);
            bundle.JsonExport.ProjectModelToLoad = project;

            var opened = bundle.ViewModel.OpenProject(projectFilePath);

            Assert.False(opened);

            var reportPath = Path.Combine(tempRoot, "LegacyRootFallback.designer-load-diagnostics.csv");
            Assert.True(File.Exists(reportPath));

            var report = File.ReadAllText(reportPath, Encoding.UTF8);
            Assert.Contains("global-node.missing.v1: Global node file was not found during load.", report, StringComparison.Ordinal);
            Assert.DoesNotContain("global-node.fallback-used.v1:", report, StringComparison.Ordinal);
            Assert.DoesNotContain("global-node.conflict-root-vs-global.v1:", report, StringComparison.Ordinal);
            Assert.DoesNotContain("global-node.malformed.v1:", report, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void OpenProject_WritesGlobalNodeConflictDiagnostic_WhenGlobalNodeAndRootBothContainGlobalOwnedFields()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "GlobalNodeConflict.sbe.json");
            var projectJson = """
            {
              "name": "GlobalNodeConflict",
              "commandVerbs": ["inspect"]
            }
            """;
            File.WriteAllText(projectFilePath, projectJson, Encoding.UTF8);

            var globalsFilePath = Path.Combine(tempRoot, "GlobalNodeConflict.sbe.globals.json");
            var globalsJson = """
            {
              "scopeKind": "Global",
              "commandVerbs": ["look"]
            }
            """;
            File.WriteAllText(globalsFilePath, globalsJson, Encoding.UTF8);

            var project = new ProjectModel
            {
                Name = "GlobalNodeConflict"
            };

            var projectUi = new ProjectUiServiceStub();
            var treeContext = new TreeContextInteractionServiceStub();
            var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(project, treeContext, projectUi);
            bundle.JsonExport.ProjectModelToLoad = project;

            var opened = bundle.ViewModel.OpenProject(projectFilePath);
            Assert.True(opened);

            var reportPath = Path.Combine(tempRoot, "GlobalNodeConflict.designer-load-diagnostics.csv");
            var report = File.ReadAllText(reportPath, Encoding.UTF8);

            Assert.Contains("global-node.conflict-root-vs-global.v1: Both global node and root contain global-owned fields; global node values are authoritative.", report, StringComparison.Ordinal);
            Assert.DoesNotContain("global-node.missing.v1:", report, StringComparison.Ordinal);
            Assert.DoesNotContain("global-node.fallback-used.v1:", report, StringComparison.Ordinal);
            Assert.DoesNotContain("global-node.malformed.v1:", report, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void OpenProject_WritesGlobalNodeMissingDiagnostic_WithoutFallback_WhenRootHasNoLegacyGlobalFields()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "GlobalNodeMissingOnly.sbe.json");
            var projectJson = """
            {
              "name": "GlobalNodeMissingOnly"
            }
            """;
            File.WriteAllText(projectFilePath, projectJson, Encoding.UTF8);

            var project = new ProjectModel
            {
                Name = "GlobalNodeMissingOnly"
            };

            var projectUi = new ProjectUiServiceStub();
            var treeContext = new TreeContextInteractionServiceStub();
            var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(project, treeContext, projectUi);
            bundle.JsonExport.ProjectModelToLoad = project;

            var opened = bundle.ViewModel.OpenProject(projectFilePath);
            Assert.False(opened);

            var reportPath = Path.Combine(tempRoot, "GlobalNodeMissingOnly.designer-load-diagnostics.csv");
            var report = File.ReadAllText(reportPath, Encoding.UTF8);

            Assert.Contains("global-node.missing.v1: Global node file was not found during load.", report, StringComparison.Ordinal);
            Assert.DoesNotContain("global-node.fallback-used.v1:", report, StringComparison.Ordinal);
            Assert.DoesNotContain("global-node.conflict-root-vs-global.v1:", report, StringComparison.Ordinal);
            Assert.DoesNotContain("global-node.malformed.v1:", report, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void OpenProject_WritesGlobalNodeMalformedDiagnostic_WhenGlobalNodeJsonIsInvalid()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "MalformedGlobalNode.sbe.json");
            var projectJson = """
            {
              "name": "MalformedGlobalNode",
              "commandVerbs": ["look"]
            }
            """;
            File.WriteAllText(projectFilePath, projectJson, Encoding.UTF8);

            var globalsFilePath = Path.Combine(tempRoot, "MalformedGlobalNode.sbe.globals.json");
            File.WriteAllText(globalsFilePath, "{ not-valid-json", Encoding.UTF8);

            var project = new ProjectModel
            {
                Name = "MalformedGlobalNode"
            };

            var projectUi = new ProjectUiServiceStub();
            var treeContext = new TreeContextInteractionServiceStub();
            var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(project, treeContext, projectUi);
            bundle.JsonExport.ProjectModelToLoad = project;

            var opened = bundle.ViewModel.OpenProject(projectFilePath);
            Assert.False(opened);

            var reportPath = Path.Combine(tempRoot, "MalformedGlobalNode.designer-load-diagnostics.csv");
            var report = File.ReadAllText(reportPath, Encoding.UTF8);

            Assert.Contains("global-node.malformed.v1: Global node file exists but is malformed JSON or has an invalid shape.", report, StringComparison.Ordinal);
            Assert.DoesNotContain("global-node.missing.v1:", report, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void OpenProject_GlobalNodeWithModernVocabularyFields_DoesNotInjectDefaults()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "GlobalNodeModernVocabulary.sbe.json");
            var projectJson = """
            {
              "name": "GlobalNodeModernVocabulary"
            }
            """;
            File.WriteAllText(projectFilePath, projectJson, Encoding.UTF8);

            var globalsFilePath = Path.Combine(tempRoot, "GlobalNodeModernVocabulary.sbe.globals.json");
            var globalsJson = """
            {
              "scopeKind": "Global",
              "additionalVerbs": ["look", "inspect", "nudge"],
              "additionalDirectionals": ["north", "south"]
            }
            """;
            File.WriteAllText(globalsFilePath, globalsJson, Encoding.UTF8);

            var project = new ProjectModel
            {
                Name = "GlobalNodeModernVocabulary",
                CommandVerbs = ["look", "inspect", "nudge"],
                Directionals = ["north", "south"]
            };

            var projectUi = new ProjectUiServiceStub();
            var treeContext = new TreeContextInteractionServiceStub();
            var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(project, treeContext, projectUi);
            bundle.JsonExport.ProjectModelToLoad = project;

            var opened = bundle.ViewModel.OpenProject(projectFilePath);

            Assert.True(opened);

            var reportPath = Path.Combine(tempRoot, "GlobalNodeModernVocabulary.designer-load-diagnostics.csv");
            Assert.True(File.Exists(reportPath));

            var report = File.ReadAllText(reportPath, Encoding.UTF8);
            Assert.DoesNotContain("commandVerbs property was missing; default verbs were injected.", report, StringComparison.Ordinal);
            Assert.DoesNotContain("directionals property was missing; default directionals were injected.", report, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
