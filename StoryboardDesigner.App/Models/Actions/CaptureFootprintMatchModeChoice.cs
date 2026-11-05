using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Models;

public sealed record CaptureFootprintMatchModeChoice(
    FootprintMatchMode Value,
    string DisplayName,
    string Explanation);

public static class CaptureFootprintMatchModeChoices
{
    public static IReadOnlyList<CaptureFootprintMatchModeChoice> All { get; } =
    [
        new(FootprintMatchMode.AnyOverlap, "Any overlap", "Includes objects sharing one or more grid cells with the target footprint."),
        new(FootprintMatchMode.FullyContained, "Fully contained", "Includes only objects whose entire footprint lies within the target footprint.")
    ];
}
