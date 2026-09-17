using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class SourceImageManagementServiceTests
{
    [Fact]
    public void Consolidate_WithRepoint_WritesDiagnosticsAndUpdatesSourcePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var sourceRoot = Path.Combine(root, "external-source");
            Directory.CreateDirectory(sourceRoot);
            var sourceImagePath = Path.Combine(sourceRoot, "crate.png");
            WriteTinyPng(sourceImagePath);

            var gameObject = new GameObject
            {
                Name = "Crate",
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        FullImagePath = sourceImagePath,
                        IsDefault = true
                    }
                ]
            };

            var project = new ProjectModel
            {
                Name = "ImageConsolidate",
                GlobalObjectScopeName = "Global",
                GameObjects = new List<GameObject> { gameObject }
            };

            var projectFilePath = Path.Combine(root, "ImageConsolidate.sbe.json");
            File.WriteAllText(projectFilePath, "{}");

            var preview = SourceImageManagementService.BuildPreview(
                project,
                projectFilePath,
                SourceImageManagementMode.ConsolidateSourceImages,
                repointSourcePaths: true);

            Assert.True(preview.ActionableCount > 0);

            var previewText = SourceImageManagementService.BuildPreviewText(
                preview,
                projectFilePath,
                SourceImageManagementMode.ConsolidateSourceImages);
            Assert.Contains("Actionable=", previewText, StringComparison.Ordinal);

            var applyResult = SourceImageManagementService.Apply(
                preview,
                projectFilePath,
                SourceImageManagementMode.ConsolidateSourceImages);

            Assert.True(applyResult.ChangesApplied > 0);
            Assert.NotEqual(sourceImagePath, gameObject.ResolveDefaultImagePath());
            Assert.Contains("project-source-images", gameObject.ResolveDefaultImagePath(), StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(gameObject.ResolveDefaultImagePath()));

            var diagnosticsRoot = Path.Combine(root, "source-image-management");
            Assert.True(Directory.Exists(diagnosticsRoot));
            Assert.NotEmpty(Directory.GetFiles(diagnosticsRoot, "preview-*.txt"));
            Assert.NotEmpty(Directory.GetFiles(diagnosticsRoot, "apply-*.txt"));

            var activityLogPath = Path.Combine(diagnosticsRoot, "activity.log");
            Assert.True(File.Exists(activityLogPath));
            var activity = File.ReadAllText(activityLogPath);
            Assert.Contains("type=preview", activity, StringComparison.Ordinal);
            Assert.Contains("type=apply", activity, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void RepairMissingSources_WithAmbiguousLocalMatches_DoesNotAutoRepoint()
    {
        var root = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var projectSourceRoot = Path.Combine(root, "project-source-images", "objects");
            Directory.CreateDirectory(Path.Combine(projectSourceRoot, "obj-a"));
            Directory.CreateDirectory(Path.Combine(projectSourceRoot, "obj-b"));

            var fileName = "shared.png";
            WriteTinyPng(Path.Combine(projectSourceRoot, "obj-a", fileName));
            WriteTinyPng(Path.Combine(projectSourceRoot, "obj-b", fileName));

            var unresolved = Path.Combine(root, "missing", fileName);
            var gameObject = new GameObject
            {
                Name = "Ambiguous",
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        FullImagePath = unresolved,
                        IsDefault = true
                    }
                ]
            };

            var project = new ProjectModel
            {
                Name = "ImageRepair",
                GlobalObjectScopeName = "Global",
                GameObjects = new List<GameObject> { gameObject }
            };

            var projectFilePath = Path.Combine(root, "ImageRepair.sbe.json");
            File.WriteAllText(projectFilePath, "{}");

            var preview = SourceImageManagementService.BuildPreview(
                project,
                projectFilePath,
                SourceImageManagementMode.RepairMissingSources,
                repointSourcePaths: true);

            Assert.Equal(0, preview.ActionableCount);

            var applyResult = SourceImageManagementService.Apply(
                preview,
                projectFilePath,
                SourceImageManagementMode.RepairMissingSources);

            Assert.Equal(0, applyResult.ChangesApplied);
            Assert.Equal(unresolved, gameObject.ResolveDefaultImagePath());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void WriteTinyPng(string path)
    {
        const string tinyPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO2F3iUAAAAASUVORK5CYII=";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Convert.FromBase64String(tinyPngBase64));
    }
}
