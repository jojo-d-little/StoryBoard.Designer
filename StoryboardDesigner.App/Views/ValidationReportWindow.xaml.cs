using System.Windows;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class ValidationReportWindow : Window
{
    private readonly Action<ProjectValidationIssue>? _navigateToIssue;

    private sealed record ValidationRow(
        string RuleId,
        string Path,
        string Problem,
        bool CanNavigate,
        ProjectValidationIssue Issue);

    public ValidationReportWindow(
        string reportTitle,
        IReadOnlyList<ProjectValidationIssue> issues,
        string reportFilePath,
        Action<ProjectValidationIssue>? navigateToIssue = null)
    {
        InitializeComponent();
        _navigateToIssue = navigateToIssue;

        Title = string.IsNullOrWhiteSpace(reportTitle) ? "Validation Report" : reportTitle;

        var safeIssues = issues ?? Array.Empty<ProjectValidationIssue>();
        var errors = safeIssues.Count(issue => issue.Severity == ValidationSeverity.Error);
        var warnings = safeIssues.Count(issue => issue.Severity == ValidationSeverity.Warning);

        SummaryTextBlock.Text = $"{safeIssues.Count} issue(s): {errors} error(s), {warnings} warning(s).";
        ReportPathTextBlock.Text = $"Report file: {reportFilePath}";

        IssuesDataGrid.ItemsSource = safeIssues.Select(issue =>
            new ValidationRow(
                string.IsNullOrWhiteSpace(issue.RuleId) ? "(none)" : issue.RuleId,
                string.IsNullOrWhiteSpace(issue.Path) ? "Project" : issue.Path,
                BuildProblemText(issue),
                !string.IsNullOrWhiteSpace(issue.Path) && _navigateToIssue is not null,
                issue))
            .ToList();
    }

    private void GoToNode_OnClick(object sender, RoutedEventArgs e)
    {
        if (_navigateToIssue is null)
        {
            return;
        }

        if (sender is not FrameworkElement { DataContext: ValidationRow row })
        {
            return;
        }

        _navigateToIssue(row.Issue);
    }

    private static string BuildProblemText(ProjectValidationIssue issue)
    {
        var severityText = issue.Severity == ValidationSeverity.Warning ? "Warning" : "Error";
        var details = string.IsNullOrWhiteSpace(issue.Description) ? "Validation issue" : issue.Description.Trim();
        if (string.IsNullOrWhiteSpace(issue.Hint))
        {
            return $"[{severityText}] {details}";
        }

        return $"[{severityText}] {details} Fix: {issue.Hint.Trim()}";
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

