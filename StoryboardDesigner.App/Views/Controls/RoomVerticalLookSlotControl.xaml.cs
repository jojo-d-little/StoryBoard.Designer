namespace StoryboardDesigner.App.Views.Controls;

/// <summary>
/// Interaction logic for RoomVerticalLookSlotControl.xaml
/// </summary>
public partial class RoomVerticalLookSlotControl : System.Windows.Controls.UserControl
{
    public static readonly System.Windows.DependencyProperty TitleProperty =
        System.Windows.DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(RoomVerticalLookSlotControl),
            new System.Windows.PropertyMetadata(string.Empty));

    public RoomVerticalLookSlotControl()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
