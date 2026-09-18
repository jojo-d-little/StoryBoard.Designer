using System.IO;
using Storyboard.Foundation.Paths;

namespace StoryboardDesigner.App.Services;

internal static class AssetSourcePathResolver
{
    public const string AssetSourceRootEnvironmentVariable = "STORYBOARD_ASSET_SOURCE_ROOT";
    public const string AssetSourceRootEnvironmentVariablePrefix = "STORYBOARD_ASSET_SOURCE_ROOT_";
    public const string AssetRootPrefix = "ASSETROOT:/";

    public static IReadOnlyList<AssetRoot> GetConfiguredAssetRoots()
    {
        var roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is not string variableName || entry.Value is not string rootPath)
            {
                continue;
            }

            if (!IsAssetRootVariable(variableName)
                || string.IsNullOrWhiteSpace(rootPath))
            {
                continue;
            }

            try
            {
                roots[variableName] = Path.GetFullPath(rootPath.Trim());
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
            {
                // An invalid optional root must not prevent the remaining roots from being used.
            }
        }

        return roots
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new AssetRoot(pair.Key, pair.Value))
            .ToArray();
    }

    public static bool TryResolveConfiguredPath(string? configuredPath, string? projectRootFolder, out string absolutePath)
    {
        absolutePath = string.Empty;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return false;
        }

        var trimmed = configuredPath.Trim();

        var result = ResolveConfiguredPath(trimmed, projectRootFolder);

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.PhysicalPath))
        {
            return false;
        }

        absolutePath = result.PhysicalPath;
        return true;
    }

    public static PathExpressionResolutionResult ResolveConfiguredPath(string? configuredPath, string? projectRootFolder)
    {
        var trimmed = configuredPath?.Trim() ?? string.Empty;
        var expression = ExpandLegacyAssetRootAlias(trimmed);
        return PathExpressionResolver.Resolve(expression, new PathExpressionOptions
        {
            AllowAbsolutePaths = true,
            AllowRelativePaths = true,
            AllowFileUris = true,
            BaseDirectory = projectRootFolder
        });
    }

    public static string NormalizeForPersistence(string? configuredPath, string? projectRootFolder)
        => NormalizeForPersistence(configuredPath, projectRootFolder, preferredRootVariableName: null).PersistedPath;

    public static AssetSourcePathNormalizationResult NormalizeForPersistence(
        string? configuredPath,
        string? projectRootFolder,
        string? preferredRootVariableName)
    {
        var trimmed = configuredPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return new AssetSourcePathNormalizationResult(trimmed, false, false, Array.Empty<string>());
        }

        if (!TryResolveConfiguredPath(trimmed, projectRootFolder, out var absolutePath))
        {
            return new AssetSourcePathNormalizationResult(trimmed, false, false, Array.Empty<string>());
        }

        var matches = AssetRootMatcher.FindContainingRoots(absolutePath, GetConfiguredAssetRoots());
        if (matches.Count == 0)
        {
            return new AssetSourcePathNormalizationResult(trimmed, false, false, Array.Empty<string>());
        }

        var deepestDepth = matches[0].Depth;
        var deepestMatches = matches
            .Where(match => match.Depth == deepestDepth)
            .ToArray();

        var selectedMatch = deepestMatches.Length == 1
            ? deepestMatches[0]
            : deepestMatches.FirstOrDefault(match => string.Equals(
                match.Root.VariableName,
                preferredRootVariableName,
                StringComparison.OrdinalIgnoreCase));

        if (selectedMatch is null)
        {
            return new AssetSourcePathNormalizationResult(
                trimmed,
                false,
                true,
                deepestMatches.Select(match => match.Root.VariableName).ToArray());
        }

        var relativePath = selectedMatch.RelativePath.Replace('\\', '/');
        var token = $"%{selectedMatch.Root.VariableName}%/{relativePath}";
        return new AssetSourcePathNormalizationResult(
            token,
            true,
            false,
            deepestMatches.Select(match => match.Root.VariableName).ToArray());
    }

    public static bool TryResolveAssetRootPath(string configuredPath, out string absolutePath)
    {
        return TryResolveConfiguredPath(configuredPath, projectRootFolder: null, out absolutePath)
            && configuredPath.StartsWith(AssetRootPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAssetRootVariable(string variableName)
    {
        if (string.Equals(variableName, AssetSourceRootEnvironmentVariable, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!variableName.StartsWith(AssetSourceRootEnvironmentVariablePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = variableName[AssetSourceRootEnvironmentVariablePrefix.Length..];
        return suffix.Length > 0
            && suffix[0] is >= 'A' and <= 'Z'
            && suffix.Skip(1).All(character => (character is >= 'A' and <= 'Z') || (character is >= '0' and <= '9') || character == '_');
    }

    private static string ExpandLegacyAssetRootAlias(string configuredPath)
    {
        if (!configuredPath.StartsWith(AssetRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return configuredPath;
        }

        var relativeSegment = configuredPath[AssetRootPrefix.Length..].TrimStart('/', '\\');
        return $"%{AssetSourceRootEnvironmentVariable}%/{relativeSegment}";
    }
}

internal sealed record AssetSourcePathNormalizationResult(
    string PersistedPath,
    bool IsPortable,
    bool IsAmbiguous,
    IReadOnlyList<string> CandidateRootVariableNames);
