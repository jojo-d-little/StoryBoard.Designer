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
        new("WindDirection", phase => phase.WindDirection?.ToString(System.Globalization.CultureInfo.InvariantCulture), GamePropertyValueRestriction.Numeric)
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
        properties.Add(ComputedNotEquals("IsWindy", "WindDirection", "-1"));
        foreach (var property in properties)
        {
            property.Id = StableId(page.Id, property.Name);
            if (property.Computation is not null)
                property.GeneratedPreviewValue = EvaluatePreview(property.Name, effective) ? "true" : "false";
        }
        return properties;
    }

    private static bool EvaluatePreview(string condition, IReadOnlyDictionary<string, string> values)
    {
        if (condition == "IsStorming") return values.TryGetValue("Storming", out var storming) && storming != "None";
        if (condition == "IsLightning") return values.TryGetValue("Storming", out var lightning) && lightning is "Lightning" or "Both";
        if (condition == "IsThundering") return values.TryGetValue("Storming", out var thundering) && thundering is "Thunder" or "Both";
        if (condition == "IsWindy") return values.TryGetValue("WindDirection", out var wind) && wind != "-1";
        var direct = DirectConditions.FirstOrDefault(item => item.Name == condition);
        return direct.Name is not null && values.TryGetValue(direct.Source, out var value) && value == direct.Value;
    }

    public static void MaterializePageProperties(PhaseNode page)
    {
        if (page.Tier != PhaseTier.Page) return;
        page.Variables.RemoveAll(static property => property.IsGeneratedEnvironmentalProperty);
        foreach (var property in BuildCleanProperties(page))
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
        new ProjectGamePropertyConditionDto
        {
            Kind = "Compare", GamePropertySource = Source(source), Operator = "Equals",
            RightOperand = new ProjectGamePropertyOperandDto { ValueKind = "Text", Value = value }
        });

    private static GamePropertyDefinition ComputedNotEquals(string name, string source, string value) => Computed(name,
        new ProjectGamePropertyConditionDto
        {
            Kind = "Compare", GamePropertySource = Source(source), Operator = "NotEquals",
            RightOperand = new ProjectGamePropertyOperandDto { ValueKind = "Numeric", Value = value }
        });

    private static GamePropertyDefinition ComputedPredicate(string name, string source, string[] values)
    {
        var comparisons = values.Select(value => new ProjectGamePropertyConditionDto
        {
            Kind = "Compare", GamePropertySource = Source(source), Operator = "Equals",
            RightOperand = new ProjectGamePropertyOperandDto { ValueKind = "Text", Value = value }
        }).ToList();
        return Computed(name, comparisons.Count == 1 ? comparisons[0] : new ProjectGamePropertyConditionDto { Kind = "Any", Conditions = comparisons });
    }

    private static GamePropertyDefinition Computed(string name, ProjectGamePropertyConditionDto condition) => new()
    {
        Name = name, DefaultValue = "false", ValueRestriction = GamePropertyValueRestriction.TrueFalse,
        Computation = new ProjectGamePropertyComputationDto { ResultKind = "Boolean", Condition = condition }
    };

    private static ProjectGamePropertySourceReferenceDto Source(string name) => new() { AnchorKey = "currentPhase", PropertyName = name };
}
