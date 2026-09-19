using StoryboardDesigner.App.Services;
using Storyboard.Foundation.Paths;

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

            Assert.Equal($"%{AssetSourcePathResolver.AssetSourceRootEnvironmentVariable}%/FormalImages/Rooms/room001.png", normalized);
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

            Assert.Equal($"%{AssetSourcePathResolver.AssetSourceRootEnvironmentVariable}%/PlaceHolderImages/WhiteChessKing.png", normalized);
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
    public void NormalizeForPersistence_UsesNamedRoot()
    {
        var root = CreateTempDirectory();
        var prior = SetEnvironmentVariable("STORYBOARD_ASSET_SOURCE_ROOT_VICTORIAN", root);
        try
        {
            var absolute = Path.Combine(root, "HotelRoom", "door.png");
            var normalized = AssetSourcePathResolver.NormalizeForPersistence(absolute, null);

            Assert.Equal("%STORYBOARD_ASSET_SOURCE_ROOT_VICTORIAN%/HotelRoom/door.png", normalized);
        }
        finally
        {
            RestoreEnvironmentVariable("STORYBOARD_ASSET_SOURCE_ROOT_VICTORIAN", prior);
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void NormalizeForPersistence_UsesDeepestMatchingRoot()
    {
        var broadRoot = CreateTempDirectory();
        var nestedRoot = Path.Combine(broadRoot, "Victorian");
        Directory.CreateDirectory(nestedRoot);
        var broadPrior = SetEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable, broadRoot);
        var nestedPrior = SetEnvironmentVariable("STORYBOARD_ASSET_SOURCE_ROOT_VICTORIAN", nestedRoot);
        try
        {
            var normalized = AssetSourcePathResolver.NormalizeForPersistence(
                Path.Combine(nestedRoot, "HotelRoom", "door.png"),
                null);

            Assert.Equal("%STORYBOARD_ASSET_SOURCE_ROOT_VICTORIAN%/HotelRoom/door.png", normalized);
        }
        finally
        {
            RestoreEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable, broadPrior);
            RestoreEnvironmentVariable("STORYBOARD_ASSET_SOURCE_ROOT_VICTORIAN", nestedPrior);
            DeleteDirectory(broadRoot);
        }
    }

    [Fact]
    public void NormalizeForPersistence_RefusesEqualDepthRootTie()
    {
        var firstRoot = CreateTempDirectory();
        var secondRoot = firstRoot;
        var firstPrior = SetEnvironmentVariable("STORYBOARD_ASSET_SOURCE_ROOT_SHARED", firstRoot);
        var secondPrior = SetEnvironmentVariable("STORYBOARD_ASSET_SOURCE_ROOT_VICTORIAN", secondRoot);
        try
        {
            var filePath = Path.Combine(firstRoot, "door.png");
            var normalized = AssetSourcePathResolver.NormalizeForPersistence(
                filePath,
                null,
                preferredRootVariableName: "STORYBOARD_ASSET_SOURCE_ROOT_VICTORIAN");

            Assert.False(normalized.IsAmbiguous);
            Assert.Equal("%STORYBOARD_ASSET_SOURCE_ROOT_VICTORIAN%/door.png", normalized.PersistedPath);

            var tie = AssetSourcePathResolver.NormalizeForPersistence(filePath, null, preferredRootVariableName: null);
            Assert.True(tie.IsAmbiguous);
            Assert.Equal(filePath, tie.PersistedPath);
        }
        finally
        {
            RestoreEnvironmentVariable("STORYBOARD_ASSET_SOURCE_ROOT_SHARED", firstPrior);
            RestoreEnvironmentVariable("STORYBOARD_ASSET_SOURCE_ROOT_VICTORIAN", secondPrior);
            DeleteDirectory(firstRoot);
        }
    }

    [Fact]
    public void ResolveConfiguredPath_ReadsCanonicalAssetRootToken()
    {
        var root = CreateTempDirectory();
        var prior = SetEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable, root);
        try
        {
            var result = AssetSourcePathResolver.ResolveConfiguredPath(
                "%STORYBOARD_ASSET_SOURCE_ROOT%/FormalImages/door.png",
                null);

            Assert.True(result.IsSuccess);
            Assert.Equal(Path.Combine(root, "FormalImages", "door.png"), result.PhysicalPath);
        }
        finally
        {
            RestoreEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable, prior);
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void ResolveConfiguredPath_DoesNotResolveLegacyAssetRootAlias()
    {
        var root = CreateTempDirectory();
        var prior = SetEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable, root);
        try
        {
            var result = AssetSourcePathResolver.ResolveConfiguredPath("ASSETROOT:/FormalImages/door.png", null);

            Assert.False(result.IsSuccess);
            Assert.Null(result.PhysicalPath);
        }
        finally
        {
            RestoreEnvironmentVariable(AssetSourcePathResolver.AssetSourceRootEnvironmentVariable, prior);
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void ResolveConfiguredPath_RejectsUnresolvedTokenWithoutRelativeFallback()
    {
        var result = AssetSourcePathResolver.ResolveConfiguredPath(
            "%STORYBOARD_ASSET_SOURCE_ROOT_DOES_NOT_EXIST%/door.png",
            null);

        Assert.Equal(PathExpressionDiagnosticCode.UnresolvedVariable, result.DiagnosticCode);
        Assert.Null(result.PhysicalPath);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string? SetEnvironmentVariable(string name, string value)
    {
        var prior = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
        return prior;
    }

    private static void RestoreEnvironmentVariable(string name, string? value) => Environment.SetEnvironmentVariable(name, value);

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
