namespace StoryboardDesigner.App.Models;

/// <summary>
/// Optional project-level fallbacks for individual point lights.
/// </summary>
public sealed class PointLightDefaults
{
    public double? RadiusPx { get; set; }
    public double? IntensityScale { get; set; }
    public string? Color { get; set; }
    public string? OuterColor { get; set; }
    public double? GradientExponent { get; set; }
    public double? LightHeightCells { get; set; }
    public double? SwayAmountPx { get; set; }
    public double? SwayHz { get; set; }
    public double? SwayDirectionDeg { get; set; }
    public double? FlickerAmount { get; set; }
    public double? FlickerHz { get; set; }
    public string? FlickerStyle { get; set; }
}
