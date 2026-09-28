namespace StoryboardDesigner.App.Models;

/// <summary>
/// Designer-authored point-light definition. Position is local pixel offset
/// from the object's top-left footprint cell corner.
/// </summary>
public sealed class PointLight
{
    public double? OffsetXPx { get; set; }
    public double? OffsetYPx { get; set; }
    public double? RadiusPx { get; set; }
    public double? DirectionDeg { get; set; }
    public double? ConeAngleDeg { get; set; }
    public string? MotionMode { get; set; }
    public double? Phase { get; set; }
    public bool? UseInstancePhaseShifting { get; set; }
    public string? Color { get; set; }
    public string? OuterColor { get; set; }
    public double? GradientExponent { get; set; }
    public double? IntensityScale { get; set; }
    public double? LightHeightCells { get; set; }
    public double? SwayAmountPx { get; set; }
    public double? SwayHz { get; set; }
    public double? SwayDirectionDeg { get; set; }
    public string? SwayStyle { get; set; }
    public double? FlickerAmount { get; set; }
    public double? FlickerHz { get; set; }
    public string? FlickerStyle { get; set; }
}
