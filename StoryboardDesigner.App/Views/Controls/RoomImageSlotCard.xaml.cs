namespace StoryboardDesigner.App.Views.Controls;

/// <summary>
/// Interaction logic for RoomImageSlotCard.xaml
/// </summary>
public partial class RoomImageSlotCard : System.Windows.Controls.UserControl
{
    public static readonly System.Windows.DependencyProperty SlotLabelProperty =
        System.Windows.DependencyProperty.Register(
            nameof(SlotLabel),
            typeof(string),
            typeof(RoomImageSlotCard),
            new System.Windows.PropertyMetadata(string.Empty));

    public RoomImageSlotCard()
    {
        InitializeComponent();
    }

    public string SlotLabel
    {
        get => (string)GetValue(SlotLabelProperty);
        set => SetValue(SlotLabelProperty, value);
    }
}
