using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.ViewModels;

public sealed class TraversalWizardDirectionRowViewModel : ViewModelBase
{
    private bool _includeTraversal;
    private bool _doorEnabled;
    private TraversalWizardDoorBehaviorOption _doorBehavior;
    private TraversalWizardDoorDefaultStateOption _doorState;
    private bool _doorLockedWhenClosed;
    private Guid? _doorTemplateId;

    public TraversalWizardDirectionRowViewModel(
        TraversalWizardDirectionSeed seed,
        IReadOnlyList<TraversalWizardDoorTemplateOption> doorTemplateOptions)
    {
        Direction = seed.Direction;
        DirectionLabel = seed.DirectionLabel;
        DestinationRoomName = seed.DestinationRoomName;
        HasImmediateNeighbor = seed.HasImmediateNeighbor;
        HasExistingTraversal = seed.HasExistingTraversal;
        CanToggleSelection = seed.CanToggleSelection;
        StatusText = seed.StatusText;
        DoorTemplateOptions = doorTemplateOptions;

        _includeTraversal = seed.DefaultIncludeTraversal;
        _doorEnabled = seed.DefaultDoorEnabled;
        _doorBehavior = seed.DefaultDoorBehavior;
        _doorState = seed.DefaultDoorState;
        _doorLockedWhenClosed = seed.DefaultDoorLockedWhenClosed;
        _doorTemplateId = seed.DefaultDoorTemplateId;
    }

    public Direction Direction { get; }
    public string DirectionLabel { get; }
    public string DestinationRoomName { get; }
    public bool HasImmediateNeighbor { get; }
    public bool HasExistingTraversal { get; }
    public bool CanToggleSelection { get; }
    public string StatusText { get; }
    public IReadOnlyList<TraversalWizardDoorTemplateOption> DoorTemplateOptions { get; }

    public bool IncludeTraversal
    {
        get => _includeTraversal;
        set
        {
            var normalized = CanToggleSelection && value;
            if (_includeTraversal == normalized)
            {
                return;
            }

            _includeTraversal = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanEditDoorEnabled));
            OnPropertyChanged(nameof(CanEditDoorBehavior));
            OnPropertyChanged(nameof(CanEditDoorState));
            OnPropertyChanged(nameof(CanEditDoorLockedWhenClosed));
            OnPropertyChanged(nameof(CanEditDoorTemplate));
        }
    }

    public bool DoorEnabled
    {
        get => _doorEnabled;
        set
        {
            var normalized = CanEditDoorEnabled && value;
            if (_doorEnabled == normalized)
            {
                return;
            }

            _doorEnabled = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanEditDoorBehavior));
            OnPropertyChanged(nameof(CanEditDoorState));
            OnPropertyChanged(nameof(CanEditDoorLockedWhenClosed));
            OnPropertyChanged(nameof(CanEditDoorTemplate));
        }
    }

    public TraversalWizardDoorBehaviorOption DoorBehavior
    {
        get => _doorBehavior;
        set
        {
            if (_doorBehavior == value)
            {
                return;
            }

            _doorBehavior = value;
            OnPropertyChanged();
        }
    }

    public TraversalWizardDoorDefaultStateOption DoorState
    {
        get => _doorState;
        set
        {
            if (_doorState == value)
            {
                return;
            }

            _doorState = value;
            if (_doorState != TraversalWizardDoorDefaultStateOption.Closed)
            {
                _doorLockedWhenClosed = false;
                OnPropertyChanged(nameof(DoorLockedWhenClosed));
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(CanEditDoorLockedWhenClosed));
        }
    }

    public bool DoorLockedWhenClosed
    {
        get => _doorLockedWhenClosed;
        set
        {
            var normalized = CanEditDoorLockedWhenClosed && value;
            if (_doorLockedWhenClosed == normalized)
            {
                return;
            }

            _doorLockedWhenClosed = normalized;
            OnPropertyChanged();
        }
    }

    public Guid? DoorTemplateId
    {
        get => _doorTemplateId;
        set
        {
            if (_doorTemplateId == value)
            {
                return;
            }

            _doorTemplateId = value;
            OnPropertyChanged();
        }
    }

    public bool CanEditDoorEnabled => CanToggleSelection && IncludeTraversal;
    public bool CanEditDoorBehavior => CanEditDoorEnabled && DoorEnabled;
    public bool CanEditDoorState => CanEditDoorEnabled && DoorEnabled;
    public bool CanEditDoorLockedWhenClosed => CanEditDoorEnabled
                                             && DoorEnabled
                                             && DoorState == TraversalWizardDoorDefaultStateOption.Closed;
    public bool CanEditDoorTemplate => CanEditDoorEnabled && DoorEnabled;

    public TraversalWizardDirectionChoice ToChoice()
    {
        var includeTraversal = IncludeTraversal;
        var doorEnabled = includeTraversal && DoorEnabled;
        var doorState = doorEnabled
            ? DoorState
            : TraversalWizardDoorDefaultStateOption.Open;
        var doorLockedWhenClosed = doorEnabled
                                   && doorState == TraversalWizardDoorDefaultStateOption.Closed
                                   && DoorLockedWhenClosed;

        return new TraversalWizardDirectionChoice
        {
            Direction = Direction,
            HasImmediateNeighbor = HasImmediateNeighbor,
            HasExistingTraversal = HasExistingTraversal,
            IncludeTraversal = includeTraversal,
            DoorEnabled = doorEnabled,
            DoorBehavior = DoorBehavior,
            DoorState = doorState,
            DoorLockedWhenClosed = doorLockedWhenClosed,
            DoorTemplateId = doorEnabled ? DoorTemplateId : null,
            StatusText = StatusText
        };
    }
}
