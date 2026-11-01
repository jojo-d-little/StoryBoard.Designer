using Storyboard.Shared.DesignerContracts;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using System.Security.Cryptography;

namespace StoryboardDesigner.App.Services;

/// <summary>Resolves authored phase environment fields and creates their clean-export property view.</summary>
internal static class PhaseEnvironmentalProperties
{
    private sealed record Field(
        string Name,
        Func<PhaseNode, string?> Read,
        GamePropertyValueRestriction ValueRestriction = GamePropertyValueRestriction.Unrestricted);

    private static readonly Field[] Fields =
    [
        new("TimeOfDay", phase => phase.TimeOfDay?.ToString()),
        new("TimeOfYear", phase => phase.TimeOfYear?.ToString()),
        new("SkyCondition", phase => phase.SkyCondition?.ToString()),
        new("Precipitation", phase => phase.Precipitation?.ToString()),
        new("WeatherIntensity", phase => phase.WeatherIntensity?.ToString()),
        new("Temperature", phase => phase.Temperature?.ToString()),
        new("Storming", phase => phase.Storming?.ToString()),
        new("WindDirection", phase => phase.WindDirection?.ToString(System.Globalization.CultureInfo.InvariantCulture), GamePropertyValueRestriction.Numeric),
        new("WindSpeed", phase => phase.WindSpeed?.ToString("R", System.Globalization.CultureInfo.InvariantCulture), GamePropertyValueRestriction.Numeric)
    ];

    private static readonly (string Name, string Source, string Value)[] DirectConditions =
    [
        ("IsDawn", "TimeOfDay", "Dawn"), ("IsDay", "TimeOfDay", "Day"), ("IsEvening", "TimeOfDay", "Evening"), ("IsNight", "TimeOfDay", "Night"),
        ("IsWinter", "TimeOfYear", "Winter"), ("IsSpring", "TimeOfYear", "Spring"), ("IsSummer", "TimeOfYear", "Summer"), ("IsFall", "TimeOfYear", "Fall"),
        ("IsClear", "SkyCondition", "Clear"), ("IsCloudy", "SkyCondition", "Cloudy"), ("IsOvercast", "SkyCondition", "Overcast"), ("IsFoggy", "SkyCondition", "Fog"),
        ("IsRaining", "Precipitation", "Rain"), ("IsSnowing", "Precipitation", "Snow"), ("IsHailing", "Precipitation", "Hail"),
        ("IsWeatherLight", "WeatherIntensity", "Light"), ("IsWeatherModerate", "WeatherIntensity", "Moderate"), ("IsWeatherHeavy", "WeatherIntensity", "Heavy"), ("IsWeatherCatastrophic", "WeatherIntensity", "Catastrophic"),
        ("IsFreezing", "Temperature", "Freezing"), ("IsCold", "Temperature", "Cold"), ("IsMild", "Temperature", "Mild"), ("IsWarm", "Temperature", "Warm"), ("IsHot", "Temperature", "Hot")
    ];

    public static IReadOnlyDictionary<string, string> Resolve(PhaseNode phase)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (PhaseNode? current = phase; current is not null; current = current.ParentScope as PhaseNode)
        {
            foreach (var field in Fields)
            {
                if (result.ContainsKey(field.Name)) continue;
                var value = field.Read(current);
                if (value is not null) result[field.Name] = value;
            }
        }
        return result;
    }

    public static List<GamePropertyDefinition> BuildCleanProperties(PhaseNode page)
    {
        var effective = Resolve(page);
        var properties = new List<GamePropertyDefinition>();
        foreach (var field in Fields)
        {
            if (effective.TryGetValue(field.Name, out var value))
                properties.Add(new GamePropertyDefinition { Name = field.Name, DefaultValue = value, ValueRestriction = field.ValueRestriction });
        }

        foreach (var (name, source, value) in DirectConditions)
            properties.Add(ComputedEquals(name, source, value));
        properties.Add(ComputedPredicate("IsStorming", "Storming", ["Lightning", "Thunder", "Both"]));
        properties.Add(ComputedPredicate("IsLightning", "Storming", ["Lightning", "Both"]));
        properties.Add(ComputedPredicate("IsThundering", "Storming", ["Thunder", "Both"]));
        properties.Add(ComputedWeatherIntensityMultiplier(effective));
        if (effective.ContainsKey("WindSpeed"))
        {
            properties.Add(ComputedNumeric("EffectiveWindSpeed", new ProjectGamePropertyExpressionDto
            {
                Kind = "multiply",
                Left = PropertyExpression("WindSpeed"),
                Right = PropertyExpression("WeatherIntensityMultiplier")
            }, "0"));
        }
        if (effective.ContainsKey("WindDirection") && effective.ContainsKey("WindSpeed"))
        {
            properties.Add(ComputedAll("IsWindy",
            [
                ComputedComparison("WindDirection", "GreaterThanOrEqual", "Numeric", "0"),
                ComputedComparison("WindSpeed", "GreaterThan", "Numeric", "0")
            ]));
        }
        foreach (var property in properties)
        {
            property.Id = StableId(page.Id, property.Name);
            if (property.Computation is not null)
                property.GeneratedPreviewValue = property.Computation.OutputType == "Numeric"
                    ? EvaluateNumericPreview(property.Name, effective)
                    : EvaluatePreview(property.Name, effective) ? "true" : "false";
        }
        return properties;
    }

    private static bool EvaluatePreview(string condition, IReadOnlyDictionary<string, string> values)
    {
        if (condition == "IsStorming") return values.TryGetValue("Storming", out var storming) && storming != "None";
        if (condition == "IsLightning") return values.TryGetValue("Storming", out var lightning) && lightning is "Lightning" or "Both";
        if (condition == "IsThundering") return values.TryGetValue("Storming", out var thundering) && thundering is "Thunder" or "Both";
        if (condition == "IsWindy")
            return values.TryGetValue("WindDirection", out var directionText)
                && int.TryParse(directionText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var direction)
                && direction >= 0
                && values.TryGetValue("WindSpeed", out var speedText)
                && double.TryParse(speedText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var speed)
                && double.IsFinite(speed)
                && speed > 0;
        var direct = DirectConditions.FirstOrDefault(item => item.Name == condition);
        return direct.Name is not null && values.TryGetValue(direct.Source, out var value) && value == direct.Value;
    }

    private static string EvaluateNumericPreview(string name, IReadOnlyDictionary<string, string> values)
    {
        var multiplier = GetWeatherIntensityMultiplier(values);
        var result = name switch
        {
            "WeatherIntensityMultiplier" => multiplier,
            "EffectiveWindSpeed" when values.TryGetValue("WindSpeed", out var speedText)
                && double.TryParse(speedText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var speed)
                && double.IsFinite(speed) => speed * multiplier,
            _ => 0d
        };
        return result.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static double GetWeatherIntensityMultiplier(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue("WeatherIntensity", out var intensity) ? intensity switch
        {
            "Light" => 0.5d,
            "Moderate" => 1d,
            "Heavy" => 2d,
            "Catastrophic" => 3d,
            _ => 1d
        } : 1d;

    public static void MaterializePageProperties(PhaseNode page)
    {
        if (page.Tier != PhaseTier.Page) return;
        page.Variables.RemoveAll(static property => property.IsGeneratedEnvironmentalProperty);
        var authoredNames = page.Variables
            .Select(static property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var property in BuildCleanProperties(page).Where(property => !authoredNames.Contains(property.Name)))
        {
            property.IsGeneratedEnvironmentalProperty = true;
            page.Variables.Add(property);
        }
    }

    private static Guid StableId(Guid ownerId, string propertyName)
    {
        var name = System.Text.Encoding.UTF8.GetBytes(ownerId.ToString("N") + ":environment:" + propertyName.ToUpperInvariant());
        return new Guid(SHA256.HashData(name).AsSpan(0, 16));
    }

    private static GamePropertyDefinition ComputedEquals(string name, string source, string value) => Computed(name,
        Compare(source, "Equals", new ProjectGamePropertyExpressionDto { Kind = "literal", Value = value }));

    private static GamePropertyDefinition ComputedAll(string name, List<ProjectGamePropertyExpressionDto> conditions) => Computed(name,
        new ProjectGamePropertyExpressionDto { Kind = "and", Operands = conditions });

    private static ProjectGamePropertyExpressionDto ComputedComparison(string source, string operation, string valueKind, string value) => Compare(source, operation,
        new ProjectGamePropertyExpressionDto
        {
            Kind = "literal",
            Value = valueKind == "Numeric" && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
                ? number
                : value
        });

    private static GamePropertyDefinition ComputedPredicate(string name, string source, string[] values)
    {
        var comparisons = values.Select(value => Compare(source, "Equals", new ProjectGamePropertyExpressionDto { Kind = "literal", Value = value })).ToList();
        return Computed(name, comparisons.Count == 1 ? comparisons[0] : new ProjectGamePropertyExpressionDto { Kind = "or", Operands = comparisons });
    }

    private static GamePropertyDefinition ComputedWeatherIntensityMultiplier(IReadOnlyDictionary<string, string> values)
    {
        if (!values.ContainsKey("WeatherIntensity"))
            return ComputedNumeric("WeatherIntensityMultiplier", new ProjectGamePropertyExpressionDto { Kind = "literal", Value = 1d }, "0");

        var branches = new (string Intensity, double Multiplier)[]
        {
            ("Light", 0.5d), ("Moderate", 1d), ("Heavy", 2d), ("Catastrophic", 3d)
        };
        return ComputedNumeric("WeatherIntensityMultiplier", new ProjectGamePropertyExpressionDto
        {
            Kind = "select",
            Branches = branches.Select(branch => new ProjectGamePropertySelectBranchDto
            {
                When = Compare("WeatherIntensity", "Equals", new ProjectGamePropertyExpressionDto { Kind = "literal", Value = branch.Intensity }),
                Value = new ProjectGamePropertyExpressionDto { Kind = "literal", Value = branch.Multiplier }
            }).ToList(),
            Else = new ProjectGamePropertyExpressionDto { Kind = "literal", Value = 1d }
        }, "0");
    }

    private static GamePropertyDefinition ComputedNumeric(string name, ProjectGamePropertyExpressionDto expression, string defaultValue) => new()
    {
        Name = name,
        DefaultValue = defaultValue,
        ValueRestriction = GamePropertyValueRestriction.Numeric,
        Computation = new ProjectGamePropertyComputationV2Dto { OutputType = "Numeric", Expression = expression }
    };

    private static ProjectGamePropertyExpressionDto PropertyExpression(string name) => new()
    {
        Kind = "property",
        Source = Source(name)
    };

    private static ProjectGamePropertyExpressionDto Compare(string source, string operation, ProjectGamePropertyExpressionDto right) => new()
    {
        Kind = "compare", Operator = operation,
        Left = new ProjectGamePropertyExpressionDto { Kind = "property", Source = Source(source) }, Right = right
    };

    private static GamePropertyDefinition Computed(string name, ProjectGamePropertyExpressionDto expression) => new()
    {
        Name = name, DefaultValue = "false", ValueRestriction = GamePropertyValueRestriction.TrueFalse,
        Computation = new ProjectGamePropertyComputationV2Dto { OutputType = "Boolean", Expression = expression }
    };

    private static ProjectGamePropertySourceReferenceDto Source(string name) => new() { AnchorKey = "currentPhase", PropertyName = name };
}
