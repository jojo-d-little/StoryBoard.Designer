using StoryboardDesigner.App.Views;

namespace StoryboardDesigner.App.Services;

public sealed class RoomDesignerDirectionImageDialogWindowAdapter : IRoomDesignerDirectionImageDialogWindow
{
    private readonly RoomDesignerDirectionImageDialog _dialog;

    public RoomDesignerDirectionImageDialogWindowAdapter(RoomDesignerDirectionImageDialog dialog)
    {
        _dialog = dialog;
        _dialog.Closed += (_, args) => Closed?.Invoke(this, args);
    }

    public event EventHandler? Closed;

    public void Show() => _dialog.ShowDialog();

    public void Activate() => _dialog.Activate();

    public void Close() => _dialog.Close();
}
