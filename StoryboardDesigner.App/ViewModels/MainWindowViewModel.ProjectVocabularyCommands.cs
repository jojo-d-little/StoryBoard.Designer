using System.Windows.Input;

namespace StoryboardDesigner.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private RelayCommand _addProjectVerbCommand = null!;
    private RelayCommandOfT<string> _removeProjectVerbCommand = null!;
    private RelayCommand _addProjectQualifierCommand = null!;
    private RelayCommandOfT<string> _removeProjectQualifierCommand = null!;
    private string _newVerbInput = string.Empty;
    private string _newQualifierInput = string.Empty;

    public string NewVerbInput
    {
        get => _newVerbInput;
        set
        {
            var candidate = value ?? string.Empty;
            if (_newVerbInput == candidate)
            {
                return;
            }

            _newVerbInput = candidate;
            OnPropertyChanged();
            _addProjectVerbCommand.RaiseCanExecuteChanged();
        }
    }

    public string NewQualifierInput
    {
        get => _newQualifierInput;
        set
        {
            var candidate = value ?? string.Empty;
            if (_newQualifierInput == candidate)
            {
                return;
            }

            _newQualifierInput = candidate;
            OnPropertyChanged();
            _addProjectQualifierCommand.RaiseCanExecuteChanged();
        }
    }

    public ICommand AddProjectVerbCommand => _addProjectVerbCommand;
    public ICommand RemoveProjectVerbCommand => _removeProjectVerbCommand;
    public ICommand AddProjectQualifierCommand => _addProjectQualifierCommand;
    public ICommand RemoveProjectQualifierCommand => _removeProjectQualifierCommand;

    private void InitializeProjectVocabularyCommands()
    {
        _addProjectVerbCommand = new RelayCommand(AddProjectVerbExecute, CanAddProjectVerb);
        _removeProjectVerbCommand = new RelayCommandOfT<string>(RemoveProjectVerbExecute, CanRemoveProjectVerb);
        _addProjectQualifierCommand = new RelayCommand(AddProjectQualifierExecute, CanAddProjectQualifier);
        _removeProjectQualifierCommand = new RelayCommandOfT<string>(RemoveProjectQualifierExecute, CanRemoveProjectQualifier);
    }

    private void AddProjectVerbExecute()
    {
        AddProjectCommandVerb(NewVerbInput);
        NewVerbInput = string.Empty;
    }

    private bool CanAddProjectVerb()
    {
        return !string.IsNullOrWhiteSpace(NewVerbInput);
    }

    private void RemoveProjectVerbExecute(string? verb)
    {
        if (string.IsNullOrWhiteSpace(verb))
        {
            return;
        }

        RemoveProjectCommandVerb(verb);
    }

    private bool CanRemoveProjectVerb(string? verb)
    {
        return !string.IsNullOrWhiteSpace(verb);
    }

    private void AddProjectQualifierExecute()
    {
        AddProjectCommandQualifier(NewQualifierInput);
        NewQualifierInput = string.Empty;
    }

    private bool CanAddProjectQualifier()
    {
        return !string.IsNullOrWhiteSpace(NewQualifierInput);
    }

    private void RemoveProjectQualifierExecute(string? qualifier)
    {
        if (string.IsNullOrWhiteSpace(qualifier))
        {
            return;
        }

        RemoveProjectCommandQualifier(qualifier);
    }

    private bool CanRemoveProjectQualifier(string? qualifier)
    {
        return !string.IsNullOrWhiteSpace(qualifier);
    }
}
