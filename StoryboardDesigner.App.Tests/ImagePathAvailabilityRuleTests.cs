using System.Text;
using System.Text.Json;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class ImagePathAvailabilityRuleTests
{
    [Fact]
    public void Evaluate_SourceMissingAndExportExists_ReturnsWarning()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var configuredPath = Path.Combine(tempRoot, "missing", "room-image.png");
            var projectFilePath = Path.Combine(tempRoot, "ImageRule.sbe.json");
            var project = BuildProjectWithRoomImage(configuredPath);

            var exportedRelativePath = "assets/images/_shared/hash__room-image.png";
            var exportedAbsolutePath = Path.Combine(tempRoot, "GameRuntimeJson", "assets", "images", "_shared", "hash__room-image.png");
            Directory.CreateDirectory(Path.GetDirectoryName(exportedAbsolutePath) ?? tempRoot);
            File.WriteAllBytes(exportedAbsolutePath, Encoding.UTF8.GetBytes("export"));

            WriteAssetsManifest(tempRoot, configuredPath, exportedRelativePath);

            var issues = ExecuteRule(project, projectFilePath);

            var issue = Assert.Single(issues);
            Assert.Equal("IMG-001", issue.RuleId);
            Assert.Equal(ValidationSeverity.Warning, issue.Severity);
            Assert.Contains("source image is missing, but an exported fallback image exists", issue.Description, StringComparison.OrdinalIgnoreCase);
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
    public void Evaluate_SourceMissingAndExportMissing_ReturnsError()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var configuredPath = Path.Combine(tempRoot, "missing", "room-image.png");
            var projectFilePath = Path.Combine(tempRoot, "ImageRule.sbe.json");
            var project = BuildProjectWithRoomImage(configuredPath);

            var issues = ExecuteRule(project, projectFilePath);

            var issue = Assert.Single(issues);
            Assert.Equal("IMG-001", issue.RuleId);
            Assert.Equal(ValidationSeverity.Error, issue.Severity);
            Assert.Contains("source image is missing and no exported fallback image was found", issue.Description, StringComparison.OrdinalIgnoreCase);
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
    public void Evaluate_SourceExistsAndExportMissing_ReturnsWarning()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sourcePath = Path.Combine(tempRoot, "source", "room-image.png");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath) ?? tempRoot);
            File.WriteAllBytes(sourcePath, Encoding.UTF8.GetBytes("source"));

            var projectFilePath = Path.Combine(tempRoot, "ImageRule.sbe.json");
            var project = BuildProjectWithRoomImage(sourcePath);

            var issues = ExecuteRule(project, projectFilePath);

            var issue = Assert.Single(issues);
            Assert.Equal("IMG-001", issue.RuleId);
            Assert.Equal(ValidationSeverity.Warning, issue.Severity);
            Assert.Contains("source image exists but exported fallback image is missing", issue.Description, StringComparison.OrdinalIgnoreCase);
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
    public void Evaluate_ObjectTemplateSourceExistsAndExportMissing_DoesNotWarn()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sourcePath = Path.Combine(tempRoot, "source", "door-closed.png");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath) ?? tempRoot);
            File.WriteAllBytes(sourcePath, Encoding.UTF8.GetBytes("source"));

            var projectFilePath = Path.Combine(tempRoot, "ImageRule.sbe.json");
            var project = BuildProjectWithObjectTemplateImage(sourcePath);

            var issues = ExecuteRule(project, projectFilePath);

            Assert.Empty(issues);
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
    public void Evaluate_SourceMissingWithAmbiguousLibraryMatchesAndExportExists_ReturnsAmbiguityWarning()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var configuredPath = Path.Combine(tempRoot, "missing", "room-image.png");
            var projectFilePath = Path.Combine(tempRoot, "ImageRule.sbe.json");
            var project = BuildProjectWithRoomImage(configuredPath);

            var firstLocal = Path.Combine(tempRoot, "project-source-images", "rooms", "room-a", "room-image.png");
            var secondLocal = Path.Combine(tempRoot, "project-source-images", "rooms", "room-b", "room-image.png");
            Directory.CreateDirectory(Path.GetDirectoryName(firstLocal) ?? tempRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(secondLocal) ?? tempRoot);
            File.WriteAllBytes(firstLocal, Encoding.UTF8.GetBytes("first"));
            File.WriteAllBytes(secondLocal, Encoding.UTF8.GetBytes("second"));

            var exportedRelativePath = "assets/images/_shared/hash__room-image.png";
            var exportedAbsolutePath = Path.Combine(tempRoot, "GameRuntimeJson", "assets", "images", "_shared", "hash__room-image.png");
            Directory.CreateDirectory(Path.GetDirectoryName(exportedAbsolutePath) ?? tempRoot);
            File.WriteAllBytes(exportedAbsolutePath, Encoding.UTF8.GetBytes("export"));
            WriteAssetsManifest(tempRoot, configuredPath, exportedRelativePath);

            var issues = ExecuteRule(project, projectFilePath);

            var issue = Assert.Single(issues);
            Assert.Equal("IMG-001", issue.RuleId);
            Assert.Equal(ValidationSeverity.Warning, issue.Severity);
            Assert.Contains("ambiguous", issue.Description, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Source Image Management", issue.Hint, StringComparison.OrdinalIgnoreCase);
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
    public void Evaluate_LinkedRoomObjectWithoutLocalVariants_UsesBaseDefinitionImageVariants()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "ImageRule.sbe.json");
            var baseDoor = new GameObject
            {
                Name = "DoorBase",
                ObjectId = Guid.NewGuid(),
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "Closed",
                        FullImagePath = Path.Combine(tempRoot, "missing", "door-closed.png")
                    }
                ]
            };

            var roomDoor = new GameObject
            {
                Name = "RoomDoor",
                LinkedBaseObjectId = baseDoor.ObjectId,
                ImageVariants = []
            };

            var room = new Room
            {
                Name = "Room",
                GameObjects = [roomDoor]
            };

            var area = new Area { Name = "Area", Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };

            var project = new ProjectModel
            {
                Name = "ImageRuleProject",
                BaseObjects = [baseDoor],
                Planets = [planet]
            };

            var issues = ExecuteRule(
                project,
                projectFilePath,
                rootScope: roomDoor,
                executionKind: ValidationExecutionKind.ScopedNodeOnly,
                includeDescendants: false);

            var issue = Assert.Single(issues);
            Assert.Equal("IMG-001", issue.RuleId);
            Assert.Equal(ValidationSeverity.Error, issue.Severity);
            Assert.Contains("object image variant 'Closed' (full)", issue.Description, StringComparison.OrdinalIgnoreCase);
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
    public void Evaluate_RoomTemplateImage_AfterCleanExport_DoesNotWarnMissingFallback()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sourcePath = Path.Combine(tempRoot, "source", "template-room.png");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath) ?? tempRoot);
            File.WriteAllBytes(sourcePath, Encoding.UTF8.GetBytes("source"));

            var templateRoom = new Room
            {
                Name = "Template Room",
                Images =
                [
                    new RoomImageEntry
                    {
                        Slot = RoomImageSlot.Default,
                        Image = new RoomImageVariant
                        {
                            FullImagePath = sourcePath
                        }
                    }
                ]
            };

            var project = new ProjectModel
            {
                Name = "ImageRuleProject",
                RoomTemplates = [templateRoom]
            };

            var projectFilePath = Path.Combine(tempRoot, "ImageRule.sbe.json");
            var exportService = new JsonExportService();
            exportService.ExportCleanProjectV1(projectFilePath, project);

            var issues = ExecuteRule(project, projectFilePath);

            Assert.DoesNotContain(issues, issue =>
                string.Equals(issue.RuleId, "IMG-001", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static List<ValidationIssue> ExecuteRule(
        ProjectModel project,
        string projectFilePath,
        ScopeNodeBase? rootScope = null,
        ValidationExecutionKind executionKind = ValidationExecutionKind.WholeProject,
        bool includeDescendants = true)
    {
        ScopeHierarchy.AttachParents(project);

        var registry = new ValidationRuleRegistry();
        registry.Register(new ImagePathAvailabilityRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(
            project,
            executionKind,
            RootScope: rootScope,
            IncludeDescendants: includeDescendants,
            CompletionMode: ValidationCompletionMode.FullReport,
            ProjectFilePath: projectFilePath));

        return result.Issues.ToList();
    }

    private static ProjectModel BuildProjectWithRoomImage(string fullImagePath)
    {
        var room = new Room
        {
            Name = "Room",
            Images =
            [
                new RoomImageEntry
                {
                    Slot = RoomImageSlot.Default,
                    Image = new RoomImageVariant
                    {
                        FullImagePath = fullImagePath
                    }
                }
            ]
        };

        var area = new Area { Name = "Area", Rooms = [room] };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };

        return new ProjectModel
        {
            Name = "ImageRuleProject",
            Planets = [planet]
        };
    }

    private static ProjectModel BuildProjectWithObjectTemplateImage(string fullImagePath)
    {
        var template = new GameObject
        {
            Name = "BasicDoor",
            ImageVariants =
            [
                new ObjectImageVariant
                {
                    VariantName = "Closed",
                    FullImagePath = fullImagePath
                }
            ]
        };

        return new ProjectModel
        {
            Name = "ImageRuleProject",
            ObjectTemplates = [template]
        };
    }

    private static void WriteAssetsManifest(string root, string sourcePath, string exportedPath)
    {
        var manifestPath = Path.Combine(root, "GameRuntimeJson", "assets", "assets-manifest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath) ?? root);

        var manifest = new
        {
            schemaVersion = "1.0",
            images = new[]
            {
                new
                {
                    hash = "hash",
                    exportedPath,
                    sourcePaths = new[] { sourcePath },
                    references = Array.Empty<string>()
                }
            },
            unresolvedSources = Array.Empty<string>()
        };

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
    }
}
