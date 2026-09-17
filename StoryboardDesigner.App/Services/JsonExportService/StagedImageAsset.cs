using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;
internal sealed class StagedImageAsset
{
    private StagedImageAsset(bool isResolved, string hash, string relativePath, long sizeBytes)
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

    public static StagedImageAsset CreateResolved(string hash, string relativePath, long sizeBytes)
    {
        return new StagedImageAsset(true, hash, relativePath, sizeBytes);
    }

    public static StagedImageAsset CreateUnresolved(string sourcePath)
    {
        return new StagedImageAsset(false, string.Empty, string.Empty, 0)
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

