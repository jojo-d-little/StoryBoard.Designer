using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;
internal sealed class CleanExportImageAssetStager
{
    private const string ManifestSchemaVersion = "1.0";
    private const string CleanAssetsFolderName = "assets";
    private const string CleanImagesFolderName = "images";
    private const string CleanSharedImagesFolderName = "_shared";

    private readonly string _sharedImagesFolderPath;
    private readonly string _imagesRootFolderPath;
    private readonly string? _projectRootFolder;
    private readonly Dictionary<string, StagedImageAsset> _stagedAssetsByHash = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StagedImageAsset> _stagedAssetsBySourcePath = new(StringComparer.OrdinalIgnoreCase);

    public CleanExportImageAssetStager(string sharedImagesFolderPath, string? projectRootFolder)
    {
        _sharedImagesFolderPath = sharedImagesFolderPath;
        _projectRootFolder = projectRootFolder;
        _imagesRootFolderPath = Directory.GetParent(sharedImagesFolderPath)?.FullName
            ?? throw new InvalidOperationException("Shared images folder path must have a parent images folder.");
    }

    public string ResolveAndStage(string? sourcePath, string reference)
    {
        return ResolveAndStage(sourcePath, reference, CleanSharedImagesFolderName);
    }

    public string ResolveAndStage(string? sourcePath, string reference, string relativeImageSubfolder)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return string.Empty;
        }

        var normalizedSourcePath = sourcePath.Trim();
        var normalizedSubfolder = NormalizeRelativeImageSubfolder(relativeImageSubfolder);
        var sourceKey = BuildSourceKey(normalizedSourcePath, normalizedSubfolder);
        if (_stagedAssetsBySourcePath.TryGetValue(sourceKey, out var existingBySource))
        {
            existingBySource.RegisterReference(normalizedSourcePath, reference);
            return existingBySource.RelativePath;
        }

        var physicalSourcePath = ResolvePhysicalSourcePath(normalizedSourcePath);
        if (string.IsNullOrWhiteSpace(physicalSourcePath) || !File.Exists(physicalSourcePath))
        {
            var unresolved = StagedImageAsset.CreateUnresolved(normalizedSourcePath);
            unresolved.RegisterReference(normalizedSourcePath, reference);
            _stagedAssetsBySourcePath[sourceKey] = unresolved;
            return string.Empty;
        }

        var imageBytes = File.ReadAllBytes(physicalSourcePath);
        var hash = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();
        var hashKey = BuildHashKey(hash, normalizedSubfolder);
        if (_stagedAssetsByHash.TryGetValue(hashKey, out var existingByHash))
        {
            existingByHash.RegisterReference(normalizedSourcePath, reference);
            _stagedAssetsBySourcePath[sourceKey] = existingByHash;
            return existingByHash.RelativePath;
        }

        var fileStem = SanitizeImageStem(Path.GetFileNameWithoutExtension(physicalSourcePath));
        var extension = DetectCanonicalImageExtension(physicalSourcePath, imageBytes);
        var fileName = $"{hash}__{fileStem}{extension}";
        var targetFolderPath = ResolveTargetFolderPath(normalizedSubfolder);
        Directory.CreateDirectory(targetFolderPath);
        var targetPath = Path.Combine(targetFolderPath, fileName);

        File.WriteAllBytes(targetPath, imageBytes);

        var relativePath = NormalizeToRelativeAssetPath(Path.Combine(CleanAssetsFolderName, CleanImagesFolderName, normalizedSubfolder, fileName));
        var stagedAsset = StagedImageAsset.CreateResolved(hash, relativePath, imageBytes.Length);
        stagedAsset.RegisterReference(normalizedSourcePath, reference);

        _stagedAssetsByHash[hashKey] = stagedAsset;
        _stagedAssetsBySourcePath[sourceKey] = stagedAsset;

        return relativePath;
    }

    public void WriteManifest(string manifestPath, JsonSerializerOptions options)
    {
        var manifestDirectory = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrWhiteSpace(manifestDirectory))
        {
            Directory.CreateDirectory(manifestDirectory);
        }

        var manifest = new CleanAssetManifestDto
        {
            SchemaVersion = ManifestSchemaVersion,
            Images = _stagedAssetsByHash.Values
                .OrderBy(asset => asset.Hash, StringComparer.Ordinal)
                .Select(asset => new CleanAssetManifestImageDto
                {
                    Hash = asset.Hash,
                    Sha256 = asset.Hash,
                    SizeBytes = asset.SizeBytes,
                    ImagePathSemantics = "runtimeExportRelative",
                    ExportedPath = asset.RelativePath,
                    SourcePaths = asset.SourcePaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList(),
                    References = asset.References.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList()
                })
                .ToList(),
            UnresolvedSources = _stagedAssetsBySourcePath
                .Where(static pair => pair.Value.IsResolved == false)
                .SelectMany(static pair => pair.Value.SourcePaths)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, options));
    }

    private string? ResolvePhysicalSourcePath(string configuredSourcePath)
    {
        return AssetSourcePathResolver.TryResolveConfiguredPath(configuredSourcePath, _projectRootFolder, out var absolutePath)
            ? absolutePath
            : null;
    }

    private static string NormalizeToRelativeAssetPath(string filePath)
    {
        return filePath.Replace('\\', '/');
    }

    private static string NormalizeRelativeImageSubfolder(string value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return CleanSharedImagesFolderName;
        }

        normalized = normalized
            .Replace('\\', '/')
            .Trim('/');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return CleanSharedImagesFolderName;
        }

        var parts = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizePathSegment)
            .Where(static part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        return parts.Count == 0
            ? CleanSharedImagesFolderName
            : string.Join("/", parts);
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
        return new string(chars).Trim();
    }

    private static string BuildSourceKey(string sourcePath, string normalizedSubfolder)
    {
        return string.Concat(normalizedSubfolder, "|", sourcePath);
    }

    private static string BuildHashKey(string hash, string normalizedSubfolder)
    {
        return string.Concat(normalizedSubfolder, "|", hash);
    }

    private string ResolveTargetFolderPath(string normalizedSubfolder)
    {
        if (string.Equals(normalizedSubfolder, CleanSharedImagesFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return _sharedImagesFolderPath;
        }

        var relativeSubfolderPath = normalizedSubfolder.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_imagesRootFolderPath, relativeSubfolderPath);
    }

    private static string SanitizeImageStem(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "image";
        }

        var sanitized = Sanitize(value.Trim());
        return string.IsNullOrWhiteSpace(sanitized) ? "image" : sanitized;
    }

    private static string Sanitize(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    private static string DetectCanonicalImageExtension(string sourcePath, byte[] bytes)
    {
        if (bytes.Length >= 8
            && bytes[0] == 0x89
            && bytes[1] == 0x50
            && bytes[2] == 0x4E
            && bytes[3] == 0x47
            && bytes[4] == 0x0D
            && bytes[5] == 0x0A
            && bytes[6] == 0x1A
            && bytes[7] == 0x0A)
        {
            return ".png";
        }

        if (bytes.Length >= 3
            && bytes[0] == 0xFF
            && bytes[1] == 0xD8
            && bytes[2] == 0xFF)
        {
            return ".jpg";
        }

        if (bytes.Length >= 6
            && bytes[0] == 0x47
            && bytes[1] == 0x49
            && bytes[2] == 0x46
            && bytes[3] == 0x38
            && (bytes[4] == 0x37 || bytes[4] == 0x39)
            && bytes[5] == 0x61)
        {
            return ".gif";
        }

        if (bytes.Length >= 2
            && bytes[0] == 0x42
            && bytes[1] == 0x4D)
        {
            return ".bmp";
        }

        if (bytes.Length >= 12
            && bytes[0] == 0x52
            && bytes[1] == 0x49
            && bytes[2] == 0x46
            && bytes[3] == 0x46
            && bytes[8] == 0x57
            && bytes[9] == 0x45
            && bytes[10] == 0x42
            && bytes[11] == 0x50)
        {
            return ".webp";
        }

        var extension = Path.GetExtension(sourcePath);
        return string.IsNullOrWhiteSpace(extension)
            ? ".bin"
            : extension.ToLowerInvariant();
    }
}

