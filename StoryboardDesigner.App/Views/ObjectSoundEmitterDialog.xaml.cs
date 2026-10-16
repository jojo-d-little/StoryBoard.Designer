using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace StoryboardDesigner.App.Views;

public partial class ObjectSoundEmitterDialog : Window
{
    private readonly ObservableCollection<ObjectSoundEmitter> _emitters;
    private readonly GamePropertyPickerContext _activationPickerContext;

    public ObjectSoundEmitterDialog(
        IReadOnlyList<ObjectSoundEmitter> emitters,
        IReadOnlyList<SoundEffectChoiceItem> soundChoices,
        GamePropertyPickerContext activationPickerContext)
    {
        InitializeComponent();
        SoundChoices = soundChoices
            .Where(static choice => choice.SoundEffectId != Guid.Empty)
            .GroupBy(static choice => choice.SoundEffectId)
            .Select(static group => group.First())
            .OrderBy(static choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _activationPickerContext = activationPickerContext;
        ActivationPropertyChoices = _activationPickerContext.DiscoverChoices(true);

        SoundDefinitionColumn.ItemsSource = SoundChoices;
        _emitters = new ObservableCollection<ObjectSoundEmitter>(emitters.Select(Clone));
        foreach (var emitter in _emitters)
        {
            emitter.ActivationGamePropertyDisplayName = ResolveActivationDisplayName(emitter);
        }
        EmittersDataGrid.ItemsSource = _emitters;
    }

    public IReadOnlyList<SoundEffectChoiceItem> SoundChoices { get; }
    public IReadOnlyList<GamePropertyChoiceItem> ActivationPropertyChoices { get; }
    public IReadOnlyList<ObjectSoundEmitter> Emitters => _emitters.Select(Clone).ToList();

    private void ChooseActivationProperty_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ObjectSoundEmitter emitter)
        {
            return;
        }

        CommitEmitterEdit();

        var pickerContext = _activationPickerContext with
        {
            SelectedReference = emitter.ActivationGamePropertyReference ?? CreateLegacySelfReference(emitter.ActivationGamePropertyName)
        };
        var dialog = new VariableChooserDialog(
            pickerContext,
            "Choose Sound Emitter Activation Property",
            emitter.ActivationGamePropertyName)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.SelectedChoice is not null)
        {
            emitter.ActivationGamePropertyReference = CloneReference(dialog.SelectedReference);
            var propertyName = dialog.SelectedReference?.PropertyName ?? dialog.SelectedChoice.PropertyName ?? dialog.SelectedValue;
            emitter.ActivationGamePropertyName = dialog.SelectedReference?.AnchorKey is null
                && (!dialog.SelectedReference?.OwnerId.HasValue ?? true || dialog.SelectedReference?.OwnerId == _activationPickerContext.RelativeOwnerId)
                    ? propertyName
                    : null;
            emitter.ActivationGamePropertyDisplayName = FormatSelectedChoice(dialog.SelectedChoice, dialog.SelectedReference);
            EmittersDataGrid.Items.Refresh();
        }
    }

    private void ClearActivationProperty_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ObjectSoundEmitter emitter)
        {
            CommitEmitterEdit();
            emitter.ActivationGamePropertyName = null;
            emitter.ActivationGamePropertyReference = null;
            emitter.ActivationGamePropertyDisplayName = null;
            EmittersDataGrid.Items.Refresh();
        }
    }

    private void AddEmitter_OnClick(object sender, RoutedEventArgs e)
    {
        if (SoundChoices.Count == 0)
        {
            MessageBox.Show(this, "Add a sound definition to this object or an accessible parent scope first.", "Object Sound Emitters", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var emitter = new ObjectSoundEmitter { SoundEffectId = SoundChoices[0].SoundEffectId };
        _emitters.Add(emitter);
        EmittersDataGrid.SelectedItem = emitter;
        EmittersDataGrid.ScrollIntoView(emitter);
    }

    private void RemoveEmitter_OnClick(object sender, RoutedEventArgs e)
    {
        if (EmittersDataGrid.SelectedItem is ObjectSoundEmitter emitter)
        {
            _emitters.Remove(emitter);
        }
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        CommitEmitterEdit();
        if (_emitters.Any(static emitter => emitter.SoundEffectId == Guid.Empty))
        {
            MessageBox.Show(this, "Choose a sound definition for every emitter.", "Object Sound Emitters", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void CommitEmitterEdit()
    {
        EmittersDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        EmittersDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private string ResolveActivationDisplayName(ObjectSoundEmitter emitter)
    {
        var reference = emitter.ActivationGamePropertyReference;
        if (reference is null)
        {
            return string.IsNullOrWhiteSpace(emitter.ActivationGamePropertyName)
                ? "Always active"
                : emitter.ActivationGamePropertyName;
        }

        var match = ActivationPropertyChoices.FirstOrDefault(choice =>
            string.Equals(choice.PropertyName, reference.PropertyName, StringComparison.OrdinalIgnoreCase)
            && (reference.OwnerId.HasValue
                ? choice.OwnerId == reference.OwnerId
                : reference.AnchorKey is not null
                    ? choice.EligibleAnchors.Contains(reference.AnchorKey, StringComparer.OrdinalIgnoreCase)
                    : choice.OwnerId == _activationPickerContext.RelativeOwnerId));
        return match is not null
            ? FormatSelectedChoice(match, reference)
            : FormatReference(reference);
    }

    private static string FormatSelectedChoice(GamePropertyChoiceItem? choice, ProjectGamePropertySourceReferenceDto? reference)
    {
        if (choice is null) return reference is null ? "Always active" : FormatReference(reference);
        if (!string.IsNullOrWhiteSpace(reference?.AnchorKey)) return $"{reference.AnchorKey} → {choice.OwnerVariable}";
        if (reference?.OwnerId is null) return $"Self → {choice.OwnerVariable}";
        return choice.OwnerVariable;
    }

    private static string FormatReference(ProjectGamePropertySourceReferenceDto reference) =>
        !string.IsNullOrWhiteSpace(reference.AnchorKey)
            ? $"{reference.AnchorKey}.{reference.PropertyName}"
            : reference.OwnerId.HasValue
                ? $"Owner {reference.OwnerId.Value:N}.{reference.PropertyName}"
                : $"Self.{reference.PropertyName}";

    private static ProjectGamePropertySourceReferenceDto? CreateLegacySelfReference(string? propertyName) =>
        string.IsNullOrWhiteSpace(propertyName)
            ? null
            : new ProjectGamePropertySourceReferenceDto { PropertyName = propertyName.Trim() };

    private static ProjectGamePropertySourceReferenceDto? CloneReference(ProjectGamePropertySourceReferenceDto? reference) => reference is null
        ? null
        : new ProjectGamePropertySourceReferenceDto
        {
            OwnerId = reference.OwnerId,
            AnchorKey = reference.AnchorKey,
            PropertyName = reference.PropertyName
        };

    private static ObjectSoundEmitter Clone(ObjectSoundEmitter emitter) => new()
    {
        EmitterId = emitter.EmitterId,
        SoundEffectId = emitter.SoundEffectId,
        ActivationGamePropertyReference = CloneReference(emitter.ActivationGamePropertyReference),
        ActivationGamePropertyName = emitter.ActivationGamePropertyName,
        ActivationGamePropertyDisplayName = emitter.ActivationGamePropertyDisplayName,
        ActiveInRoom = emitter.ActiveInRoom,
        ActiveOutOfRoom = emitter.ActiveOutOfRoom
    };
}
