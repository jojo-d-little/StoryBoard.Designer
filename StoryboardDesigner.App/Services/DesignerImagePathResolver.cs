using System.IO;
using System.Text.Json;

namespace StoryboardDesigner.App.Services;

public static class DesignerImagePathResolver
{
    private const string SourceImageRootFolderName = "project-source-images";
    private const string CleanExportRootFolderName = "GameRuntimeJson";
    private const string AssetsManifestRelativePath = "assets/assets-manifest.json";

    public static string? ResolveForPreview(string? configuredPath, string? projectFilePath, string? preferredSourceBucket = null)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        var sourceCandidate = ResolveSourcePath(configuredPath, projectFilePath);
        if (!string.IsNullOrWhiteSpace(sourceCandidate))
        {
            return sourceCandidate;
        }

        var projectRootFolder = GetProjectRootFolder(projectFilePath);

        var sourceImageLibraryCandidate = ResolveFromProjectSourceImageLibrary(configuredPath, projectRootFolder, preferredSourceBucket);
        if (!string.IsNullOrWhiteSpace(sourceImageLibraryCandidate))
        {
            return sourceImageLibraryCandidate;
        }

        var exportedAssetCandidate = ResolveExportAssetPath(configuredPath, projectFilePath);
        if (!string.IsNullOrWhiteSpace(exportedAssetCandidate))
        {
            return exportedAssetCandidate;
        }

        return null;
    }

    public static string? ResolveSourcePath(string? configuredPath, string? projectFilePath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        var projectRootFolder = GetProjectRootFolder(projectFilePath);
        var sourceCandidate = ToAbsolutePath(configuredPath, projectRootFolder);
        return !string.IsNullOrWhiteSpace(sourceCandidate) && File.Exists(sourceCandidate)
            ? sourceCandidate
            : null;
    }

    public static string? ResolveExportAssetPath(string? configuredPath, string? projectFilePath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        var projectRootFolder = GetProjectRootFolder(projectFilePath);
        return ResolveFromExportAssets(configuredPath, projectRootFolder);
    }

    public static bool HasAmbiguousSourceLibraryMatches(string? configuredPath, string? projectFilePath, string? preferredSourceBucket = null)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return false;
        }

        var projectRootFolder = GetProjectRootFolder(projectFilePath);
        if (string.IsNullOrWhiteSpace(projectRootFolder))
        {
            return false;
        }

        var sourceRoot = Path.Combine(projectRootFolder, SourceImageRootFolderName);
        if (!Directory.Exists(sourceRoot))
        {
            return false;
        }

        var fileName = Path.GetFileName(configuredPath.Trim());
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(preferredSourceBucket))
        {
            var bucketPath = Path.Combine(sourceRoot, preferredSourceBucket);
            if (Directory.Exists(bucketPath))
            {
                var bucketCount = CountMatchesByFileName(bucketPath, fileName, 2);
                if (bucketCount > 1)
                {
                    return true;
                }
            }
        }

        var globalCount = CountMatchesByFileName(sourceRoot, fileName, 2);
        return globalCount > 1;
    }

    private static string? ResolveFromProjectSourceImageLibrary(string configuredPath, string? projectRootFolder, string? preferredSourceBucket)
    {
        if (string.IsNullOrWhiteSpace(projectRootFolder))
        {
            return null;
        }

        var sourceRoot = Path.Combine(projectRootFolder, SourceImageRootFolderName);
        if (!Directory.Exists(sourceRoot))
        {
            return null;
        }

        var fileName = Path.GetFileName(configuredPath.Trim());
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(preferredSourceBucket))
        {
            var bucketPath = Path.Combine(sourceRoot, preferredSourceBucket);
            var bucketMatch = ResolveUniqueMatchByFileName(bucketPath, fileName);
            if (!string.IsNullOrWhiteSpace(bucketMatch))
            {
                return bucketMatch;
            }
        }

        return ResolveUniqueMatchByFileName(sourceRoot, fileName);
    }

    private static string? ResolveFromExportAssets(string configuredPath, string? projectRootFolder)
    {
        if (string.IsNullOrWhiteSpace(projectRootFolder))
        {
            return null;
        }

        var cleanExportRoot = Path.Combine(projectRootFolder, CleanExportRootFolderName);
        if (!Directory.Exists(cleanExportRoot))
        {
            return null;
        }

        var trimmedPath = configuredPath.Trim();
        if (trimmedPath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)
            || trimmedPath.StartsWith("assets\\", StringComparison.OrdinalIgnoreCase))
        {
            var directCandidate = Path.Combine(cleanExportRoot, trimmedPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
            if (File.Exists(directCandidate))
            {
                return directCandidate;
            }
        }

        var manifestPath = Path.Combine(cleanExportRoot, AssetsManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var document = JsonDocument.Parse(stream);

            if (!document.RootElement.TryGetProperty("images", out var images)
                || images.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var configuredAbsolutePath = ToAbsolutePath(configuredPath, projectRootFolder) ?? configuredPath.Trim();

            foreach (var imageEntry in images.EnumerateArray())
            {
                if (!imageEntry.TryGetProperty("sourcePaths", out var sourcePaths)
                    || sourcePaths.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var sourcePathMatches = sourcePaths.EnumerateArray()
                    .Select(static source => source.GetString() ?? string.Empty)
                    .Any(source => string.Equals(source, configuredAbsolutePath, StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(source, configuredPath.Trim(), StringComparison.OrdinalIgnoreCase));

                if (!sourcePathMatches)
                {
                    continue;
                }

                if (!imageEntry.TryGetProperty("exportedPath", out var exportedPathProperty))
                {
                    continue;
                }

                var exportedPath = exportedPathProperty.GetString();
                if (string.IsNullOrWhiteSpace(exportedPath))
                {
                    continue;
                }

                var absoluteExportedPath = Path.Combine(cleanExportRoot, exportedPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
                if (File.Exists(absoluteExportedPath))
                {
                    return absoluteExportedPath;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? ResolveUniqueMatchByFileName(string searchRoot, string fileName)
    {
        if (!Directory.Exists(searchRoot))
        {
            return null;
        }

        var matches = Directory.EnumerateFiles(searchRoot, fileName, SearchOption.AllDirectories)
            .Take(2)
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    private static int CountMatchesByFileName(string searchRoot, string fileName, int take)
    {
        return Directory.EnumerateFiles(searchRoot, fileName, SearchOption.AllDirectories)
            .Take(take)
            .Count();
    }

    private static string? ToAbsolutePath(string path, string? projectRootFolder)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return AssetSourcePathResolver.TryResolveConfiguredPath(path, projectRootFolder, out var absolutePath)
            ? absolutePath
            : null;
    }

    private static string? GetProjectRootFolder(string? projectFilePath)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath))
        {
            return null;
        }

        return Path.GetDirectoryName(projectFilePath);
    }
}