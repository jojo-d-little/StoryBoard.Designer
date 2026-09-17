namespace StoryboardDesigner.App.Services;

public interface IRoomDesignerDirectionImageDialogWindow
{
    event EventHandler? Closed;

    void Show();

    void Activate();

    void Close();
}
