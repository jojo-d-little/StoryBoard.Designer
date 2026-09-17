using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class ActionOutcomeMessageTextFormatterTests
{
    [Fact]
    public void BuildNoResultCodesText_ReturnsExpectedString()
    {
        Assert.Equal("(no result codes)", ActionOutcomeMessageTextFormatter.BuildNoResultCodesText());
    }

    [Fact]
    public void BuildCompactStatusText_ReturnsExpectedDefinedAndUndefinedCounts()
    {
        var text = ActionOutcomeMessageTextFormatter.BuildCompactStatusText(definedCount: 2, totalCount: 5);

        Assert.Equal("(echo scripts defined: 2/5, undefined: 3)", text);
    }

    [Fact]
    public void BuildCompactStatusText_ClampsDefinedCountToValidRange()
    {
        var low = ActionOutcomeMessageTextFormatter.BuildCompactStatusText(definedCount: -4, totalCount: 3);
        var high = ActionOutcomeMessageTextFormatter.BuildCompactStatusText(definedCount: 9, totalCount: 3);

        Assert.Equal("(echo scripts defined: 0/3, undefined: 3)", low);
        Assert.Equal("(echo scripts defined: 3/3, undefined: 0)", high);
    }

    [Fact]
    public void BuildCompactStatusText_UsesNoResultCodesText_WhenTotalIsNonPositive()
    {
        Assert.Equal("(no result codes)", ActionOutcomeMessageTextFormatter.BuildCompactStatusText(definedCount: 1, totalCount: 0));
    }
}
