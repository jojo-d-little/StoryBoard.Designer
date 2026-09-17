using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class AssetSourcePathResolverTests
{
    [Fact]
    public void NormalizeForPersistence_ReturnsAssetRootToken_WhenAbsolutePathIsUnderConfiguredRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var prior = Environment.GetEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable, tempRoot);
            var absolute = Path.Combine(tempRoot, "FormalImages", "Rooms", "room001.png");
            var normalized = AssetSourcePathResolver.NormalizeForPersistence(absolute, projectRootFolder: null);

            Assert.Equal("ASSETROOT:/FormalImages/Rooms/room001.png", normalized);
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
    public void NormalizeForPersistence_KeepsOriginalValue_WhenPathIsOutsideConfiguredRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        var otherRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Other", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(otherRoot);

        var prior = Environment.GetEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable, tempRoot);
            var outsidePath = Path.Combine(otherRoot, "PlaceHolderImages", "crate.png");
            var normalized = AssetSourcePathResolver.NormalizeForPersistence(outsidePath, projectRootFolder: null);

            Assert.Equal(outsidePath, normalized);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable, prior);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }

            if (Directory.Exists(otherRoot))
            {
                Directory.Delete(otherRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void NormalizeForPersistence_ResolvesRelativePathAgainstProjectRoot_ThenConvertsToAssetRootToken()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var prior = Environment.GetEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable, tempRoot);
            var projectRoot = Path.Combine(tempRoot, "Samples", "ChessDemo");
            Directory.CreateDirectory(projectRoot);

            var normalized = AssetSourcePathResolver.NormalizeForPersistence(
                Path.Combine("..", "..", "PlaceHolderImages", "WhiteChessKing.png"),
                projectRoot);

            Assert.Equal("ASSETROOT:/PlaceHolderImages/WhiteChessKing.png", normalized);
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
}
