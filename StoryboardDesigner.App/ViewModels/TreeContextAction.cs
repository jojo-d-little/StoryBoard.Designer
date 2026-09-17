namespace StoryboardDesigner.App.ViewModels;

public sealed class TreeContextAction
{
    public TreeContextAction(string actionId, string header, bool isEnabled = true)
    {
        ActionId = actionId;
        Header = header;
        IsEnabled = isEnabled;
    }

    public string ActionId { get; }
    public string Header { get; }
    public bool IsEnabled { get; }
}
