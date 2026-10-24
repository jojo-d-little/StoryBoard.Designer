using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class ParticleEffectImageChoicesDialog : Window
{
    private sealed record ChoiceEntry(ParticleEffectImageChoice Value)
    {
        public string DisplayName => $"{Value.EffectKey} / {Value.ImageSlot}";
    }

    private readonly ParticleEffectAuthoringCatalogService _catalog = new();
    private readonly ObservableCollection<ChoiceEntry> _choices;
    private bool _loading;

    public List<ParticleEffectImageChoice> Choices => _choices.Select(entry => Clone(entry.Value)).ToList();

    public ParticleEffectImageChoicesDialog(IEnumerable<ParticleEffectImageChoice>? choices)
    {
        InitializeComponent();
        _choices = new ObservableCollection<ChoiceEntry>((choices ?? Array.Empty<ParticleEffectImageChoice>())
            .Select(choice => new ChoiceEntry(Clone(choice))));
        ChoicesListBox.ItemsSource = _choices;
        RecipeComboBox.ItemsSource = _catalog.GetRecipes();
        if (_choices.Count > 0) ChoicesListBox.SelectedIndex = 0;
        else ClearEditor();
    }

    private void Add_OnClick(object sender, RoutedEventArgs e)
    {
        _loading = true;
        ChoicesListBox.SelectedItem = null;
        RecipeComboBox.SelectedIndex = -1;
        ImageSlotComboBox.ItemsSource = Array.Empty<string>();
        AssetPathTextBox.Text = string.Empty;
        _loading = false;
    }

    private void Remove_OnClick(object sender, RoutedEventArgs e)
    {
        if (ChoicesListBox.SelectedItem is not ChoiceEntry selected) return;
        var index = ChoicesListBox.SelectedIndex;
        _choices.Remove(selected);
        if (_choices.Count == 0) Add_OnClick(sender, e);
        else ChoicesListBox.SelectedIndex = Math.Min(index, _choices.Count - 1);
    }

    private void ChoicesListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ChoicesListBox.SelectedItem is not ChoiceEntry entry) return;
        _loading = true;
        RecipeComboBox.SelectedItem = _catalog.GetRecipes().FirstOrDefault(recipe =>
            string.Equals(recipe.EffectKey, entry.Value.EffectKey, StringComparison.OrdinalIgnoreCase));
        AssetPathTextBox.Text = entry.Value.AssetPath;
        UpdateImageSlots(entry.Value.ImageSlot);
        _loading = false;
    }

    private void RecipeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) UpdateImageSlots(null);
    }

    private void UpdateImageSlots(string? selectedSlot)
    {
        var effectKey = (RecipeComboBox.SelectedItem as ParticleEffectRecipeOption)?.EffectKey;
        var slots = _catalog.GetImageSlots(effectKey);
        ImageSlotComboBox.ItemsSource = slots;
        ImageSlotComboBox.SelectedItem = slots.FirstOrDefault(slot => string.Equals(slot, selectedSlot, StringComparison.OrdinalIgnoreCase))
            ?? slots.FirstOrDefault();
    }

    private void Browse_OnClick(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Particle images (*.png;*.webp)|*.png;*.webp|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) == true) AssetPathTextBox.Text = picker.FileName;
    }

    private void SaveChoice_OnClick(object sender, RoutedEventArgs e)
    {
        if (RecipeComboBox.SelectedItem is not ParticleEffectRecipeOption recipe
            || ImageSlotComboBox.SelectedItem is not string slot
            || string.IsNullOrWhiteSpace(AssetPathTextBox.Text))
        {
            System.Windows.MessageBox.Show(this, "Choose an effect, a declared image slot, and an image file.", "Particle Effect Artwork", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var extension = Path.GetExtension(AssetPathTextBox.Text.Trim());
        if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase))
        {
            System.Windows.MessageBox.Show(this, "Particle effect image overrides must be PNG or WebP files.", "Particle Effect Artwork", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var saved = new ParticleEffectImageChoice { EffectKey = recipe.EffectKey, ImageSlot = slot, AssetPath = AssetPathTextBox.Text.Trim() };
        var existing = _choices.FirstOrDefault(entry => string.Equals(entry.Value.EffectKey, saved.EffectKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.Value.ImageSlot, saved.ImageSlot, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) _choices[_choices.IndexOf(existing)] = new ChoiceEntry(saved);
        else _choices.Add(new ChoiceEntry(saved));
        ChoicesListBox.SelectedItem = _choices.FirstOrDefault(entry => ReferenceEquals(entry.Value, saved));
    }

    private void Done_OnClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void ClearEditor()
    {
        RecipeComboBox.SelectedIndex = -1;
        ImageSlotComboBox.ItemsSource = Array.Empty<string>();
        AssetPathTextBox.Text = string.Empty;
    }

    private static ParticleEffectImageChoice Clone(ParticleEffectImageChoice choice) => new()
    {
        EffectKey = choice.EffectKey, ImageSlot = choice.ImageSlot, AssetPath = choice.AssetPath
    };
}
