using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class NavigationArrowViewModel : ViewModelBase
{
    private bool _hasValidationError;
    private bool _hasValidationWarning;
    private string _validationToolTip = string.Empty;

    public NavigationArrowViewModel(TraversalConnection connection)
    {
        Connection = connection;
    }

    public TraversalConnection Connection { get; }
    public Guid SourceRoomId { get; init; }
    public Guid DestinationRoomId { get; init; }
    public Direction Direction { get; init; }

    public double X { get; init; }
    public double Y { get; init; }
    public double Angle { get; init; }

    public bool HasValidationError
    {
        get => _hasValidationError;
        set
        {
            if (_hasValidationError == value)
            {
                return;
            }

            _hasValidationError = value;
            OnPropertyChanged();
        }
    }

    public bool HasValidationWarning
    {
        get => _hasValidationWarning;
        set
        {
            if (_hasValidationWarning == value)
            {
                return;
            }

            _hasValidationWarning = value;
            OnPropertyChanged();
        }
    }

    public string ValidationToolTip
    {
        get => _validationToolTip;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_validationToolTip, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _validationToolTip = normalized;
            OnPropertyChanged();
        }
    }
}
