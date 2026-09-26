using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Models;

/// <summary>
/// Optional phase-authored contribution to the room ambient baseline.
/// Page values take precedence over chapter values at Runtime.
/// </summary>
public sealed class PhaseAmbientOverlay
{
    public double? AmbientScale { get; set; }
    public string? Color { get; set; }
    public PhaseAmbientColorMode? ColorMode { get; set; }
    public double? ColorBlend { get; set; }
}
