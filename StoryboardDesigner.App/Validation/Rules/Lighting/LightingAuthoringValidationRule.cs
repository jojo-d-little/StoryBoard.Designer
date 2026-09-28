using System.Text.RegularExpressions;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Lighting;

/// <summary>
/// Validates optional Designer-authored lighting values without resolving them
/// into Runtime-effective or Host-coordinate state.
/// </summary>
public sealed class LightingAuthoringValidationRule : IValidationRule
{
    private static readonly Regex ColorPattern =
        new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlySet<ScopeNodeKind> SupportedKinds =
        new HashSet<ScopeNodeKind>
        {
            ScopeNodeKind.Global,
            ScopeNodeKind.Templates,
            ScopeNodeKind.RoomTemplates,
            ScopeNodeKind.Room,
            ScopeNodeKind.GameObject,
            ScopeNodeKind.Book,
            ScopeNodeKind.Chapter,
            ScopeNodeKind.Page
        };

    private static readonly IReadOnlySet<string> MotionModes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "static",
            "sway",
            "flicker",
            "sway-flicker"
        };

    private static readonly IReadOnlySet<string> FlickerStyles =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "swell",
            "flame"
        };

    private static readonly IReadOnlySet<string> SwayStyles =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "directional",
            "breeze"
        };

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds => SupportedKinds;

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "LIGHT-001",
        Title: "Lighting Authoring Constraints",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Lighting");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        switch (context.CandidateNode)
        {
            case ProjectModel project:
                foreach (var issue in ValidatePointLight(
                             project.PointLightDefaults,
                             "Global / PointLightDefaults",
                             includeOffsets: false))
                {
                    yield return issue;
                }

                break;

            case Room room:
                foreach (var issue in ValidateRoomAmbient(room))
                {
                    yield return issue;
                }

                break;

            case PhaseNode phaseNode:
                foreach (var issue in ValidatePhaseOverlay(phaseNode))
                {
                    yield return issue;
                }

                break;

            case GameObject gameObject:
                foreach (var issue in ValidatePointLight(
                             gameObject.PointLight,
                             AppendPath(BuildScopePath(gameObject), "PointLight"),
                             includeOffsets: true))
                {
                    yield return issue;
                }

                foreach (var issue in ValidateLightOcclusion(gameObject))
                {
                    yield return issue;
                }

                break;
        }
    }

    private IEnumerable<ValidationIssue> ValidateRoomAmbient(Room room)
    {
        if (room.AmbientLighting is null)
        {
            yield break;
        }

        var path = AppendPath(BuildScopePath(room), "AmbientLighting");
        foreach (var issue in ValidateRange(room.AmbientLighting.Ambient, path, "ambient", 0, 1, "0..1"))
        {
            yield return issue;
        }

        foreach (var issue in ValidateColor(room.AmbientLighting.AmbientColor, path, "ambientColor"))
        {
            yield return issue;
        }
    }

    private IEnumerable<ValidationIssue> ValidatePhaseOverlay(PhaseNode phaseNode)
    {
        if (phaseNode.PhaseAmbientOverlay is null)
        {
            yield break;
        }

        var path = AppendPath(BuildScopePath(phaseNode), "PhaseAmbientOverlay");
        if (phaseNode.Tier is not (PhaseTier.Page or PhaseTier.Chapter))
        {
            yield return Issue(
                path,
                "phase ambient overlays are supported only on chapter and page phases.",
                "Move the overlay to a chapter or page, or remove it from the book phase.");
        }

        var overlay = phaseNode.PhaseAmbientOverlay;
        foreach (var issue in ValidateRange(overlay.AmbientScale, path, "ambientScale", 0, 2, "0..2"))
        {
            yield return issue;
        }

        foreach (var issue in ValidateColor(overlay.Color, path, "color"))
        {
            yield return issue;
        }

        foreach (var issue in ValidateRange(overlay.ColorBlend, path, "colorBlend", 0, 1, "0..1"))
        {
            yield return issue;
        }

        if (overlay.ColorMode == PhaseAmbientColorMode.Blend
            && string.IsNullOrWhiteSpace(overlay.Color))
        {
            yield return Issue(
                AppendPath(path, "colorMode"),
                "colorMode 'Blend' requires a phase overlay color.",
                "Set color to a six-digit #RRGGBB value or use colorMode 'Override' only when no blend color is needed.");
        }

        if (overlay.ColorMode == PhaseAmbientColorMode.Override
            && overlay.ColorBlend.HasValue)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                ValidationSeverity.Warning,
                AppendPath(path, "colorBlend"),
                "colorBlend is ignored when colorMode is 'Override'.",
                "Remove colorBlend or change colorMode to 'Blend'.");
        }
    }

    private IEnumerable<ValidationIssue> ValidateLightOcclusion(GameObject gameObject)
    {
        if (gameObject.LightOcclusion is null)
        {
            yield break;
        }

        var path = AppendPath(BuildScopePath(gameObject), "LightOcclusion");
        foreach (var issue in ValidateRange(gameObject.LightOcclusion.Strength, path, "strength", 0, 1, "0..1"))
        {
            yield return issue;
        }

        if (gameObject.SpatialType != RuntimeObjectSpatialTypes.SolidObject
            || gameObject.ObjectHeightUnits <= 0
            || gameObject.FootprintWidthCells <= 0
            || gameObject.FootprintHeightCells <= 0)
        {
            yield return Issue(
                path,
                "light occlusion requires a solid object with a positive height and footprint.",
                "Use a solid object with positive height/footprint values, or remove the light occlusion override.");
        }
    }

    private IEnumerable<ValidationIssue> ValidatePointLight(
        PointLightDefaults? defaults,
        string path,
        bool includeOffsets)
    {
        if (defaults is null)
        {
            yield break;
        }

        var pointLight = new PointLight
        {
            RadiusPx = defaults.RadiusPx,
            IntensityScale = defaults.IntensityScale,
            Color = defaults.Color,
            OuterColor = defaults.OuterColor,
            GradientExponent = defaults.GradientExponent,
            LightHeightCells = defaults.LightHeightCells,
            SwayAmountPx = defaults.SwayAmountPx,
            SwayHz = defaults.SwayHz,
            SwayDirectionDeg = defaults.SwayDirectionDeg,
            SwayStyle = defaults.SwayStyle,
            FlickerAmount = defaults.FlickerAmount,
            FlickerHz = defaults.FlickerHz,
            FlickerStyle = defaults.FlickerStyle
        };

        foreach (var issue in ValidatePointLight(pointLight, path, includeOffsets))
        {
            yield return issue;
        }
    }

    private IEnumerable<ValidationIssue> ValidatePointLight(
        PointLight? pointLight,
        string path,
        bool includeOffsets)
    {
        if (pointLight is null)
        {
            yield break;
        }

        if (includeOffsets)
        {
            foreach (var issue in ValidateFinite(pointLight.OffsetXPx, path, "offsetXPx"))
            {
                yield return issue;
            }

            foreach (var issue in ValidateFinite(pointLight.OffsetYPx, path, "offsetYPx"))
            {
                yield return issue;
            }
        }

        foreach (var issue in ValidateRange(pointLight.RadiusPx, path, "radiusPx", 0, null, "0 or greater"))
        {
            yield return issue;
        }

        foreach (var issue in ValidateFinite(pointLight.DirectionDeg, path, "directionDeg"))
        {
            yield return issue;
        }

        foreach (var issue in ValidateRange(pointLight.ConeAngleDeg, path, "coneAngleDeg", 1, 360, "1..360"))
        {
            yield return issue;
        }

        foreach (var issue in ValidateEnum(pointLight.MotionMode, path, "motionMode", MotionModes))
        {
            yield return issue;
        }

        foreach (var issue in ValidateFinite(pointLight.Phase, path, "phase"))
        {
            yield return issue;
        }

        if (pointLight.UseInstancePhaseShifting == true)
        {
            if (!pointLight.Phase.HasValue)
            {
                yield return Issue(
                    AppendPath(path, "phase"),
                    "phase is required and must be finite and nonzero when instance phase shifting is enabled.",
                    "Enter a finite, nonzero phase offset or disable instance phase shifting.");
            }
            else if (double.IsFinite(pointLight.Phase.Value) && pointLight.Phase.Value == 0)
            {
                yield return Issue(
                    AppendPath(path, "phase"),
                    "phase must be nonzero when instance phase shifting is enabled.",
                    "Enter a finite, nonzero phase offset or disable instance phase shifting.");
            }
        }

        foreach (var issue in ValidateColor(pointLight.Color, path, "color"))
        {
            yield return issue;
        }

        foreach (var issue in ValidateColor(pointLight.OuterColor, path, "outerColor"))
        {
            yield return issue;
        }

        foreach (var issue in ValidateRange(pointLight.GradientExponent, path, "gradientExponent", 0, null, "0 or greater"))
        {
            yield return issue;
        }

        foreach (var issue in ValidateRange(pointLight.IntensityScale, path, "intensityScale", 0, null, "0 or greater"))
        {
            yield return issue;
        }

        foreach (var issue in ValidateRange(pointLight.LightHeightCells, path, "lightHeightCells", 0, null, "0 or greater"))
        {
            yield return issue;
        }

        foreach (var issue in ValidateRange(pointLight.SwayAmountPx, path, "swayAmountPx", 0, null, "0 or greater"))
        {
            yield return issue;
        }

        foreach (var issue in ValidateRange(pointLight.SwayHz, path, "swayHz", 0, null, "0 or greater"))
        {
            yield return issue;
        }

        foreach (var issue in ValidateFinite(pointLight.SwayDirectionDeg, path, "swayDirectionDeg"))
        {
            yield return issue;
        }

        foreach (var issue in ValidateEnum(pointLight.SwayStyle, path, "swayStyle", SwayStyles))
        {
            yield return issue;
        }

        foreach (var issue in ValidateRange(pointLight.FlickerAmount, path, "flickerAmount", 0, 1, "0..1"))
        {
            yield return issue;
        }

        foreach (var issue in ValidateRange(pointLight.FlickerHz, path, "flickerHz", 0, null, "0 or greater"))
        {
            yield return issue;
        }

        foreach (var issue in ValidateEnum(pointLight.FlickerStyle, path, "flickerStyle", FlickerStyles))
        {
            yield return issue;
        }
    }

    private IEnumerable<ValidationIssue> ValidateFinite(double? value, string path, string fieldName)
    {
        if (value.HasValue && !double.IsFinite(value.Value))
        {
            yield return Issue(
                AppendPath(path, fieldName),
                $"{fieldName} must be a finite number.",
                $"Set {fieldName} to a finite value in the documented units.");
        }
    }

    private IEnumerable<ValidationIssue> ValidateRange(
        double? value,
        string path,
        string fieldName,
        double minimum,
        double? maximum,
        string expectedRange)
    {
        if (!value.HasValue)
        {
            yield break;
        }

        if (!double.IsFinite(value.Value)
            || value.Value < minimum
            || maximum.HasValue && value.Value > maximum.Value)
        {
            yield return Issue(
                AppendPath(path, fieldName),
                $"{fieldName} must be within {expectedRange}.",
                $"Set {fieldName} to a value within {expectedRange}.");
        }
    }

    private IEnumerable<ValidationIssue> ValidateColor(string? value, string path, string fieldName)
    {
        if (value is not null && !ColorPattern.IsMatch(value))
        {
            yield return Issue(
                AppendPath(path, fieldName),
                $"{fieldName} must be a six-digit #RRGGBB color.",
                $"Set {fieldName} to a value such as #FFFFFF.");
        }
    }

    private IEnumerable<ValidationIssue> ValidateEnum(
        string? value,
        string path,
        string fieldName,
        IReadOnlySet<string> allowedValues)
    {
        if (value is not null && !allowedValues.Contains(value))
        {
            yield return Issue(
                AppendPath(path, fieldName),
                $"{fieldName} must be one of: {string.Join(", ", allowedValues)}.",
                $"Choose a supported {fieldName} value.");
        }
    }

    private ValidationIssue Issue(string path, string description, string hint)
    {
        return new ValidationIssue(Metadata.RuleId, Metadata.DefaultSeverity, path, description, hint);
    }

    private static string BuildScopePath(ScopeNodeBase node)
    {
        var scopes = node
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static scope => scope.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (scopes.Count == 0)
        {
            return "Global";
        }

        var path = string.Join(" / ", scopes);
        return path.StartsWith("Global", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"Global / {path}";
    }

    private static string AppendPath(string path, string segment)
    {
        return $"{path} / {segment}";
    }
}
