using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;
internal sealed class CleanAssetManifestDto
{
    public string SchemaVersion { get; set; } = "1.0";

    public List<CleanAssetManifestImageDto> Images { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CleanAssetManifestSoundDto>? Sounds { get; set; }

    public List<string> UnresolvedSources { get; set; } = new();
}

