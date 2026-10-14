using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using StoryboardDesigner.App.Models;
using MessageBox = System.Windows.MessageBox;

namespace StoryboardDesigner.App.Views;

public partial class ObjectSoundEmitterDialog : Window
{
    public sealed record ActivationChoice(Guid? Id, string Label);

    private readonly ObservableCollection<ObjectSoundEmitter> _emitters;

    public ObjectSoundEmitterDialog(
        IReadOnlyList<ObjectSoundEmitter> emitters,
        IReadOnlyList<SoundEffectChoiceItem> soundChoices,
        IReadOnlyList<GamePropertyDefinition> activationProperties)
    {
        InitializeComponent();
        SoundChoices = soundChoices
            .Where(static choice => choice.SoundEffectId != Guid.Empty)
            .GroupBy(static choice => choice.SoundEffectId)
            .Select(static group => group.First())
            .OrderBy(static choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ActivationChoices = new List<ActivationChoice> { new(null, "Always enabled") }
            .Concat(activationProperties
                .Where(static property => property.Id != Guid.Empty
                    && (property.ValueRestriction == GamePropertyValueRestriction.TrueFalse
                        || bool.TryParse(property.DefaultValue, out _)))
                .OrderBy(static property => property.Name, StringComparer.OrdinalIgnoreCase)
                .Select(static property => new ActivationChoice(property.Id, property.Name)))
            .ToList();

        SoundDefinitionColumn.ItemsSource = SoundChoices;
        ActivationPropertyColumn.ItemsSource = ActivationChoices;
        _emitters = new ObservableCollection<ObjectSoundEmitter>(emitters.Select(Clone));
        EmittersDataGrid.ItemsSource = _emitters;
    }

    public IReadOnlyList<SoundEffectChoiceItem> SoundChoices { get; }
    public IReadOnlyList<ActivationChoice> ActivationChoices { get; }
    public IReadOnlyList<ObjectSoundEmitter> Emitters => _emitters.Select(Clone).ToList();

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
        EmittersDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        EmittersDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (_emitters.Any(static emitter => emitter.SoundEffectId == Guid.Empty))
        {
            MessageBox.Show(this, "Choose a sound definition for every emitter.", "Object Sound Emitters", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private static ObjectSoundEmitter Clone(ObjectSoundEmitter emitter) => new()
    {
        EmitterId = emitter.EmitterId,
        SoundEffectId = emitter.SoundEffectId,
        ActivationGamePropertyId = emitter.ActivationGamePropertyId,
        ActiveInRoom = emitter.ActiveInRoom,
        ActiveOutOfRoom = emitter.ActiveOutOfRoom
    };
}
