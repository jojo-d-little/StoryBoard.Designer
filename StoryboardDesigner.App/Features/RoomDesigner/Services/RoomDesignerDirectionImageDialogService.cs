using System.Windows;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;
using StoryboardDesigner.App.Views;

namespace StoryboardDesigner.App.Services;

public sealed class RoomDesignerDirectionImageDialogService : IRoomDesignerDirectionImageDialogService
{
    private readonly Dictionary<(Guid roomId, RoomImageSlot slot), IRoomDesignerDirectionImageDialogWindow> _openEditors = new();
    private readonly Func<RoomDesignerImageSlotViewModel, IRoomDesignerDirectionImageDialogWindow> _windowFactory;

    public RoomDesignerDirectionImageDialogService()
        : this(CreateDefaultWindow)
    {
    }

    public RoomDesignerDirectionImageDialogService(Func<RoomDesignerImageSlotViewModel, IRoomDesignerDirectionImageDialogWindow> windowFactory)
    {
        _windowFactory = windowFactory;
    }

    public void OpenOrFocus(Guid roomId, RoomDesignerImageSlotViewModel slot)
    {
        var key = (roomId, slot.Entry.Slot);
        if (_openEditors.TryGetValue(key, out var existing))
        {
            existing.Activate();
            return;
        }

        var dialog = _windowFactory(slot);

        dialog.Closed += (_, _) => _openEditors.Remove(key);
        _openEditors[key] = dialog;
        dialog.Show();
    }

    public void CloseAllEditors()
    {
        foreach (var dialog in _openEditors.Values.ToList())
        {
            dialog.Close();
        }

        _openEditors.Clear();
    }

    private static Window? GetOwnerWindow()
    {
        var activeWindow = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        return activeWindow ?? System.Windows.Application.Current?.MainWindow;
    }

    private static IRoomDesignerDirectionImageDialogWindow CreateDefaultWindow(RoomDesignerImageSlotViewModel slot)
    {
        var dialog = new RoomDesignerDirectionImageDialog(slot)
        {
            Owner = GetOwnerWindow()
        };

        return new RoomDesignerDirectionImageDialogWindowAdapter(dialog);
    }
}
