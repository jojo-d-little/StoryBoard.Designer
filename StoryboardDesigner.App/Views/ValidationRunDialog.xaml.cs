using System.Windows;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class ValidationRunDialog : Window
{
    public ValidationRunDialogResult Selection { get; private set; } = new();

    public ValidationRunDialog(ValidationRunDialogRequest request)
    {
        InitializeComponent();

        var contextLabel = string.IsNullOrWhiteSpace(request.ContextNodeLabel)
            ? "(unnamed node)"
            : request.ContextNodeLabel.Trim();
        ContextNodeTextBlock.Text = $"Context: {contextLabel}";

        switch (request.InitialScope)
        {
            case ValidationRunScopeOption.NodeOnly:
                NodeOnlyRadioButton.IsChecked = true;
                break;
            case ValidationRunScopeOption.WholeProject:
                WholeProjectRadioButton.IsChecked = true;
                break;
            default:
                FromHereRadioButton.IsChecked = true;
                break;
        }

        switch (request.InitialCompletion)
        {
            case ValidationCompletionMode.StopOnFirstBlocking:
                StopOnFirstBlockingRadioButton.IsChecked = true;
                break;
            default:
                FullReportRadioButton.IsChecked = true;
                break;
        }
    }

    private void Run_OnClick(object sender, RoutedEventArgs e)
    {
        Selection = new ValidationRunDialogResult
        {
            Scope = WholeProjectRadioButton.IsChecked == true
                ? ValidationRunScopeOption.WholeProject
                : NodeOnlyRadioButton.IsChecked == true
                    ? ValidationRunScopeOption.NodeOnly
                    : ValidationRunScopeOption.FromHere,
            Completion = StopOnFirstBlockingRadioButton.IsChecked == true
                ? ValidationCompletionMode.StopOnFirstBlocking
                : ValidationCompletionMode.FullReport
        };

        DialogResult = true;
    }
}

