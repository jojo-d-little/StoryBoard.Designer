namespace StoryboardDesigner.App.Models;

public sealed class ObjectImageVariant
{
    public string VariantName { get; set; } = string.Empty;
    public string FullImagePath { get; set; } = string.Empty;
    public double ImageLocalAlignmentRotationDegrees { get; set; }
    public double ImageLocalAlignmentOffsetX { get; set; }
    public double ImageLocalAlignmentOffsetY { get; set; }
    public double ImageScale { get; set; } = 1;
    public bool IsDefault { get; set; }
}
