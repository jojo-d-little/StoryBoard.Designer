using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class AreaRoomPlacementViewModel : ViewModelBase
{
    private readonly AreaRoomPlacement _placement;
    private bool _isTraversalSourceSelected;
    private bool _hasRoomAbove;
    private bool _hasRoomBelow;
    private bool _hasTraversalUp;
    private bool _hasTraversalDown;

    public AreaRoomPlacementViewModel(Room room, AreaRoomPlacement placement)
    {
        Room = room;
        _placement = placement;
    }

    public Room Room { get; }

    public AreaMapFootprint Footprint => AreaMapGeometry.FromPlacement(_placement);

    public double CardWidth => AreaMapGeometry.CardWidth(ColumnSpan);
    public double CardHeight => AreaMapGeometry.CardHeight(RowSpan);
    public string SizeLabel => $"Size {ColumnSpan}×{RowSpan} ▾";

    public int ColumnSpan
    {
        get => _placement.ColumnSpan;
        set
        {
            if (_placement.ColumnSpan == value) return;
            _placement.ColumnSpan = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CardWidth));
            OnPropertyChanged(nameof(Footprint));
            OnPropertyChanged(nameof(SizeLabel));
        }
    }

    public int RowSpan
    {
        get => _placement.RowSpan;
        set
        {
            if (_placement.RowSpan == value) return;
            _placement.RowSpan = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CardHeight));
            OnPropertyChanged(nameof(Footprint));
            OnPropertyChanged(nameof(SizeLabel));
        }
    }

    public bool IsTraversalSourceSelected
    {
        get => _isTraversalSourceSelected;
        set
        {
            if (_isTraversalSourceSelected == value)
            {
                return;
            }

            _isTraversalSourceSelected = value;
            OnPropertyChanged();
        }
    }

    public bool HasRoomAbove
    {
        get => _hasRoomAbove;
        set
        {
            if (_hasRoomAbove == value)
            {
                return;
            }

            _hasRoomAbove = value;
            OnPropertyChanged();
        }
    }

    public bool HasRoomBelow
    {
        get => _hasRoomBelow;
        set
        {
            if (_hasRoomBelow == value)
            {
                return;
            }

            _hasRoomBelow = value;
            OnPropertyChanged();
        }
    }

    public bool HasTraversalUp
    {
        get => _hasTraversalUp;
        set
        {
            if (_hasTraversalUp == value)
            {
                return;
            }

            _hasTraversalUp = value;
            OnPropertyChanged();
        }
    }

    public bool HasTraversalDown
    {
        get => _hasTraversalDown;
        set
        {
            if (_hasTraversalDown == value)
            {
                return;
            }

            _hasTraversalDown = value;
            OnPropertyChanged();
        }
    }

    public double X
    {
        get => _placement.X;
        set
        {
            if (Math.Abs(_placement.X - value) < 0.01)
            {
                return;
            }

            _placement.X = value;
            OnPropertyChanged();
        }
    }

    public double Y
    {
        get => _placement.Y;
        set
        {
            if (Math.Abs(_placement.Y - value) < 0.01)
            {
                return;
            }

            _placement.Y = value;
            OnPropertyChanged();
        }
    }

    public int FloorElevation
    {
        get => _placement.FloorElevation;
        set
        {
            if (_placement.FloorElevation == value)
            {
                return;
            }

            _placement.FloorElevation = value;
            OnPropertyChanged();
        }
    }
}
