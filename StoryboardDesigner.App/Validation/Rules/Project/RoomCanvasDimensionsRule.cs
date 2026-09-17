using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class RoomCanvasDimensionsRule : IValidationRule
{
    private const int MinimumGridCells = 4;
    private const int MaximumGridCells = 200;

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Room, ScopeNodeKind.RoomTemplates };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "ROOM-001",
        Title: "Room Canvas Dimensions",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Project");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        var projectCellSize = context.Project.RoomDesignerGridCellSize > 0
            ? context.Project.RoomDesignerGridCellSize
            : 40;

        foreach (var (room, path) in EnumerateRooms(context.Project))
        {
            var width = room.RoomImageCanvasWidth;
            var height = room.RoomImageCanvasHeight;

            if (width <= 0 || height <= 0)
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    path,
                    $"room canvas size must be positive, but found {width}x{height}.",
                    "Set room width and height to positive values in room settings.");
                continue;
            }

            if (width % projectCellSize != 0 || height % projectCellSize != 0)
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    path,
                    $"room canvas size {width}x{height} is not divisible by project grid cell size {projectCellSize}.",
                    $"Adjust room width/height so both divide evenly by {projectCellSize}.");
                continue;
            }

            var columns = width / projectCellSize;
            var rows = height / projectCellSize;
            if (columns is < MinimumGridCells or > MaximumGridCells || rows is < MinimumGridCells or > MaximumGridCells)
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    path,
                    $"room canvas size {width}x{height} resolves to {columns} columns x {rows} rows, outside allowed range {MinimumGridCells}..{MaximumGridCells}.",
                    "Resize the room or adjust project cell size to keep rows and columns within limits.");
            }
        }
    }

    private static IEnumerable<(Room Room, string Path)> EnumerateRooms(ProjectModel project)
    {
        foreach (var roomTemplate in project.RoomTemplates)
        {
            var templateName = string.IsNullOrWhiteSpace(roomTemplate.Name) ? "(unnamed room template)" : roomTemplate.Name.Trim();
            yield return (roomTemplate, $"Global / Room Templates / {templateName}");
        }

        foreach (var planet in project.Planets)
        {
            var planetName = NormalizeScopeName(planet.Name, "(unnamed planet)");
            foreach (var country in planet.Countries)
            {
                var countryName = NormalizeScopeName(country.Name, "(unnamed country)");
                foreach (var area in country.Areas)
                {
                    var areaName = NormalizeScopeName(area.Name, "(unnamed area)");
                    foreach (var room in area.Rooms)
                    {
                        var roomName = NormalizeScopeName(room.Name, "(unnamed room)");
                        yield return (room, $"Global / {planetName} / {countryName} / {areaName} / {roomName}");
                    }
                }
            }
        }
    }

    private static string NormalizeScopeName(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
