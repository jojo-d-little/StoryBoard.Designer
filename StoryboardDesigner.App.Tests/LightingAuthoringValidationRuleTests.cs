using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Lighting;

namespace StoryboardDesigner.App.Tests;

public sealed class LightingAuthoringValidationRuleTests
{
    [Fact]
    public void Evaluate_AcceptsValidBoundaryValuesAndOptionalLighting()
    {
        var roomObject = new GameObject
        {
            Name = "Boundary Light",
            ObjectHeightUnits = 1,
            PointLight = new PointLight
            {
                OffsetXPx = -25,
                OffsetYPx = 0,
                RadiusPx = 0,
                ConeAngleDeg = 360,
                MotionMode = "sway-flicker",
                Color = "#000000",
                OuterColor = "#FFFFFF",
                GradientExponent = 0,
                IntensityScale = 0,
                LightHeightCells = 0,
                SwayAmountPx = 0,
                SwayHz = 0,
                FlickerAmount = 1,
                FlickerHz = 0,
                FlickerStyle = "flame"
            },
            LightOcclusion = new LightOcclusion { Strength = 0 }
        };

        var room = new Room
        {
            Name = "Room",
            AmbientLighting = new RoomAmbientLighting
            {
                Ambient = 0,
                AmbientColor = "#000000"
            },
            GameObjects = [roomObject]
        };

        var chapter = new PhaseNode
        {
            Tier = PhaseTier.Chapter,
            PhaseKey = "book.chapter",
            DisplayName = "Chapter",
            PhaseAmbientOverlay = new PhaseAmbientOverlay
            {
                AmbientScale = 2,
                Color = "#FFFFFF",
                ColorMode = PhaseAmbientColorMode.Blend,
                ColorBlend = 0
            }
        };

        var project = BuildProject(room, chapter);
        project.PointLightDefaults = new PointLightDefaults
        {
            RadiusPx = 0,
            IntensityScale = 0,
            Color = "#000000",
            OuterColor = "#FFFFFF",
            GradientExponent = 0,
            LightHeightCells = 0,
            SwayAmountPx = 0,
            SwayHz = 0,
            FlickerAmount = 1,
            FlickerHz = 0,
            FlickerStyle = "swell"
        };

        var result = Evaluate(project);

        Assert.Empty(result.Issues);
        Assert.Contains("LIGHT-001", result.ExecutedRuleIds);
    }

    [Fact]
    public void Evaluate_ReportsSchemaRangeColorAndEnumViolations()
    {
        var roomObject = new GameObject
        {
            Name = "Invalid Light",
            PointLight = new PointLight
            {
                RadiusPx = -1,
                ConeAngleDeg = 0,
                MotionMode = "invalid-mode",
                Color = "white",
                FlickerAmount = 2,
                FlickerStyle = "invalid-style"
            }
        };

        var room = new Room
        {
            Name = "Invalid Room",
            AmbientLighting = new RoomAmbientLighting
            {
                Ambient = -0.1,
                AmbientColor = "#FFF"
            },
            GameObjects = [roomObject]
        };

        var chapter = new PhaseNode
        {
            Tier = PhaseTier.Chapter,
            PhaseKey = "book.chapter",
            DisplayName = "Chapter",
            PhaseAmbientOverlay = new PhaseAmbientOverlay
            {
                AmbientScale = 2.1,
                Color = null,
                ColorMode = PhaseAmbientColorMode.Blend
            }
        };

        var project = BuildProject(room, chapter);
        project.PointLightDefaults = new PointLightDefaults { IntensityScale = -1 };

        var issues = Evaluate(project).Issues;

        Assert.NotEmpty(issues);
        Assert.All(issues, issue => Assert.Equal("LIGHT-001", issue.RuleId));
        Assert.Contains(issues, issue => issue.Description.Contains("radiusPx", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Description.Contains("coneAngleDeg", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Description.Contains("motionMode", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Description.Contains("six-digit", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Description.Contains("ambientScale", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Description.Contains("requires a phase overlay color", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_RejectsBookOverlayAndMeaninglessOcclusion()
    {
        var roomObject = new GameObject
        {
            Name = "Passive Object",
            SpatialType = RuntimeObjectSpatialTypes.PassiveObject,
            ObjectHeightUnits = 0,
            LightOcclusion = new LightOcclusion { Strength = 0 }
        };

        var room = new Room { Name = "Room", GameObjects = [roomObject] };
        var book = new PhaseNode
        {
            Tier = PhaseTier.Book,
            PhaseKey = "book",
            DisplayName = "Book",
            PhaseAmbientOverlay = new PhaseAmbientOverlay { AmbientScale = 1 }
        };

        var issues = Evaluate(BuildProject(room, book)).Issues;

        Assert.Contains(issues, issue => issue.Description.Contains("only on chapter and page", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => issue.Description.Contains("requires a solid object", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_AllowsOmittedLightingAndExplicitDisabledDefault()
    {
        var project = BuildProject(new Room
        {
            Name = "Unlit Room",
            GameObjects =
            [
                new GameObject
                {
                    Name = "Ordinary Object",
                    IsPointLightEnabledDefaultValue = false
                }
            ]
        }, phase: null);

        var result = Evaluate(project);

        Assert.Empty(result.Issues);
    }

    private static ValidationExecutionResult Evaluate(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new LightingAuthoringValidationRule());

        return new ValidationEngine(registry).Execute(new ValidationExecutionRequest(project));
    }

    private static ProjectModel BuildProject(Room room, PhaseNode? phase)
    {
        var area = new Area
        {
            Name = "Area",
            Rooms = [room],
            StartingRoomId = room.Id
        };
        var country = new Country
        {
            Name = "Country",
            Areas = [area],
            StartingAreaName = area.Name
        };
        var planet = new Planet
        {
            Name = "Planet",
            Countries = [country],
            StartingCountryName = country.Name
        };
        var project = new ProjectModel
        {
            Name = "LightingValidation",
            Planets = [planet]
        };

        if (phase is not null)
        {
            project.PhaseBooks = [phase];
        }

        ScopeHierarchy.AttachParents(project);
        return project;
    }
}
