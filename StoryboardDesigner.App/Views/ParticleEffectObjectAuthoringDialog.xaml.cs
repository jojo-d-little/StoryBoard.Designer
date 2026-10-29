using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using Storyboard.Shared.DesignerContracts;

namespace StoryboardDesigner.App.Views;

public partial class ParticleEffectObjectAuthoringDialog : Window
{
    private readonly ParticleEffectAuthoringCatalogService _catalog = new();
    private readonly Dictionary<string, System.Windows.Controls.TextBox> _controlInputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProjectGamePropertySourceReferenceDto?> _bindings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, System.Windows.Controls.Button> _bindingButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly GamePropertyPickerContext? _variablePickerContext;
    private readonly Func<string, string, GamePropertyDefinition?>? _newComputedVariableFactory;
    private ParticleEffectAttachment? _initialAttachment;
    private bool _isInitializing;

    public ParticleEffectAttachment? ParticleEffect { get; private set; }
    public IReadOnlyList<GamePropertyDefinition> CreatedVariables { get; private set; } = Array.Empty<GamePropertyDefinition>();

    public ParticleEffectObjectAuthoringDialog(
        ParticleEffectAttachment? attachment,
        GamePropertyPickerContext? variablePickerContext = null,
        Func<string, string, GamePropertyDefinition?>? newComputedVariableFactory = null)
    {
        InitializeComponent();
        _variablePickerContext = variablePickerContext;
        _newComputedVariableFactory = newComputedVariableFactory;
        _initialAttachment = CloneAttachment(attachment);
        _isInitializing = true;
        RecipeComboBox.ItemsSource = _catalog.GetRecipes(ParticleEffectAuthoringContext.ObjectAttachment);
        HasEffectCheckBox.IsChecked = attachment is not null;
        RecipeComboBox.SelectedItem = _catalog.GetRecipes(ParticleEffectAuthoringContext.ObjectAttachment)
            .FirstOrDefault(recipe => string.Equals(recipe.EffectKey, attachment?.EffectKey, StringComparison.OrdinalIgnoreCase));
        _isInitializing = false;
        RefreshControlInputs();
        RefreshEnabledState();
    }

    private void RecipeComboBox_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitializing) RefreshControlInputs();
    }

    private void HasEffect_OnChanged(object sender, RoutedEventArgs e) => RefreshEnabledState();

    private void RefreshControlInputs()
    {
        ControlsPanel.Children.Clear();
        _controlInputs.Clear();
        _bindings.Clear();
        _bindingButtons.Clear();
        if (RecipeComboBox.SelectedItem is not ParticleEffectRecipeOption recipe) return;
        var existing = _initialAttachment?.Settings ?? new List<ParticleEffectControlSettingDto>();
        foreach (var control in _catalog.GetControls(recipe.EffectKey))
        {
            if (string.Equals(control.Kind, "colorPair", StringComparison.OrdinalIgnoreCase))
            {
                var primary = existing.FirstOrDefault(setting => string.Equals(setting.ControlKey, $"{control.Name}.Primary", StringComparison.OrdinalIgnoreCase));
                var secondary = existing.FirstOrDefault(setting => string.Equals(setting.ControlKey, $"{control.Name}.Secondary", StringComparison.OrdinalIgnoreCase));
                AddControlRow(control, $"{control.Name}.Primary", ParticleEffectAuthoringHelpText.GetColorRoleDisplayLabel(control, primary: true),
                    primary, primary?.Value?.ToString() ?? string.Empty);
                AddControlRow(control, $"{control.Name}.Secondary", ParticleEffectAuthoringHelpText.GetColorRoleDisplayLabel(control, primary: false),
                    secondary, secondary?.Value?.ToString() ?? string.Empty);
            }
            else
            {
                var prior = existing.FirstOrDefault(setting => string.Equals(setting.ControlKey, control.Name, StringComparison.OrdinalIgnoreCase));
                AddControlRow(control, control.Name, ParticleEffectAuthoringHelpText.GetDisplayLabel(control),
                    prior, prior?.Value?.ToString() ?? GetInitialControlValue(control));
            }
        }
    }

    private void AddControlRow(ParticleEffectControlMetadata control, string propertyName, string labelText,
        ParticleEffectControlSettingDto? prior, string initialValue)
    {
        var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var isColor = string.Equals(control.Kind, "color", StringComparison.OrdinalIgnoreCase)
            || string.Equals(control.Kind, "colorPair", StringComparison.OrdinalIgnoreCase);
        var isColorRole = propertyName.EndsWith(".Primary", StringComparison.OrdinalIgnoreCase)
            || propertyName.EndsWith(".Secondary", StringComparison.OrdinalIgnoreCase);
        var tooltip = ParticleEffectAuthoringHelpText.GetControlTooltip(control, isColorRole ? labelText : null, isColor);
        var label = new TextBlock { Text = labelText, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, ToolTip = tooltip };
        var input = new System.Windows.Controls.TextBox
        {
            Text = initialValue,
            ToolTip = tooltip
        };
        Grid.SetColumn(label, 0); Grid.SetColumn(input, 1);
        row.Children.Add(label); row.Children.Add(input);
        var nextColumn = 2;
        if (isColor)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var pick = new System.Windows.Controls.Button
            {
                Content = "Pick...",
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(6, 0, 0, 0),
                Tag = propertyName
            };
            pick.Click += PickColor_OnClick;
            Grid.SetColumn(pick, nextColumn++);
            row.Children.Add(pick);
        }
        var bind = new System.Windows.Controls.Button { Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(6, 0, 0, 0), IsEnabled = _variablePickerContext is not null, Tag = propertyName, MaxWidth = 155 };
        SetBindingButton(bind, prior?.Binding);
        bind.Click += BindControl_OnClick;
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(bind, nextColumn++); row.Children.Add(bind);
        ControlsPanel.Children.Add(row);
        _controlInputs[propertyName] = input;
        _bindings[propertyName] = ParticleEffectControlSettingCloner.CloneReference(prior?.Binding);
        var newComputed = new System.Windows.Controls.Button
        {
            Content = "New computed variable",
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(6, 0, 0, 0),
            IsEnabled = _newComputedVariableFactory is not null,
            Tag = propertyName,
            ToolTip = "Create a computed property on this object and bind this control to it."
        };
        newComputed.Click += NewComputedVariable_OnClick;
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(newComputed, nextColumn++); row.Children.Add(newComputed);
        var reset = new System.Windows.Controls.Button
        {
            Content = "Default",
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(6, 0, 0, 0),
            Tag = propertyName,
            ToolTip = "Clear this control's literal and binding, then use the recipe baseline."
        };
        reset.Click += ResetControl_OnClick;
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(reset, nextColumn); row.Children.Add(reset);
        _bindingButtons[propertyName] = bind;
    }

    private void RefreshEnabledState()
    {
        var enabled = HasEffectCheckBox.IsChecked == true;
        RecipeComboBox.IsEnabled = enabled;
        ControlsPanel.IsEnabled = enabled;
    }

    private void SetBindingButton(System.Windows.Controls.Button button, ProjectGamePropertySourceReferenceDto? source)
    {
        if (source is null)
        {
            button.Content = "Choose variable…";
            button.ToolTip = "Choose a variable to override the recipe default.";
            return;
        }

        var display = !string.IsNullOrWhiteSpace(source.AnchorKey)
                ? $"{source.AnchorKey}.{source.PropertyName}"
                : source.OwnerId.HasValue
                    ? _variablePickerContext?.DiscoverChoices(true).FirstOrDefault(choice => choice.OwnerId == source.OwnerId
                        && string.Equals(choice.PropertyName, source.PropertyName, StringComparison.OrdinalIgnoreCase)) is { } choice
                        ? $"{choice.ScopePath} → {choice.OwnerVariable}" : source.PropertyName
                    : source.PropertyName;
        button.Content = display;
        button.ToolTip = $"Bound to {display}. Click to change the variable.";
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (HasEffectCheckBox.IsChecked == true)
        {
            if (RecipeComboBox.SelectedItem is not ParticleEffectRecipeOption recipe)
            {
                System.Windows.MessageBox.Show(this, "Choose a particle effect from the catalog.", "Particle Effect", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var properties = new List<ParticleEffectControlSettingDto>();
            var metadata = _catalog.GetControls(recipe.EffectKey).ToDictionary(control => control.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in _controlInputs)
            {
                var roleSpecific = pair.Key.EndsWith(".Primary", StringComparison.OrdinalIgnoreCase)
                    || pair.Key.EndsWith(".Secondary", StringComparison.OrdinalIgnoreCase);
                var baseName = roleSpecific ? pair.Key[..pair.Key.LastIndexOf('.')] : pair.Key;
                if (!metadata.TryGetValue(baseName, out var control)) continue;
                var isNumeric = string.Equals(control.Kind, "number", StringComparison.OrdinalIgnoreCase);
                var binding = _bindings.GetValueOrDefault(pair.Key);
                var valueText = pair.Value.Text.Trim();
                if (string.IsNullOrWhiteSpace(valueText) && binding is null)
                {
                    continue;
                }
                if (isNumeric && !string.IsNullOrWhiteSpace(valueText))
                {
                    if (!double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var numberValue)
                        || !double.IsFinite(numberValue))
                    {
                        System.Windows.MessageBox.Show(this, $"'{control.Label}' must be a finite number.", "Particle Effect", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    if ((control.Minimum.HasValue && numberValue < control.Minimum.Value)
                        || (control.Maximum.HasValue && numberValue > control.Maximum.Value))
                    {
                        System.Windows.MessageBox.Show(this, $"'{control.Label}' must be between {control.Minimum?.ToString(CultureInfo.InvariantCulture) ?? "-infinity"} and {control.Maximum?.ToString(CultureInfo.InvariantCulture) ?? "infinity"} {control.Unit}.", "Particle Effect", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
                var valueKind = roleSpecific ? "color" : control.Kind;
                if (!string.IsNullOrWhiteSpace(valueText) && !IsValidTypedValue(valueKind, valueText))
                {
                    var expected = string.Equals(valueKind, "color", StringComparison.OrdinalIgnoreCase)
                            ? "an sRGB hex color in #RRGGBB form"
                            : null;
                    if (expected is not null)
                    {
                        System.Windows.MessageBox.Show(this, $"'{control.Label}' must be {expected}.", "Particle Effect", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
                properties.Add(new ParticleEffectControlSettingDto
                {
                    ControlKey = pair.Key,
                    Value = string.IsNullOrWhiteSpace(valueText) ? null : ParseLiteralValue(valueKind, valueText),
                    Binding = ParticleEffectControlSettingCloner.CloneReference(binding)
                });
            }
            ParticleEffect = new ParticleEffectAttachment { EffectKey = recipe.EffectKey, OffsetXPx = _initialAttachment?.OffsetXPx, OffsetYPx = _initialAttachment?.OffsetYPx, Settings = properties };
        }
        else ParticleEffect = null;

        CreatedVariables = _createdVariables.ToList();

        DialogResult = true;
    }

    private void BindControl_OnClick(object sender, RoutedEventArgs e)
    {
        if (_variablePickerContext is null || sender is not System.Windows.Controls.Button { Tag: string name }) return;
        var current = _bindings.GetValueOrDefault(name);
        var picker = new VariableChooserDialog(
            _variablePickerContext with { SelectedReference = current },
            "Choose Effect Control Variable") { Owner = this };
        if (picker.ShowDialog() != true || picker.SelectedReference is not { } selected) return;
        _bindings[name] = ParticleEffectControlSettingCloner.CloneReference(selected);
        if (_bindingButtons.TryGetValue(name, out var button)) SetBindingButton(button, selected);
    }

    private void PickColor_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string name }
            || !_controlInputs.TryGetValue(name, out var input)) return;
        var picker = new ColorPickerDialog(input.Text) { Owner = this };
        if (picker.ShowDialog() == true)
        {
            input.Text = picker.SelectedColor ?? string.Empty;
        }
    }

    private void ResetControl_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string name } || !_controlInputs.TryGetValue(name, out var input)) return;
        input.Text = string.Empty;
        var source = _bindings.GetValueOrDefault(name);
        if (source is not null && _variablePickerContext?.RelativeOwnerId is Guid ownerId && source.OwnerId == ownerId)
        {
            _createdVariables.RemoveAll(variable => string.Equals(variable.Name, source.PropertyName, StringComparison.OrdinalIgnoreCase));
        }
        _bindings[name] = null;
        if (_bindingButtons.TryGetValue(name, out var button)) SetBindingButton(button, null);
    }

    private readonly List<GamePropertyDefinition> _createdVariables = new();

    private void NewComputedVariable_OnClick(object sender, RoutedEventArgs e)
    {
        if (_newComputedVariableFactory is null || sender is not FrameworkElement { Tag: string name }) return;
        var variable = _newComputedVariableFactory(BuildSuggestedVariableName(name), GetResultKind(name));
        if (variable?.Computation is null || _variablePickerContext?.RelativeOwnerId is not Guid ownerId) return;
        _createdVariables.Add(variable);
        var source = new ProjectGamePropertySourceReferenceDto { OwnerId = ownerId, PropertyName = variable.Name };
        _bindings[name] = source;
        if (_bindingButtons.TryGetValue(name, out var button)) SetBindingButton(button, source);
    }

    private string GetResultKind(string propertyName)
    {
        var roleSpecific = propertyName.EndsWith(".Primary", StringComparison.OrdinalIgnoreCase)
            || propertyName.EndsWith(".Secondary", StringComparison.OrdinalIgnoreCase);
        var baseName = roleSpecific ? propertyName[..propertyName.LastIndexOf('.')] : propertyName;
        var control = RecipeComboBox.SelectedItem is ParticleEffectRecipeOption recipe
            ? _catalog.GetControls(recipe.EffectKey).FirstOrDefault(item => string.Equals(item.Name, baseName, StringComparison.OrdinalIgnoreCase))
            : null;
        return roleSpecific || string.Equals(control?.Kind, "color", StringComparison.OrdinalIgnoreCase)
            || string.Equals(control?.Kind, "colorPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(control?.Kind, "enum", StringComparison.OrdinalIgnoreCase) ? "String"
            : string.Equals(control?.Kind, "boolean", StringComparison.OrdinalIgnoreCase) ? "Boolean" : "Numeric";
    }

    private static string BuildSuggestedVariableName(string controlPropertyName)
    {
        var suffix = new string(controlPropertyName
            .Where(char.IsLetterOrDigit)
            .ToArray());
        if (string.IsNullOrWhiteSpace(suffix)) suffix = "Value";
        return "particleEffect" + char.ToUpperInvariant(suffix[0]) + suffix[1..];
    }

    private static bool TryOptionalNumber(string text, out double? value)
    {
        if (string.IsNullOrWhiteSpace(text)) { value = null; return true; }
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed))
        { value = parsed; return true; }
        value = null; return false;
    }

    private static string Format(double? value) => value?.ToString("0.################", CultureInfo.InvariantCulture) ?? string.Empty;

    private static bool IsValidTypedValue(string kind, string text) => kind.ToLowerInvariant() switch
    {
        "color" => IsHexColor(text),
        "colorpair" => text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) is { Length: 2 } colors
            && colors.All(IsHexColor),
        _ => true
    };

    private static bool IsHexColor(string value) => value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

    private static object ParseLiteralValue(string kind, string value) => kind.ToLowerInvariant() switch
    {
        "number" => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
        "boolean" => bool.Parse(value),
        _ => value
    };

    private static string GetInitialControlValue(ParticleEffectControlMetadata control) => string.Empty;

    private static ParticleEffectAttachment? CloneAttachment(ParticleEffectAttachment? source) => source is null ? null : new ParticleEffectAttachment
    {
        EffectKey = source.EffectKey, OffsetXPx = source.OffsetXPx, OffsetYPx = source.OffsetYPx,
        Settings = ParticleEffectControlSettingCloner.CloneSettings(source.Settings)
    };

}
