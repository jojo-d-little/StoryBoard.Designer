using System.Collections.ObjectModel;
using System.ComponentModel;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.ViewModels;

public sealed class TraversalWizardDialogViewModel : ViewModelBase
{
    public TraversalWizardDialogViewModel(TraversalWizardDialogRequest request)
    {
        Request = request;
        Rows = new ObservableCollection<TraversalWizardDirectionRowViewModel>(
            request.Rows.Select(seed => new TraversalWizardDirectionRowViewModel(seed, request.DoorTemplateOptions)));

        foreach (var row in Rows)
        {
            row.PropertyChanged += Row_OnPropertyChanged;
        }

        SelectAllCommand = new RelayCommand(SelectAllRows);
        ClearAllCommand = new RelayCommand(ClearAllRows);
    }

    public TraversalWizardDialogRequest Request { get; }

    public ObservableCollection<TraversalWizardDirectionRowViewModel> Rows { get; }

    public RelayCommand SelectAllCommand { get; }

    public RelayCommand ClearAllCommand { get; }

    public int SelectedCount => Rows.Count(row => row.IncludeTraversal);

    public TraversalWizardDialogResult BuildResult()
    {
        return new TraversalWizardDialogResult
        {
            Rows = Rows.Select(row => row.ToChoice()).ToList()
        };
    }

    private void Row_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TraversalWizardDirectionRowViewModel.IncludeTraversal))
        {
            OnPropertyChanged(nameof(SelectedCount));
        }
    }

    private void SelectAllRows()
    {
        foreach (var row in Rows)
        {
            if (!row.CanToggleSelection)
            {
                continue;
            }

            row.IncludeTraversal = true;
        }
    }

    private void ClearAllRows()
    {
        foreach (var row in Rows)
        {
            if (!row.CanToggleSelection)
            {
                continue;
            }

            row.IncludeTraversal = false;
        }
    }
}
