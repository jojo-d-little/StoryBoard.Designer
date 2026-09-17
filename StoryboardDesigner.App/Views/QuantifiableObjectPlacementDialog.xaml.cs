using System.Windows;
using System.Windows.Input;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class QuantifiableObjectPlacementDialog : Window
{
    private sealed class Option
    {
        public required QuantifiableObjectPlacementCandidate Candidate { get; init; }
        public required string Label { get; init; }
    }

    public QuantifiableObjectPlacementDialog(IReadOnlyList<QuantifiableObjectPlacementCandidate> candidates)
    {
        InitializeComponent();

        var options = candidates
            .OrderBy(candidate => candidate.ScopePriority)
            .ThenBy(candidate => candidate.SourceObject.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.SourceScopePath, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => new Option
            {
                Candidate = candidate,
                Label = BuildLabel(candidate)
            })
            .ToList();

        ObjectsListBox.ItemsSource = options;
        ObjectsListBox.SelectedIndex = options.Count > 0 ? 0 : -1;
    }

    public QuantifiableObjectPlacementSelection? Selection { get; private set; }

    private void Add_OnClick(object sender, RoutedEventArgs e)
    {
        if (ObjectsListBox.SelectedItem is not Option option)
        {
            System.Windows.MessageBox.Show(this, "Choose a quantifiable base object.", "Add Instance of Existing Object", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!int.TryParse(QuantityTextBox.Text.Trim(), out var quantity) || quantity < 1)
        {
            System.Windows.MessageBox.Show(this, "Quantity must be a number greater than or equal to 1.", "Add Instance of Existing Object", MessageBoxButton.OK, MessageBoxImage.Information);
            QuantityTextBox.Text = "1";
            QuantityTextBox.Focus();
            QuantityTextBox.SelectAll();
            return;
        }

        Selection = new QuantifiableObjectPlacementSelection(option.Candidate.SourceObject, quantity);
        DialogResult = true;
    }

    private static string BuildLabel(QuantifiableObjectPlacementCandidate candidate)
    {
        var obj = candidate.SourceObject;
        var distributionMode = string.Equals(obj.QuantifiablePlacementDistributionMode, "IndividualInstances", StringComparison.OrdinalIgnoreCase)
            ? "IndividualInstances"
            : "GroupedStack";
        return $"{obj.Name} [{candidate.SourceScopePath}] - {distributionMode}";
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ObjectsListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Add_OnClick(sender, e);
    }

    private void QuantityTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void QuantityTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(System.Windows.DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.SourceDataObject.GetData(System.Windows.DataFormats.Text) as string;
        if (string.IsNullOrWhiteSpace(pastedText) || !pastedText.All(char.IsDigit))
        {
            e.CancelCommand();
        }
    }
}
