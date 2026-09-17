using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;
internal sealed class CleanAssetManifestImageDto
{
    public string Hash { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string ImagePathSemantics { get; set; } = "runtimeExportRelative";

    public string ExportedPath { get; set; } = string.Empty;

    public List<string> SourcePaths { get; set; } = new();

    public List<string> References { get; set; } = new();
}

