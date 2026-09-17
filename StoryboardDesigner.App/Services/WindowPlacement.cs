namespace StoryboardDesigner.App.Services;

internal sealed class WindowPlacement
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string WindowState { get; set; } = System.Windows.WindowState.Normal.ToString();
    public double? LeftPaneWidth { get; set; }
    public double? RightPaneWidth { get; set; }
}
