using System.Windows;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views;

public partial class TraversalWizardDialog : Window
{
    private readonly TraversalWizardDialogViewModel _viewModel;

    public TraversalWizardDialog(TraversalWizardDialogRequest request)
    {
        InitializeComponent();
        _viewModel = new TraversalWizardDialogViewModel(request);
        DataContext = _viewModel;
    }

    public TraversalWizardDialogResult Result => _viewModel.BuildResult();

    private void Continue_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
