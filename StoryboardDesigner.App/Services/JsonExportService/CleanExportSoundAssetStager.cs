using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace StoryboardDesigner.App.Services;

internal sealed class CleanExportSoundAssetStager
{
    private const string CleanAssetsFolderName = "assets";
    private const string CleanSoundsFolderName = "sounds";
    private const string CleanSharedSoundsFolderName = "_shared";

    private readonly string _sharedSoundsFolderPath;
    private readonly string? _projectRootFolder;
    private readonly Dictionary<string, StagedSoundAsset> _stagedAssetsByHash = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StagedSoundAsset> _stagedAssetsBySourcePath = new(StringComparer.OrdinalIgnoreCase);

    public CleanExportSoundAssetStager(string sharedSoundsFolderPath, string? projectRootFolder)
    {
        _sharedSoundsFolderPath = sharedSoundsFolderPath;
        _projectRootFolder = projectRootFolder;
    }

    public string ResolveAndStage(string? sourcePath, string reference)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return string.Empty;
        }

        var normalizedSourcePath = sourcePath.Trim();
        if (_stagedAssetsBySourcePath.TryGetValue(normalizedSourcePath, out var existingBySource))
        {
            existingBySource.RegisterReference(normalizedSourcePath, reference);
            return existingBySource.RelativePath;
        }

        var physicalSourcePath = ResolvePhysicalSourcePath(normalizedSourcePath);
        if (string.IsNullOrWhiteSpace(physicalSourcePath) || !File.Exists(physicalSourcePath))
        {
            var unresolved = StagedSoundAsset.CreateUnresolved(normalizedSourcePath);
            unresolved.RegisterReference(normalizedSourcePath, reference);
            _stagedAssetsBySourcePath[normalizedSourcePath] = unresolved;
            return string.Empty;
        }

        var soundBytes = File.ReadAllBytes(physicalSourcePath);
        var hash = Convert.ToHexString(SHA256.HashData(soundBytes)).ToLowerInvariant();
        if (_stagedAssetsByHash.TryGetValue(hash, out var existingByHash))
        {
            existingByHash.RegisterReference(normalizedSourcePath, reference);
            _stagedAssetsBySourcePath[normalizedSourcePath] = existingByHash;
            return existingByHash.RelativePath;
        }

        var fileStem = SanitizeFileStem(Path.GetFileNameWithoutExtension(physicalSourcePath), "sound");
        var extension = Path.GetExtension(physicalSourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        var fileName = $"{hash}__{fileStem}{extension.ToLowerInvariant()}";
        var targetPath = Path.Combine(_sharedSoundsFolderPath, fileName);
        File.WriteAllBytes(targetPath, soundBytes);

        var relativePath = NormalizeToRelativeAssetPath(Path.Combine(CleanAssetsFolderName, CleanSoundsFolderName, CleanSharedSoundsFolderName, fileName));
        var stagedAsset = StagedSoundAsset.CreateResolved(hash, relativePath, soundBytes.Length);
        stagedAsset.RegisterReference(normalizedSourcePath, reference);

        _stagedAssetsByHash[hash] = stagedAsset;
        _stagedAssetsBySourcePath[normalizedSourcePath] = stagedAsset;

        return relativePath;
    }

    public void MergeIntoManifest(string manifestPath, JsonSerializerOptions options)
    {
        if (_stagedAssetsByHash.Count == 0 && _stagedAssetsBySourcePath.Values.All(static asset => asset.IsResolved))
        {
            return;
        }

        CleanAssetManifestDto manifest;
        if (File.Exists(manifestPath))
        {
            var text = File.ReadAllText(manifestPath);
            manifest = JsonSerializer.Deserialize<CleanAssetManifestDto>(text, options) ?? new CleanAssetManifestDto();
        }
        else
        {
            manifest = new CleanAssetManifestDto();
        }

        manifest.Sounds = _stagedAssetsByHash.Values
            .OrderBy(asset => asset.Hash, StringComparer.Ordinal)
            .Select(asset => new CleanAssetManifestSoundDto
            {
                Hash = asset.Hash,
                Sha256 = asset.Hash,
                SizeBytes = asset.SizeBytes,
                AssetRefSemantics = "runtimeExportRelative",
                ExportedPath = asset.RelativePath,
                SourcePaths = asset.SourcePaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList(),
                References = asset.References.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList()
            })
            .ToList();

        var unresolved = new HashSet<string>(manifest.UnresolvedSources, StringComparer.OrdinalIgnoreCase);
        foreach (var sourcePath in _stagedAssetsBySourcePath
                     .Where(static pair => pair.Value.IsResolved == false)
                     .Select(static pair => pair.Key))
        {
            unresolved.Add(sourcePath);
        }

        manifest.UnresolvedSources = unresolved
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, options));
    }

    private string? ResolvePhysicalSourcePath(string configuredSourcePath)
    {
        return AssetSourcePathResolver.TryResolveConfiguredPath(configuredSourcePath, _projectRootFolder, out var absolutePath)
            ? absolutePath
            : null;
    }

    private static string SanitizeFileStem(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var sanitized = Sanitize(value.Trim());
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }

    private static string Sanitize(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    private static string NormalizeToRelativeAssetPath(string filePath)
    {
        return filePath.Replace('\\', '/');
    }
}
