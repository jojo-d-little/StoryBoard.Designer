using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class ActionOutcomeMessageInlineHintFormatterTests
{
    [Fact]
    public void Build_ComposesStatusAndTooltipsFromFormatterInputs()
    {
        const string compact = "(echo scripts defined: 1/3, undefined: 2)";
        const string detail = "Success: defined\nFailure: undefined\nSetFailed: undefined";

        var hints = ActionOutcomeMessageInlineHintFormatter.Build(compact, detail);

        Assert.Equal("Mapped echoes (echo scripts defined: 1/3, undefined: 2)", hints.StatusText);
        Assert.Contains("Success: defined", hints.StatusToolTip);
        Assert.Contains("Use 'Edit Outcome Echoes...' to configure per-result-code scripts.", hints.StatusToolTip);
        Assert.Equal("Open per-result-code mapping editor. Current status: (echo scripts defined: 1/3, undefined: 2)", hints.ButtonToolTip);
    }

    [Fact]
    public void Build_ToleratesNullInputs()
    {
        var hints = ActionOutcomeMessageInlineHintFormatter.Build(compactStatusText: null!, detailedToolTip: null!);

        Assert.Equal("Mapped echoes ", hints.StatusText);
        Assert.Contains("Use 'Edit Outcome Echoes...' to configure per-result-code scripts.", hints.StatusToolTip);
        Assert.Equal("Open per-result-code mapping editor. Current status: ", hints.ButtonToolTip);
    }
}
