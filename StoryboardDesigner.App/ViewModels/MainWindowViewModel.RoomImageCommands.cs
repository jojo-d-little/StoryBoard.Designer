using System.Windows.Input;

namespace StoryboardDesigner.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private RelayCommandOfT<RoomDesignerImageSlotViewModel> _editRoomImageSlotCommand = null!;

    public ICommand EditRoomImageSlotCommand => _editRoomImageSlotCommand;

    private void InitializeRoomImageCommands()
    {
        _editRoomImageSlotCommand = new RelayCommandOfT<RoomDesignerImageSlotViewModel>(EditRoomImageSlotExecute, slot => slot is not null);
    }

    private void EditRoomImageSlotExecute(RoomDesignerImageSlotViewModel? slot)
    {
        if (slot is null || SelectedRoom is null)
        {
            return;
        }

        _roomDesignerDirectionImageDialogService.OpenOrFocus(SelectedRoom.Id, slot);
    }
}
