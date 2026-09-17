using System.IO;

namespace StoryboardDesigner.App.Services;

internal static class AssetSourcePathResolver
{
    public const string AssetSourceRootEnvironmentVariable = "STORYBOARD_ASSET_SOURCE_ROOT";
    public const string AssetRootPrefix = "ASSETROOT:/";

    public static bool TryResolveConfiguredPath(string? configuredPath, string? projectRootFolder, out string absolutePath)
    {
        absolutePath = string.Empty;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return false;
        }

        var trimmed = configuredPath.Trim();

        if (TryResolveAssetRootPath(trimmed, out var assetRootResolvedPath))
        {
            absolutePath = assetRootResolvedPath;
            return true;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            absolutePath = uri.LocalPath;
            return true;
        }

        if (Path.IsPathRooted(trimmed))
        {
            absolutePath = trimmed;
            return true;
        }

        absolutePath = !string.IsNullOrWhiteSpace(projectRootFolder)
            ? Path.GetFullPath(Path.Combine(projectRootFolder, trimmed))
            : Path.GetFullPath(trimmed);
        return true;
    }

    public static string NormalizeForPersistence(string? configuredPath, string? projectRootFolder)
    {
        var trimmed = configuredPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (!TryResolveConfiguredPath(trimmed, projectRootFolder, out var absolutePath))
        {
            return trimmed;
        }

        return TryBuildAssetRootToken(absolutePath, out var token)
            ? token
            : trimmed;
    }

    public static bool TryResolveAssetRootPath(string configuredPath, out string absolutePath)
    {
        absolutePath = string.Empty;
        if (string.IsNullOrWhiteSpace(configuredPath)
            || !configuredPath.StartsWith(AssetRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var assetRoot = Environment.GetEnvironmentVariable(AssetSourceRootEnvironmentVariable)?.Trim();
        if (string.IsNullOrWhiteSpace(assetRoot))
        {
            return false;
        }

        var relativeSegment = configuredPath[AssetRootPrefix.Length..]
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        absolutePath = string.IsNullOrWhiteSpace(relativeSegment)
            ? Path.GetFullPath(assetRoot)
            : Path.GetFullPath(Path.Combine(assetRoot, relativeSegment));
        return true;
    }

    private static bool TryBuildAssetRootToken(string absolutePath, out string token)
    {
        token = string.Empty;
        var assetRoot = Environment.GetEnvironmentVariable(AssetSourceRootEnvironmentVariable)?.Trim();
        if (string.IsNullOrWhiteSpace(assetRoot))
        {
            return false;
        }

        try
        {
            var normalizedRoot = Path.GetFullPath(assetRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedPath = Path.GetFullPath(absolutePath);

            var rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;
            var isRootPath = string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase);
            var isDescendant = normalizedPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
            if (!isRootPath && !isDescendant)
            {
                return false;
            }

            if (isRootPath)
            {
                token = AssetRootPrefix;
                return true;
            }

            var relative = Path.GetRelativePath(normalizedRoot, normalizedPath)
                .Replace('\\', '/');
            token = string.Concat(AssetRootPrefix, relative);
            return true;
        }
        catch
        {
            return false;
        }
    }
}