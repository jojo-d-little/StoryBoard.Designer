namespace StoryboardDesigner.App.Services;

using StoryboardDesigner.App.ViewModels;

public interface IRoomDesignerDirectionImageDialogService
{
    void OpenOrFocus(Guid roomId, RoomDesignerImageSlotViewModel slot);

    void CloseAllEditors();
}
