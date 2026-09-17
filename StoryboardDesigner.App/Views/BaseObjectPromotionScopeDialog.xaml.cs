using System.Windows;
using System.Windows.Input;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class BaseObjectPromotionScopeDialog : Window
{
    private sealed class Option
    {
        public required BaseObjectPromotionScopeKind ScopeKind { get; init; }
        public required string Label { get; init; }
    }

    public BaseObjectPromotionScopeDialog(IReadOnlyList<BaseObjectPromotionScopeOption> options)
    {
        InitializeComponent();

        var dialogOptions = options
            .Select(option => new Option
            {
                ScopeKind = option.ScopeKind,
                Label = option.Label
            })
            .ToList();

        ScopeListBox.ItemsSource = dialogOptions;
        ScopeListBox.SelectedIndex = dialogOptions.Count > 0 ? 0 : -1;
    }

    public BaseObjectPromotionScopeKind SelectedScopeKind { get; private set; } = BaseObjectPromotionScopeKind.Area;

    private void Select_OnClick(object sender, RoutedEventArgs e)
    {
        if (ScopeListBox.SelectedItem is not Option option)
        {
            System.Windows.MessageBox.Show(this, "Choose a target scope.", "Promote to Base Object", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedScopeKind = option.ScopeKind;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ScopeListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Select_OnClick(sender, e);
    }
}
