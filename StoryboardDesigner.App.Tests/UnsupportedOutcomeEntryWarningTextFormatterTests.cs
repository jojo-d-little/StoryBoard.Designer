using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class UnsupportedOutcomeEntryWarningTextFormatterTests
{
    [Fact]
    public void BuildBannerText_ReturnsEmpty_ForNonPositiveCounts()
    {
        Assert.Equal(string.Empty, UnsupportedOutcomeEntryWarningTextFormatter.BuildBannerText(0));
        Assert.Equal(string.Empty, UnsupportedOutcomeEntryWarningTextFormatter.BuildBannerText(-1));
    }

    [Fact]
    public void BuildBannerText_UsesSingularAndPluralForms()
    {
        var singular = UnsupportedOutcomeEntryWarningTextFormatter.BuildBannerText(1);
        var plural = UnsupportedOutcomeEntryWarningTextFormatter.BuildBannerText(2);

        Assert.Contains("1 unsupported result-code entry", singular, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2 unsupported result-code entries", plural, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ignored by runtime execution", singular, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildTooltipSummaryLine_ReturnsEmpty_ForNonPositiveCounts()
    {
        Assert.Equal(string.Empty, UnsupportedOutcomeEntryWarningTextFormatter.BuildTooltipSummaryLine(0));
    }

    [Fact]
    public void BuildTooltipSummaryLine_UsesExpectedFormat()
    {
        var singular = UnsupportedOutcomeEntryWarningTextFormatter.BuildTooltipSummaryLine(1);
        var plural = UnsupportedOutcomeEntryWarningTextFormatter.BuildTooltipSummaryLine(3);

        Assert.Equal("Unsupported preserved entry: 1 (ignored at runtime)", singular);
        Assert.Equal("Unsupported preserved entries: 3 (ignored at runtime)", plural);
    }
}
