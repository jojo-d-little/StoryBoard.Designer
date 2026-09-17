using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StoryboardDesigner.App.Models;

public sealed class Room : ScopeNodeBase, INotifyPropertyChanged
{
    private string _name = "New Room";
    private string _nameInGame = string.Empty;
    private string _description = string.Empty;
    private string _producerNotes = string.Empty;
    private int _roomImageCanvasWidth = 800;
    private int _roomImageCanvasHeight = 600;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScopeNameInGame));
        }
    }

    public string NameInGame
    {
        get => _nameInGame;
        set
        {
            if (_nameInGame == value)
            {
                return;
            }

            _nameInGame = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasNameInGame));
            OnPropertyChanged(nameof(ScopeNameInGame));
        }
    }

    public bool HasNameInGame => !string.IsNullOrWhiteSpace(NameInGame);

    public string Description
    {
        get => _description;
        set
        {
            if (_description == value)
            {
                return;
            }

            _description = value;
            OnPropertyChanged();
        }
    }

    public string ProducerNotes
    {
        get => _producerNotes;
        set
        {
            if (_producerNotes == value)
            {
                return;
            }

            _producerNotes = value;
            OnPropertyChanged();
        }
    }

    public bool HideEmptyConfiguration { get; set; }

    public int RoomImageCanvasWidth
    {
        get => _roomImageCanvasWidth;
        set
        {
            var normalized = value > 0 ? value : 800;
            if (_roomImageCanvasWidth == normalized)
            {
                return;
            }

            _roomImageCanvasWidth = normalized;
            OnPropertyChanged();
        }
    }

    public int RoomImageCanvasHeight
    {
        get => _roomImageCanvasHeight;
        set
        {
            var normalized = value > 0 ? value : 600;
            if (_roomImageCanvasHeight == normalized)
            {
                return;
            }

            _roomImageCanvasHeight = normalized;
            OnPropertyChanged();
        }
    }

    public List<GamePropertyDefinition> Variables { get; set; } = new();
    public RuntimeRoomImageDisplayMode RoomDisplayMode { get; set; } = RuntimeRoomImageDisplayMode.Independent;
    public AreaAdjacencyMode? TraversalModeOverride { get; set; }
    public List<string> AdditionalVerbs { get; set; } = new();
    public List<string> AdditionalDirectionals { get; set; } = new();
    public List<DirectionalTraversalMapping> AdditionalDirectionalTraversalMappings { get; set; } = new();
    public List<RoomImageEntry> Images { get; set; } = new();
    public List<GameObject> GameObjects { get; set; } = new();
    public List<SoundEffectLibraryEntry> SoundEffectLibraryEntries { get; set; } = new();
    public ObservableCollection<CommandAction> AvailableActions { get; set; } = new();
    public List<string> Commands { get; set; } = new();

    public override ScopeNodeKind ScopeKind => ScopeNodeKind.Room;
    public override string ScopeName => Name;
    public override string ScopeNameInGame
    {
        get
        {
            var inGame = NameInGame?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(inGame) ? ScopeName : inGame;
        }
    }
    public override IEnumerable<string> ScopeTokens
    {
        get
        {
            var nameInGame = NameInGame?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(nameInGame))
            {
                yield return nameInGame;
            }

            var name = Name?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name))
            {
                yield return name;
            }
        }
    }
    public override IEnumerable<IScopedAwareNode> ChildScopes => GameObjects;

    protected override bool CanAcceptChild(IScopedAwareNode child)
    {
        return child.ScopeKind == ScopeNodeKind.GameObject;
    }

    protected override bool TryAddChildCore(IScopedAwareNode child)
    {
        if (child is not GameObject obj)
        {
            return false;
        }

        if (GameObjects.Contains(obj))
        {
            return true;
        }

        GameObjects.Add(obj);
        return true;
    }

    protected override bool TryRemoveChildCore(IScopedAwareNode child)
    {
        return child is GameObject obj && GameObjects.Remove(obj);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

