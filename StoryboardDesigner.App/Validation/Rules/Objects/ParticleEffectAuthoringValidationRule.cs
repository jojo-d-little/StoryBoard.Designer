using System.Globalization;
using System.Text.Json;
using Storyboard.Shared.DesignerContracts;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Objects;

public sealed class ParticleEffectAuthoringValidationRule : IValidationRule
{
    private readonly ParticleEffectAuthoringCatalogService _catalog;

    public ParticleEffectAuthoringValidationRule() : this(new ParticleEffectAuthoringCatalogService()) { }

    public ParticleEffectAuthoringValidationRule(ParticleEffectAuthoringCatalogService catalog) => _catalog = catalog;

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = Enum.GetValues<ScopeNodeKind>().ToHashSet();

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "FX-001",
        Title: "Particle Effect Authoring Must Match the Installed Catalog",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Presentation");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        if (context.CandidateNode is ProjectModel project)
        {
            var seenImageChoices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var choice in project.ParticleEffectImageChoices)
            {
                var path = $"Global / Particle effect '{choice.EffectKey}' / image slot '{choice.ImageSlot}'";
                var choiceKey = $"{choice.EffectKey.Trim()}\u001f{choice.ImageSlot.Trim()}";
                if (!seenImageChoices.Add(choiceKey))
                {
                    yield return Issue(path, "Only one project image override is allowed for an effect key and image slot.");
                    continue;
                }
                if (FindRecipe(choice.EffectKey) is null)
                {
                    yield return Issue(path, $"Effect key '{choice.EffectKey}' is not present in the installed particle catalog.");
                    continue;
                }
                if (!_catalog.GetImageSlots(choice.EffectKey).Contains(choice.ImageSlot, StringComparer.OrdinalIgnoreCase))
                    yield return Issue(path, $"Image slot '{choice.ImageSlot}' is not declared for effect key '{choice.EffectKey}'.");
                if (string.IsNullOrWhiteSpace(choice.AssetPath))
                    yield return Issue(path, "A project image path is required for each configured image override.");
            }

            foreach (var effect in project.EnvironmentalEffects)
            {
                var path = $"{project.ScopeName} / Environmental effect '{effect.Name}'";
                foreach (var issue in ValidateDefinition(project, Guid.Empty, effect.EffectKey, effect.Settings, path, ParticleEffectAuthoringContext.Environmental)) yield return issue;
                foreach (var issue in ValidateReference(project, Guid.Empty, effect.EnabledBinding, GamePropertyValueRestriction.TrueFalse, path, "Enabled")) yield return issue;
            }
            yield break;
        }

        if (context.CandidateNode is GameObject gameObject)
        {
            if (gameObject.ParticleEffect is { } attachment)
                foreach (var issue in ValidateDefinition(context.Project, gameObject.ObjectId, attachment.EffectKey, attachment.Settings,
                             $"{gameObject.ScopeName} / Particle effect", ParticleEffectAuthoringContext.ObjectAttachment)) yield return issue;
            yield break;
        }

        if (context.CandidateNode is ScopeNodeBase scope)
        {
            var ownerId = ScopeOwnerId(scope);
            foreach (var effect in scope.EnvironmentalEffects)
            {
                var path = $"{scope.ScopeName} / Environmental effect '{effect.Name}'";
                foreach (var issue in ValidateDefinition(context.Project, ownerId, effect.EffectKey, effect.Settings, path, ParticleEffectAuthoringContext.Environmental)) yield return issue;
                foreach (var issue in ValidateReference(context.Project, ownerId, effect.EnabledBinding, GamePropertyValueRestriction.TrueFalse, path, "Enabled")) yield return issue;
            }
        }
    }

    private IEnumerable<ValidationIssue> ValidateDefinition(
        ProjectModel project,
        Guid ownerId,
        string effectKey,
        IEnumerable<ParticleEffectControlSettingDto> settings,
        string path,
        ParticleEffectAuthoringContext context)
    {
        var recipe = FindRecipe(effectKey);
        if (recipe is null)
        {
            yield return Issue(path, $"Effect key '{effectKey}' is not present in the installed particle catalog.");
            yield break;
        }
        if (!_catalog.SupportsContext(effectKey, context))
        {
            var routes = context == ParticleEffectAuthoringContext.ObjectAttachment ? "point or stain" : "room or overlay";
            yield return Issue(path, $"Effect key '{effectKey}' uses the '{recipe.RenderingMode}' rendering mode and cannot be used here; this context supports {routes} recipes.");
            yield break;
        }

        var propertyList = settings.ToList();
        var duplicateKeys = propertyList.GroupBy(static item => item.ControlKey?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1).Select(static group => group.Key);
        foreach (var key in duplicateKeys) yield return Issue(path, $"Control setting '{key}' is defined more than once.");

        var controls = _catalog.GetControls(effectKey).ToDictionary(static control => control.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var setting in propertyList)
        {
            var key = setting.ControlKey?.Trim() ?? string.Empty;
            var roleColor = key.EndsWith(".Primary", StringComparison.OrdinalIgnoreCase)
                || key.EndsWith(".Secondary", StringComparison.OrdinalIgnoreCase);
            var controlName = roleColor ? key[..key.LastIndexOf('.')] : key;
            if (!controls.TryGetValue(controlName, out var control))
            {
                yield return Issue(path, $"Setting '{key}' is not a control declared for effect family '{recipe.Family}'.");
                continue;
            }
            if (setting.Value is null && setting.Binding is null)
            {
                yield return Issue(path, $"Setting '{key}' must contain a literal value, a variable binding, or both.");
                continue;
            }

            var kind = roleColor ? "color" : control.Kind;
            var expectedRestriction = ExpectedRestriction(kind);
            if (setting.Binding is not null && expectedRestriction is { } restriction)
                foreach (var issue in ValidateReference(project, ownerId, setting.Binding, restriction, path, key)) yield return issue;

            if (setting.Value is not null)
            {
                var value = ScalarText(setting.Value);
                if (value is null)
                {
                    yield return Issue(path, $"Control '{key}' requires a scalar literal value.");
                    continue;
                }
                if (string.Equals(kind, "number", StringComparison.OrdinalIgnoreCase))
                {
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                    {
                        yield return Issue(path, $"Control '{key}' needs a finite numeric value.");
                        continue;
                    }
                    if ((control.Minimum.HasValue && number < control.Minimum.Value) || (control.Maximum.HasValue && number > control.Maximum.Value))
                        yield return Issue(path, $"Control '{key}' must be between {control.Minimum?.ToString(CultureInfo.InvariantCulture) ?? "-infinity"} and {control.Maximum?.ToString(CultureInfo.InvariantCulture) ?? "infinity"} {control.Unit}.");
                }
                else if (string.Equals(kind, "color", StringComparison.OrdinalIgnoreCase) && value.Length != 0 && !IsHexColor(value))
                    yield return Issue(path, $"Control '{key}' needs an sRGB hex color in #RRGGBB form.");
                else if (string.Equals(kind, "boolean", StringComparison.OrdinalIgnoreCase) && !bool.TryParse(value, out _))
                    yield return Issue(path, $"Control '{key}' needs a true or false value.");
            }
        }

        var speed = propertyList.FirstOrDefault(static item => string.Equals(item.ControlKey, "WindSpeedPxPerSecond", StringComparison.OrdinalIgnoreCase));
        var direction = propertyList.FirstOrDefault(static item => string.Equals(item.ControlKey, "WindDirectionDegrees", StringComparison.OrdinalIgnoreCase));
        if (speed?.Binding is null && speed?.Value is not null
            && double.TryParse(ScalarText(speed.Value), NumberStyles.Float, CultureInfo.InvariantCulture, out var speedValue)
            && speedValue > 0 && (direction is null || direction.Value is null && direction.Binding is null))
            yield return Issue(path, "Positive WindSpeedPxPerSecond requires a WindDirectionDegrees value or binding.");
    }

    private IEnumerable<ValidationIssue> ValidateReference(
        ProjectModel project,
        Guid relativeOwnerId,
        ProjectGamePropertySourceReferenceDto? source,
        GamePropertyValueRestriction expectedRestriction,
        string path,
        string settingName)
    {
        if (source is null) yield break;
        if (string.IsNullOrWhiteSpace(source.PropertyName) || source.OwnerId.HasValue && !string.IsNullOrWhiteSpace(source.AnchorKey))
        {
            yield return Issue(path, $"Binding for '{settingName}' has an invalid source reference.");
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(source.AnchorKey))
        {
            var anchor = source.AnchorKey.Trim();
            if (!GamePropertySourceResolver.SupportsAnchor(project, anchor))
            {
                yield return Issue(path, $"Binding for '{settingName}' uses unsupported anchor '{anchor}'.");
                yield break;
            }
        }

        var matches = GamePropertySourceResolver.Resolve(project, source, relativeOwnerId);
        if (matches.Count == 0)
        {
            yield return Issue(path, $"Binding for '{settingName}' cannot resolve variable '{source.PropertyName}'.");
            yield break;
        }
        if (string.IsNullOrWhiteSpace(source.AnchorKey) && matches.Count > 1)
        {
            yield return Issue(path, $"Binding for '{settingName}' is ambiguous for variable '{source.PropertyName}'.");
            yield break;
        }
        var restrictions = matches.Select(static match => match.Property.ValueRestriction).Distinct().ToList();
        if (restrictions.Count != 1)
        {
            yield return Issue(path, $"Binding for '{settingName}' has inconsistent types across anchor owners.");
            yield break;
        }
        if (restrictions[0] != expectedRestriction)
            yield return Issue(path, $"Binding for '{settingName}' has type {restrictions[0]}; this setting requires {expectedRestriction}.");
    }

    private static GamePropertyValueRestriction? ExpectedRestriction(string kind) => kind.ToLowerInvariant() switch
    {
        "number" => GamePropertyValueRestriction.Numeric,
        "boolean" => GamePropertyValueRestriction.TrueFalse,
        "color" or "colorpair" or "enum" or "string" => GamePropertyValueRestriction.Unrestricted,
        _ => null
    };

    private static Guid ScopeOwnerId(ScopeNodeBase scope) => scope switch
    {
        Planet planet => planet.Id,
        Country country => country.Id,
        Area area => area.Id,
        Room room => room.Id,
        _ => Guid.Empty
    };

    private static string? ScalarText(object value) => value switch
    {
        JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
        JsonElement { ValueKind: JsonValueKind.Number } json => json.GetRawText(),
        JsonElement { ValueKind: JsonValueKind.True } => "true",
        JsonElement { ValueKind: JsonValueKind.False } => "false",
        JsonElement => null,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    private ParticleEffectRecipeOption? FindRecipe(string? effectKey) => _catalog.GetRecipes().FirstOrDefault(recipe => string.Equals(recipe.EffectKey, effectKey?.Trim(), StringComparison.OrdinalIgnoreCase));
    private ValidationIssue Issue(string path, string message) => new(Metadata.RuleId, Metadata.DefaultSeverity, path, message);
    private static bool IsHexColor(string? value) => value is { Length: 7 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

}
