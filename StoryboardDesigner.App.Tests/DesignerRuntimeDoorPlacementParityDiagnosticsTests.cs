using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using Xunit.Abstractions;

namespace StoryboardDesigner.App.Tests;

public sealed class DesignerRuntimeDoorPlacementParityDiagnosticsTests
{
    private static readonly ExpectedDoorPlacement[] ExpectedPlacements =
    [
        new("Door_N", "Closed", 280, 0, 6, 0, 0, 286, 0),
        new("Door_E", "Open", 760, 160, 40, 25, 90, 800, 185),
        new("Door_S", "Closed", 280, 560, 234, 40, 180, 514, 600),
        new("Door_W", "Closed", 0, 160, 0, 255, 270, 0, 415)
    ];

    private readonly ITestOutputHelper _output;

    public DesignerRuntimeDoorPlacementParityDiagnosticsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void WorkshopTutorialAtrium_Doors_DesignerSourceAndPreviewMatchExpectedPlacements()
    {
        var authoredProject = LoadAuthoredProject(GetAuthoredProjectPath());
        var authoredAtrium = FindRoomByName(authoredProject, "Atrium");
        var reportLines = new List<string>();

        foreach (var expected in ExpectedPlacements)
        {
            var authoredDoor = FindAuthoredDoor(authoredAtrium, expected.Name);
            var variantName = ResolveDoorVariantName(authoredDoor);
            Assert.Equal(expected.VariantName, variantName);

            var preview = new RoomDesignerPreviewObjectViewModel(authoredDoor)
            {
                SelectedPreviewVariantName = variantName
            };
            var drawX = authoredDoor.PositionX + preview.ImageLocalAlignmentOffsetX;
            var drawY = authoredDoor.PositionY + preview.ImageLocalAlignmentOffsetY;

            Assert.Equal(expected.AnchorX, authoredDoor.PositionX, 6);
            Assert.Equal(expected.AnchorY, authoredDoor.PositionY, 6);
            Assert.Equal(expected.IconOffsetX, preview.ImageLocalAlignmentOffsetX, 6);
            Assert.Equal(expected.IconOffsetY, preview.ImageLocalAlignmentOffsetY, 6);
            Assert.Equal(expected.RotationDegrees, authoredDoor.ImageRotationDegrees, 6);
            Assert.Equal(expected.DrawX, drawX, 6);
            Assert.Equal(expected.DrawY, drawY, 6);

            reportLines.Add(
                $"{expected.Name}: variant={variantName}; anchor=({authoredDoor.PositionX:0.###},{authoredDoor.PositionY:0.###}); "
                + $"iconOffset=({preview.ImageLocalAlignmentOffsetX:0.###},{preview.ImageLocalAlignmentOffsetY:0.###}); "
                + $"draw=({drawX:0.###},{drawY:0.###}); rotation={authoredDoor.ImageRotationDegrees:0.###}; "
                + $"previewCompare={preview.PlacementComparisonSnapshot}");
        }

        Assert.Equal(ExpectedPlacements.Length, reportLines.Count);
        foreach (var line in reportLines)
        {
            _output.WriteLine(line);
        }
    }

    private static GameObject FindAuthoredDoor(Room room, string doorName)
    {
        return room.GameObjects.FirstOrDefault(obj =>
                   string.Equals(obj.Name, doorName, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException($"Unable to resolve authored door '{doorName}'.");
    }

    private static string ResolveDoorVariantName(GameObject door)
    {
        if (door.IsOpenDefaultValue)
        {
            return door.ImageVariants.Any(v => string.Equals(v.VariantName, "Open", StringComparison.OrdinalIgnoreCase))
                ? "Open"
                : string.Empty;
        }

        return door.ImageVariants.Any(v => string.Equals(v.VariantName, "Closed", StringComparison.OrdinalIgnoreCase))
            ? "Closed"
            : string.Empty;
    }

    private static Room FindRoomByName(ProjectModel project, string roomName)
    {
        foreach (var planet in project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                foreach (var area in country.Areas)
                {
                    var room = area.Rooms.FirstOrDefault(r => string.Equals(r.Name, roomName, StringComparison.OrdinalIgnoreCase));
                    if (room is not null)
                    {
                        return room;
                    }
                }
            }
        }

        throw new InvalidOperationException($"Unable to resolve room '{roomName}'.");
    }

    private static ProjectModel LoadAuthoredProject(string authoredProjectPath)
    {
        var project = new JsonExportService().TryLoadProjectModel(authoredProjectPath);
        if (project is null)
        {
            throw new InvalidOperationException($"Unable to load authored project '{authoredProjectPath}'.");
        }

        ScopeHierarchy.AttachParents(project);
        return project;
    }

    private static string GetAuthoredProjectPath() => Path.Combine(
        AppContext.BaseDirectory,
        "Samples",
        "WorkshopTutorial",
        "WorkshopTutorial.sbe.json");

    private sealed record ExpectedDoorPlacement(
        string Name,
        string VariantName,
        double AnchorX,
        double AnchorY,
        double IconOffsetX,
        double IconOffsetY,
        double RotationDegrees,
        double DrawX,
        double DrawY);
}
