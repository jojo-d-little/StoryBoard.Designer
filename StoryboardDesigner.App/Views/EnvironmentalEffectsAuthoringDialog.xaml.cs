using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using Storyboard.Shared.DesignerContracts;
using EnvironmentalEffectApplicability = StoryboardDesigner.App.Models.EnvironmentalEffectApplicability;

namespace StoryboardDesigner.App.Views;

public partial class EnvironmentalEffectsAuthoringDialog : Window
{
    private readonly ParticleEffectAuthoringCatalogService _catalog = new();
    private readonly ObservableCollection<EnvironmentalEffectDefinition> _effects;
    private readonly Dictionary<string, System.Windows.Controls.TextBox> _inputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProjectGamePropertySourceReferenceDto?> _bindings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, System.Windows.Controls.Button> _bindingButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly GamePropertyPickerContext? _variablePickerContext;
    private ProjectGamePropertySourceReferenceDto? _enabledBinding;
    private bool _loading;
    private Guid? _editingId;

    public List<EnvironmentalEffectDefinition> Effects => _effects.Select(Clone).ToList();

    public EnvironmentalEffectsAuthoringDialog(IEnumerable<EnvironmentalEffectDefinition>? effects,
        GamePropertyPickerContext? variablePickerContext = null,
        Guid? selectedEffectId = null)
    {
        InitializeComponent();
        _variablePickerContext = variablePickerContext;
        BindEnabledButton.IsEnabled = variablePickerContext is not null;
        _effects = new ObservableCollection<EnvironmentalEffectDefinition>((effects ?? Array.Empty<EnvironmentalEffectDefinition>()).Select(Clone));
        EffectsListBox.ItemsSource = _effects;
        RecipeComboBox.ItemsSource = _catalog.GetRecipes(ParticleEffectAuthoringContext.Environmental);
        if (_effects.Count > 0)
        {
            var selectedIndex = selectedEffectId.HasValue
                ? _effects.ToList().FindIndex(effect => effect.Id == selectedEffectId.Value)
                : -1;
            EffectsListBox.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        }
        else ClearEditor();
    }

    private void EffectsListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var requested = EffectsListBox.SelectedItem as EnvironmentalEffectDefinition;
        var currentlyEdited = _effects.FirstOrDefault(effect => effect.Id == _editingId);
        var hasPendingDefinition = _editingId.HasValue
            || !string.IsNullOrWhiteSpace(NameTextBox.Text)
            || RecipeComboBox.SelectedItem is ParticleEffectRecipeOption;
        if (hasPendingDefinition && currentlyEdited?.Id != requested?.Id && !TryCommitCurrentEffect())
        {
            _loading = true;
            EffectsListBox.SelectedItem = currentlyEdited;
            _loading = false;
            return;
        }
        if (requested is not null) LoadEditor(requested);
    }

    private void RecipeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) BuildControls(RecipeComboBox.SelectedItem is ParticleEffectRecipeOption recipe ? recipe.EffectKey : null, null);
    }

    private void Add_OnClick(object sender, RoutedEventArgs e)
    {
        _loading = true;
        EffectsListBox.SelectedItem = null;
        _editingId = null;
        NameTextBox.Text = "";
        RecipeComboBox.SelectedIndex = -1;
        ApplicabilityComboBox.SelectedIndex = 0;
        EnabledCheckBox.IsChecked = true;
        RenderOrderTextBox.Text = "0";
        ControlsPanel.Children.Clear();
        _inputs.Clear();
        _bindings.Clear();
        _bindingButtons.Clear();
        _enabledBinding = null;
        SetBindingButton(BindEnabledButton, null);
        _loading = false;
        NameTextBox.Focus();
    }

    private void Remove_OnClick(object sender, RoutedEventArgs e)
    {
        if (EffectsListBox.SelectedItem is not EnvironmentalEffectDefinition selected) return;
        var index = EffectsListBox.SelectedIndex;
        _loading = true;
        _effects.Remove(selected);
        EffectsListBox.SelectedItem = null;
        _editingId = null;
        NameTextBox.Text = string.Empty;
        RecipeComboBox.SelectedIndex = -1;
        ControlsPanel.Children.Clear();
        _inputs.Clear();
        _bindings.Clear();
        _bindingButtons.Clear();
        _enabledBinding = null;
        SetBindingButton(BindEnabledButton, null);
        _loading = false;
        if (_effects.Count == 0) Add_OnClick(sender, e);
        else EffectsListBox.SelectedIndex = Math.Min(index, _effects.Count - 1);
    }

    private bool TryCommitCurrentEffect()
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text) || RecipeComboBox.SelectedItem is not ParticleEffectRecipeOption recipe)
        {
            System.Windows.MessageBox.Show(this, "Enter an effect name and choose an environmental recipe.", "Environmental Effects", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        if (!int.TryParse(RenderOrderTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var order))
        {
            System.Windows.MessageBox.Show(this, "Render order must be a whole number.", "Environmental Effects", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        var properties = new List<ParticleEffectControlSettingDto>();
        var metadata = _catalog.GetControls(recipe.EffectKey).ToDictionary(control => control.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _inputs)
        {
            var roleSpecific = pair.Key.EndsWith(".Primary", StringComparison.OrdinalIgnoreCase)
                || pair.Key.EndsWith(".Secondary", StringComparison.OrdinalIgnoreCase);
            var baseName = roleSpecific ? pair.Key[..pair.Key.LastIndexOf('.')] : pair.Key;
            if (!metadata.TryGetValue(baseName, out var control)) continue;
            var isNumber = string.Equals(control.Kind, "number", StringComparison.OrdinalIgnoreCase);
            var binding = _bindings.GetValueOrDefault(pair.Key);
            var valueText = pair.Value.Text.Trim();
            if (string.IsNullOrWhiteSpace(valueText) && binding is null)
            {
                continue;
            }
            if (!string.IsNullOrWhiteSpace(valueText) && isNumber && (!double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)))
            {
                System.Windows.MessageBox.Show(this, $"'{control.Label}' must be a finite number.", "Environmental Effects", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            if (isNumber
                && double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericValue)
                && ((control.Minimum.HasValue && numericValue < control.Minimum.Value)
                    || (control.Maximum.HasValue && numericValue > control.Maximum.Value)))
            {
                System.Windows.MessageBox.Show(this, $"'{control.Label}' must be between {control.Minimum?.ToString(CultureInfo.InvariantCulture) ?? "-infinity"} and {control.Maximum?.ToString(CultureInfo.InvariantCulture) ?? "infinity"} {control.Unit}.", "Environmental Effects", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            var valueKind = roleSpecific ? "color" : control.Kind;
            if (!string.IsNullOrWhiteSpace(valueText) && !IsValidTypedValue(valueKind, valueText))
            {
                var expected = string.Equals(valueKind, "color", StringComparison.OrdinalIgnoreCase)
                        ? "an sRGB hex color in #RRGGBB form"
                        : null;
                if (expected is not null)
                {
                    System.Windows.MessageBox.Show(this, $"'{control.Label}' must be {expected}.", "Environmental Effects", MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }
            }
            properties.Add(new ParticleEffectControlSettingDto
            {
                ControlKey = pair.Key,
                Value = string.IsNullOrWhiteSpace(valueText) ? null : ParseLiteralValue(valueKind, valueText),
                Binding = ParticleEffectControlSettingCloner.CloneReference(binding)
            });
        }

        var existing = _effects.FirstOrDefault(effect => effect.Id == _editingId);
        var definition = new EnvironmentalEffectDefinition
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            Name = NameTextBox.Text.Trim(),
            EffectKey = recipe.EffectKey,
            Applicability = (ApplicabilityComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
            {
                "Indoor" => EnvironmentalEffectApplicability.Indoor,
                "Both" => EnvironmentalEffectApplicability.Both,
                _ => EnvironmentalEffectApplicability.Outdoor
            },
            Enabled = EnabledCheckBox.IsChecked == true,
            EnabledBinding = ParticleEffectControlSettingCloner.CloneReference(_enabledBinding),
            RenderOrder = order,
            Settings = properties
        };
        _loading = true;
        if (existing is null) _effects.Add(definition);
        else
        {
            var position = _effects.IndexOf(existing);
            _effects[position] = definition;
        }
        _editingId = definition.Id;
        _loading = false;
        return true;
    }

    private void Done_OnClick(object sender, RoutedEventArgs e)
    {
        var hasPendingDefinition = _editingId.HasValue
            || !string.IsNullOrWhiteSpace(NameTextBox.Text)
            || RecipeComboBox.SelectedItem is ParticleEffectRecipeOption;
        if (hasPendingDefinition && !TryCommitCurrentEffect()) return;
        DialogResult = true;
    }

    private void LoadEditor(EnvironmentalEffectDefinition effect)
    {
        _loading = true;
        _editingId = effect.Id;
        NameTextBox.Text = effect.Name;
        RecipeComboBox.SelectedItem = _catalog.GetRecipes(ParticleEffectAuthoringContext.Environmental)
            .FirstOrDefault(recipe => string.Equals(recipe.EffectKey, effect.EffectKey, StringComparison.OrdinalIgnoreCase));
        ApplicabilityComboBox.SelectedIndex = effect.Applicability switch
        {
            EnvironmentalEffectApplicability.Indoor => 1,
            EnvironmentalEffectApplicability.Both => 2,
            _ => 0
        };
        EnabledCheckBox.IsChecked = effect.Enabled;
        _enabledBinding = ParticleEffectControlSettingCloner.CloneReference(effect.EnabledBinding);
        SetBindingButton(BindEnabledButton, _enabledBinding);
        RenderOrderTextBox.Text = effect.RenderOrder.ToString(CultureInfo.InvariantCulture);
        _loading = false;
        BuildControls(effect.EffectKey, effect.Settings);
    }

    private void BuildControls(string? effectKey, IEnumerable<ParticleEffectControlSettingDto>? currentValues)
    {
        ControlsPanel.Children.Clear();
        _inputs.Clear();
        _bindings.Clear();
        _bindingButtons.Clear();
        if (string.IsNullOrWhiteSpace(effectKey)) return;
        var current = (currentValues ?? Array.Empty<ParticleEffectControlSettingDto>()).ToDictionary(setting => setting.ControlKey, StringComparer.OrdinalIgnoreCase);
        foreach (var control in _catalog.GetControls(effectKey))
        {
            if (string.Equals(control.Kind, "colorPair", StringComparison.OrdinalIgnoreCase))
            {
                var primary = current.GetValueOrDefault($"{control.Name}.Primary");
                var secondary = current.GetValueOrDefault($"{control.Name}.Secondary");
                AddControlRow(control, $"{control.Name}.Primary", control.PrimaryRoleLabel ?? "Primary color", primary);
                AddControlRow(control, $"{control.Name}.Secondary", control.SecondaryRoleLabel ?? "Secondary color", secondary);
            }
            else
            {
                var property = current.GetValueOrDefault(control.Name);
                AddControlRow(control, control.Name, string.IsNullOrWhiteSpace(control.Unit) ? control.Label : $"{control.Label} ({control.Unit})", property);
            }
        }
    }

    private void AddControlRow(ParticleEffectControlMetadata control, string propertyName, string labelText, ParticleEffectControlSettingDto? setting)
    {
        var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = new TextBlock { Text = labelText, VerticalAlignment = VerticalAlignment.Center, ToolTip = control.Description };
        var input = new System.Windows.Controls.TextBox
        {
            Text = setting?.Value?.ToString() ?? string.Empty,
            ToolTip = string.Equals(control.Kind, "color", StringComparison.OrdinalIgnoreCase) || string.Equals(control.Kind, "colorPair", StringComparison.OrdinalIgnoreCase)
                ? "Enter an sRGB hex color such as #AABBCC." : control.Description
        };
        input.ToolTip = $"{input.ToolTip} Leave blank to use the recipe default.";
        var bind = new System.Windows.Controls.Button { Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(6, 0, 0, 0), IsEnabled = _variablePickerContext is not null, Tag = propertyName, MaxWidth = 155 };
        bind.Click += BindControl_OnClick;
        SetBindingButton(bind, setting?.Binding);
        var nextColumn = 2;
        if (string.Equals(control.Kind, "color", StringComparison.OrdinalIgnoreCase)
            || string.Equals(control.Kind, "colorPair", StringComparison.OrdinalIgnoreCase))
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var pick = new System.Windows.Controls.Button { Content = "Pick...", Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(6, 0, 0, 0), Tag = propertyName };
            pick.Click += PickColor_OnClick;
            Grid.SetColumn(pick, nextColumn++);
            row.Children.Add(pick);
        }
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(bind, nextColumn++);
        var clearBinding = new System.Windows.Controls.Button { Content = "Unbind", Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(6, 0, 0, 0), Tag = propertyName, ToolTip = "Remove the variable binding and keep the literal fallback." };
        clearBinding.Click += ClearBinding_OnClick;
        Grid.SetColumn(clearBinding, nextColumn++);
        var reset = new System.Windows.Controls.Button { Content = "Default", Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(6, 0, 0, 0), Tag = propertyName, ToolTip = "Clear this override and use the recipe default." };
        reset.Click += ResetControl_OnClick;
        Grid.SetColumn(reset, nextColumn);
        Grid.SetColumn(label, 0); Grid.SetColumn(input, 1);
        row.Children.Add(label); row.Children.Add(input); row.Children.Add(bind); row.Children.Add(clearBinding); row.Children.Add(reset); ControlsPanel.Children.Add(row);
        _inputs[propertyName] = input;
        _bindings[propertyName] = ParticleEffectControlSettingCloner.CloneReference(setting?.Binding);
        _bindingButtons[propertyName] = bind;
    }

    private void BindEnabled_OnClick(object sender, RoutedEventArgs e)
    {
        _enabledBinding = ChooseVariable(_enabledBinding, booleanOnly: true);
        SetBindingButton(BindEnabledButton, _enabledBinding);
    }

    private void ClearEnabledBinding_OnClick(object sender, RoutedEventArgs e)
    {
        _enabledBinding = null;
        SetBindingButton(BindEnabledButton, null);
    }

    private void BindControl_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string name }) return;
        var selected = ChooseVariable(_bindings.GetValueOrDefault(name));
        _bindings[name] = ParticleEffectControlSettingCloner.CloneReference(selected);
        if (_bindingButtons.TryGetValue(name, out var button)) SetBindingButton(button, selected);
    }

    private void PickColor_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string name } || !_inputs.TryGetValue(name, out var input)) return;
        var picker = new ColorPickerDialog(input.Text) { Owner = this };
        if (picker.ShowDialog() == true)
        {
            input.Text = picker.SelectedColor ?? string.Empty;
        }
    }

    private void ResetControl_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string name } || !_inputs.TryGetValue(name, out var input)) return;
        input.Text = string.Empty;
        _bindings[name] = null;
        if (_bindingButtons.TryGetValue(name, out var button)) SetBindingButton(button, null);
    }

    private void ClearBinding_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string name }) return;
        _bindings[name] = null;
        if (_bindingButtons.TryGetValue(name, out var button)) SetBindingButton(button, null);
    }

    private ProjectGamePropertySourceReferenceDto? ChooseVariable(ProjectGamePropertySourceReferenceDto? current, bool booleanOnly = false)
    {
        if (_variablePickerContext is null) return current;
        var picker = new VariableChooserDialog(_variablePickerContext with
        {
            BooleanOnly = booleanOnly,
            AllowReferenceModes = true,
            SelectedReference = current
        }, "Choose Effect Variable") { Owner = this };
        return picker.ShowDialog() == true ? picker.SelectedReference : current;
    }

    private void SetBindingButton(System.Windows.Controls.Button button, ProjectGamePropertySourceReferenceDto? source)
    {
        if (source is null)
        {
            button.Content = "Choose variable...";
            button.ToolTip = "Choose a variable to override the recipe value.";
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

    private void ClearEditor()
    {
        _editingId = null;
        NameTextBox.Text = "";
        RecipeComboBox.SelectedIndex = -1;
        ControlsPanel.Children.Clear();
    }

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

    private static EnvironmentalEffectDefinition Clone(EnvironmentalEffectDefinition source) => new()
    {
        Id = source.Id, Name = source.Name, EffectKey = source.EffectKey, Applicability = source.Applicability,
        RenderOrder = source.RenderOrder, Enabled = source.Enabled,
        EnabledBinding = ParticleEffectControlSettingCloner.CloneReference(source.EnabledBinding),
        Settings = ParticleEffectControlSettingCloner.CloneSettings(source.Settings)
    };
}
