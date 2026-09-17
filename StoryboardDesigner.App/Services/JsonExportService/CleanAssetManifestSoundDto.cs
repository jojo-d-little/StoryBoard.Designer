namespace StoryboardDesigner.App.Services;

internal sealed class CleanAssetManifestSoundDto
{
    public string Hash { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string AssetRefSemantics { get; set; } = "runtimeExportRelative";

    public string ExportedPath { get; set; } = string.Empty;

    public List<string> SourcePaths { get; set; } = new();

    public List<string> References { get; set; } = new();
}
