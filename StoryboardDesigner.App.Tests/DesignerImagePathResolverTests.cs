using System.Text;
using System.Text.Json;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public class DesignerImagePathResolverTests
{
    [Fact]
    public void ResolveSourcePath_ResolvesAssetRootToken_WhenEnvironmentVariableIsConfigured()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var prior = Environment.GetEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable, tempRoot);
            var projectFilePath = Path.Combine(tempRoot, "Resolver.sbe.json");
            var sourcePath = Path.Combine(tempRoot, "FormalImages", "Rooms", "token-source.png");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath) ?? tempRoot);
            File.WriteAllBytes(sourcePath, Encoding.UTF8.GetBytes("source"));

            var configuredPath = "ASSETROOT:/FormalImages/Rooms/token-source.png";
            var resolved = DesignerImagePathResolver.ResolveSourcePath(configuredPath, projectFilePath);

            Assert.Equal(sourcePath, resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable, prior);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolveForPreview_ReturnsSourcePath_WhenSourceFileExists()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "Resolver.sbe.json");
            var sourcePath = Path.Combine(tempRoot, "source.png");
            File.WriteAllBytes(sourcePath, Encoding.UTF8.GetBytes("source"));

            var resolved = DesignerImagePathResolver.ResolveForPreview(sourcePath, projectFilePath, preferredSourceBucket: "objects");

            Assert.Equal(sourcePath, resolved);
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
    public void ResolveForPreview_UsesProjectSourceImageLibrary_WhenSourceMissing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "Resolver.sbe.json");
            var configuredPath = Path.Combine(tempRoot, "missing", "photo.png");
            var localLibraryFile = Path.Combine(tempRoot, "project-source-images", "objects", "item--1", "photo.png");

            Directory.CreateDirectory(Path.GetDirectoryName(localLibraryFile) ?? tempRoot);
            File.WriteAllBytes(localLibraryFile, Encoding.UTF8.GetBytes("local"));

            var resolved = DesignerImagePathResolver.ResolveForPreview(configuredPath, projectFilePath, preferredSourceBucket: "objects");

            Assert.Equal(localLibraryFile, resolved);
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
    public void ResolveForPreview_UsesExportAssetFromManifest_WhenSourceAndLibraryMissing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "Resolver.sbe.json");
            var configuredPath = Path.Combine(tempRoot, "missing", "photo.png");

            var exportedRelativePath = "assets/images/_shared/hash__photo.png";
            var exportedAbsolutePath = Path.Combine(tempRoot, "GameRuntimeJson", "assets", "images", "_shared", "hash__photo.png");
            Directory.CreateDirectory(Path.GetDirectoryName(exportedAbsolutePath) ?? tempRoot);
            File.WriteAllBytes(exportedAbsolutePath, Encoding.UTF8.GetBytes("export"));

            var manifestPath = Path.Combine(tempRoot, "GameRuntimeJson", "assets", "assets-manifest.json");
            var manifest = new
            {
                schemaVersion = "1.0",
                images = new[]
                {
                    new
                    {
                        hash = "hash",
                        exportedPath = exportedRelativePath,
                        sourcePaths = new[] { configuredPath },
                        references = Array.Empty<string>()
                    }
                },
                unresolvedSources = Array.Empty<string>()
            };

            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));

            var resolved = DesignerImagePathResolver.ResolveForPreview(configuredPath, projectFilePath, preferredSourceBucket: "objects");

            Assert.Equal(exportedAbsolutePath, resolved);
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
    public void ResolveForPreview_ReturnsNull_WhenSourceAndFallbacksAreUnavailable()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "Resolver.sbe.json");
            var configuredPath = Path.Combine(tempRoot, "missing", "photo.png");

            var resolved = DesignerImagePathResolver.ResolveForPreview(configuredPath, projectFilePath, preferredSourceBucket: "objects");

            Assert.Null(resolved);
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
    public void HasAmbiguousSourceLibraryMatches_ReturnsTrue_WhenPreferredBucketHasMultipleMatches()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "Resolver.sbe.json");
            var configuredPath = Path.Combine(tempRoot, "missing", "photo.png");
            var bucketRoot = Path.Combine(tempRoot, "project-source-images", "objects");
            var first = Path.Combine(bucketRoot, "item-a", "photo.png");
            var second = Path.Combine(bucketRoot, "item-b", "photo.png");

            Directory.CreateDirectory(Path.GetDirectoryName(first) ?? tempRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(second) ?? tempRoot);
            File.WriteAllBytes(first, Encoding.UTF8.GetBytes("a"));
            File.WriteAllBytes(second, Encoding.UTF8.GetBytes("b"));

            var ambiguous = DesignerImagePathResolver.HasAmbiguousSourceLibraryMatches(configuredPath, projectFilePath, preferredSourceBucket: "objects");

            Assert.True(ambiguous);
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
    public void HasAmbiguousSourceLibraryMatches_ReturnsFalse_WhenOnlySingleMatchExists()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "Resolver.sbe.json");
            var configuredPath = Path.Combine(tempRoot, "missing", "photo.png");
            var filePath = Path.Combine(tempRoot, "project-source-images", "objects", "item-a", "photo.png");

            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? tempRoot);
            File.WriteAllBytes(filePath, Encoding.UTF8.GetBytes("single"));

            var ambiguous = DesignerImagePathResolver.HasAmbiguousSourceLibraryMatches(configuredPath, projectFilePath, preferredSourceBucket: "objects");

            Assert.False(ambiguous);
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
