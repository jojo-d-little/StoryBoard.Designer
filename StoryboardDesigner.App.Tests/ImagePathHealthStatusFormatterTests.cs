using StoryboardDesigner.App.Services;
using System.Text.Json;

namespace StoryboardDesigner.App.Tests;

public sealed class ImagePathHealthStatusFormatterTests
{
    [Fact]
    public void BuildStatusLabel_NotConfigured_ReturnsNotConfigured()
    {
        var label = ImagePathHealthStatusFormatter.BuildStatusLabel(string.Empty, projectFilePath: null, sourceBucket: "objects");

        Assert.Equal("Not configured.", label);
    }

    [Fact]
    public void BuildStatusLabel_SourceExists_ReturnsSourceAvailable()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sourcePath = Path.Combine(tempRoot, "source", "icon.png");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3 });

            var label = ImagePathHealthStatusFormatter.BuildStatusLabel(sourcePath, Path.Combine(tempRoot, "Project.sbe.json"), "objects");

            Assert.Equal("Source available.", label);
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
    public void BuildStatusLabel_AmbiguousLocalAndExportExists_ReturnsExplicitAmbiguityWithExportFallback()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "Project.sbe.json");
            var configuredPath = Path.Combine(tempRoot, "missing", "icon.png");

            var firstLocal = Path.Combine(tempRoot, "project-source-images", "objects", "a", "icon.png");
            var secondLocal = Path.Combine(tempRoot, "project-source-images", "objects", "b", "icon.png");
            Directory.CreateDirectory(Path.GetDirectoryName(firstLocal)!);
            Directory.CreateDirectory(Path.GetDirectoryName(secondLocal)!);
            File.WriteAllBytes(firstLocal, new byte[] { 1 });
            File.WriteAllBytes(secondLocal, new byte[] { 2 });

            var exportedPath = Path.Combine(tempRoot, "GameRuntimeJson", "assets", "images", "_shared", "hash__icon.png");
            Directory.CreateDirectory(Path.GetDirectoryName(exportedPath)!);
            File.WriteAllBytes(exportedPath, new byte[] { 3 });

            var manifestPath = Path.Combine(tempRoot, "GameRuntimeJson", "assets", "assets-manifest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            var manifest = new
            {
                schemaVersion = "1.0",
                images = new[]
                {
                    new
                    {
                        hash = "hash",
                        exportedPath = "assets/images/_shared/hash__icon.png",
                        sourcePaths = new[] { configuredPath },
                        references = Array.Empty<string>()
                    }
                },
                unresolvedSources = Array.Empty<string>()
            };
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));

            var label = ImagePathHealthStatusFormatter.BuildStatusLabel(configuredPath, projectFilePath, "objects");

            Assert.Contains("Fallback: exported asset used", label, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Multiple project-source-images matches", label, StringComparison.OrdinalIgnoreCase);
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
