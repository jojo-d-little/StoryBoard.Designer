using System.Windows;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Views;

public partial class SelectProcedureDialog : Window
{
    public SelectProcedureDialog(IReadOnlyList<ProcedureChoiceItem> procedureChoices, Guid? selectedProcedureId)
    {
        InitializeComponent();

        var normalizedChoices = (procedureChoices ?? Array.Empty<ProcedureChoiceItem>())
            .Where(static choice => choice.ProcedureId != Guid.Empty)
            .GroupBy(static choice => choice.ProcedureId)
            .Select(static group => group.First())
            .OrderBy(static choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static choice => choice.OwnerLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ProceduresGrid.ItemsSource = normalizedChoices;
        if (selectedProcedureId.HasValue && selectedProcedureId.Value != Guid.Empty)
        {
            var selected = normalizedChoices.FirstOrDefault(choice => choice.ProcedureId == selectedProcedureId.Value);
            if (selected is not null)
            {
                ProceduresGrid.SelectedItem = selected;
            }
        }

        if (ProceduresGrid.SelectedItem is null && normalizedChoices.Count > 0)
        {
            ProceduresGrid.SelectedItem = normalizedChoices[0];
        }
    }

    public Guid? SelectedProcedureId { get; private set; }

    private void Choose_OnClick(object sender, RoutedEventArgs e)
    {
        if (ProceduresGrid.SelectedItem is not ProcedureChoiceItem selected)
        {
            System.Windows.MessageBox.Show(this, "Select a procedure.", "Procedure", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedProcedureId = selected.ProcedureId;
        DialogResult = true;
    }

    private void Clear_OnClick(object sender, RoutedEventArgs e)
    {
        SelectedProcedureId = null;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ProceduresGrid_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ProceduresGrid.SelectedItem is not ProcedureChoiceItem selected)
        {
            return;
        }

        SelectedProcedureId = selected.ProcedureId;
        DialogResult = true;
    }
}
