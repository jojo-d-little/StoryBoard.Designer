using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Views;

public partial class ScopedProcedureOwnershipDialog : Window
{
    private sealed class ProcedureRow : INotifyPropertyChanged
    {
        private bool _isOwned;

        public Guid ProcedureId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public string IdText { get; init; } = string.Empty;

        public bool IsOwned
        {
            get => _isOwned;
            set
            {
                if (_isOwned == value)
                {
                    return;
                }

                _isOwned = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private readonly IList<Guid> _targetProcedureIds;
    private readonly ObservableCollection<ProcedureRow> _rows;

    public ScopedProcedureOwnershipDialog(
        string scopeLabel,
        IList<Guid> procedureIds,
        IReadOnlyList<ProcedureDefinition> availableProcedures)
    {
        InitializeComponent();

        _targetProcedureIds = procedureIds;
        var owned = new HashSet<Guid>((procedureIds ?? Array.Empty<Guid>())
            .Where(static id => id != Guid.Empty));

        HeaderText.Text = string.IsNullOrWhiteSpace(scopeLabel)
            ? "Edit Procedures"
            : $"{scopeLabel} - Procedures";

        _rows = new ObservableCollection<ProcedureRow>((availableProcedures ?? Array.Empty<ProcedureDefinition>())
            .Where(static procedure => procedure.Id != Guid.Empty)
            .GroupBy(static procedure => procedure.Id)
            .Select(static group => group.First())
            .OrderBy(static procedure => procedure.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static procedure => procedure.Id)
            .Select(procedure => new ProcedureRow
            {
                ProcedureId = procedure.Id,
                Name = string.IsNullOrWhiteSpace(procedure.Name) ? "Unnamed Procedure" : procedure.Name.Trim(),
                Summary = string.IsNullOrWhiteSpace(procedure.ProcedureSummary) ? string.Empty : procedure.ProcedureSummary.Trim(),
                IdText = procedure.Id.ToString("N"),
                IsOwned = owned.Contains(procedure.Id)
            }));

        ProceduresGrid.ItemsSource = _rows;
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        _targetProcedureIds.Clear();

        foreach (var id in _rows
                     .Where(static row => row.IsOwned)
                     .Select(static row => row.ProcedureId)
                     .Where(static id => id != Guid.Empty)
                     .Distinct()
                     .ToList())
        {
            _targetProcedureIds.Add(id);
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
