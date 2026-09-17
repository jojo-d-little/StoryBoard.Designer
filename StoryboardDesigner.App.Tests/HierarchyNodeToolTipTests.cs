using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class HierarchyNodeToolTipTests
{
    [Fact]
    public void ValidationSummaryToolTip_ForNonPropertyNodeWithoutIssues_IsEmpty()
    {
        var project = new ProjectModel();
        var node = new ProjectRootNodeViewModel(project);

        Assert.Equal(string.Empty, node.ValidationSummaryToolTip);
        Assert.False(node.ShouldShowValidationSummaryToolTip);
    }

    [Fact]
    public void ValidationSummaryToolTip_ForNonPropertyNodeWithIssues_IncludesValidationDetails()
    {
        var project = new ProjectModel();
        var node = new ProjectRootNodeViewModel(project);
        node.SetValidationCounts(directErrorCount: 1, directWarningCount: 0, descendantErrorCount: 0, descendantWarningCount: 2);
        node.SetDirectValidationIssueMessages(["Root is missing required value"]);

        var tooltip = node.ValidationSummaryToolTip;

        Assert.True(node.ShouldShowValidationSummaryToolTip);
        Assert.Contains("Direct issues: 1 error(s), 0 warning(s).", tooltip, StringComparison.Ordinal);
        Assert.Contains("Descendant issues: 0 error(s), 2 warning(s).", tooltip, StringComparison.Ordinal);
        Assert.Contains("Root is missing required value", tooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidationSummaryToolTip_ForNonPropertyNodeWithWarningsOnly_ShowsToolTip()
    {
        var project = new ProjectModel();
        var node = new ProjectRootNodeViewModel(project);
        node.SetValidationCounts(directErrorCount: 0, directWarningCount: 1, descendantErrorCount: 0, descendantWarningCount: 2);
        node.SetDirectValidationIssueMessages(["Warning details"]);

        Assert.True(node.ShouldShowValidationSummaryToolTip);
        Assert.Contains("Direct issues: 0 error(s), 1 warning(s).", node.ValidationSummaryToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidationSummaryToolTip_ForNonPropertyNodeWithDescendantOnlyIssues_DoesNotShowToolTip()
    {
        var project = new ProjectModel();
        var node = new ProjectRootNodeViewModel(project);
        node.SetValidationCounts(directErrorCount: 0, directWarningCount: 0, descendantErrorCount: 1, descendantWarningCount: 1);

        Assert.False(node.ShouldShowValidationSummaryToolTip);
    }

    [Theory]
    [InlineData(GamePropertyValueRestriction.TrueFalse, "true")]
    [InlineData(GamePropertyValueRestriction.Numeric, "42")]
    [InlineData(GamePropertyValueRestriction.Unrestricted, "")]
    public void ValidationSummaryToolTip_ForGamePropertyWithoutIssues_IsOnlyTheValue(
        GamePropertyValueRestriction restriction,
        string currentValue)
    {
        var variable = new GamePropertyDefinition
        {
            Name = "sampleProperty",
            DefaultValue = currentValue,
            ValueRestriction = restriction
        };
        var container = new GamePropertiesContainerNodeViewModel(PropertyResolutionScope.Object);
        var node = new GamePropertyNodeViewModel(variable, PropertyResolutionScope.Object, container);

        var tooltip = node.ValidationSummaryToolTip;

        Assert.True(node.ShouldShowValidationSummaryToolTip);
        Assert.Equal(currentValue, tooltip);
    }

    [Fact]
    public void ValidationSummaryToolTip_ForGamePropertyWithIssues_IncludesValidationDetails()
    {
        var variable = new GamePropertyDefinition
        {
            Name = "isPassable",
            DefaultValue = "true",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse
        };
        var container = new GamePropertiesContainerNodeViewModel(PropertyResolutionScope.Object);
        var node = new GamePropertyNodeViewModel(variable, PropertyResolutionScope.Object, container);
        node.SetValidationCounts(directErrorCount: 1, directWarningCount: 1, descendantErrorCount: 0, descendantWarningCount: 0);
        node.SetDirectValidationIssueMessages(["Value must be true/false"]);

        var tooltip = node.ValidationSummaryToolTip;

        Assert.Contains("true", tooltip, StringComparison.Ordinal);
        Assert.Contains("Direct issues: 1 error(s), 1 warning(s).", tooltip, StringComparison.Ordinal);
        Assert.Contains("Value must be true/false", tooltip, StringComparison.Ordinal);
    }
}
