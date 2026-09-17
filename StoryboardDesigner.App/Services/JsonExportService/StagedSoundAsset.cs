namespace StoryboardDesigner.App.Services;

internal sealed class StagedSoundAsset
{
    private StagedSoundAsset(bool isResolved, string hash, string relativePath, long sizeBytes)
    {
        IsResolved = isResolved;
        Hash = hash;
        RelativePath = relativePath;
        SizeBytes = sizeBytes;
    }

    public bool IsResolved { get; }

    public string Hash { get; }

    public string RelativePath { get; }

    public long SizeBytes { get; }

    public HashSet<string> SourcePaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> References { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static StagedSoundAsset CreateResolved(string hash, string relativePath, long sizeBytes)
    {
        return new StagedSoundAsset(true, hash, relativePath, sizeBytes);
    }

    public static StagedSoundAsset CreateUnresolved(string sourcePath)
    {
        return new StagedSoundAsset(false, string.Empty, string.Empty, 0)
        {
            SourcePaths = { sourcePath }
        };
    }

    public void RegisterReference(string sourcePath, string reference)
    {
        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            SourcePaths.Add(sourcePath);
        }

        if (!string.IsNullOrWhiteSpace(reference))
        {
            References.Add(reference);
        }
    }
}
