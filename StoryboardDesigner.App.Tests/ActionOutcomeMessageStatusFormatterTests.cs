using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class ActionOutcomeMessageStatusFormatterTests
{
    [Fact]
    public void BuildStatuses_UsesCanonicalSupportedTokensAndCountsDefinedEntries()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.SetGameProperty
        };

        action.OutcomeMessageMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["failure"] = "mapped failure",
            ["CustomFutureCode"] = "future"
        };

        var statuses = ActionOutcomeMessageStatusFormatter.BuildStatuses(action);

        Assert.Equal(3, statuses.Count);
        Assert.Contains(statuses, status => status.Token == "Success" && !status.IsDefined && status.Script == string.Empty);
        Assert.Contains(statuses, status => status.Token == "Failure" && status.IsDefined && status.Script == "mapped failure");
        Assert.Contains(statuses, status => status.Token == "SetFailed" && !status.IsDefined);

        var compact = ActionOutcomeMessageStatusFormatter.BuildCompactStatusText(action);
        Assert.Equal("(echo scripts defined: 1/3, undefined: 2)", compact);
    }

    [Fact]
    public void BuildStatuses_RespectsExplicitBlankSupportedEntry()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.SetGameProperty
        };

        action.OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Success"] = string.Empty
        };

        var statuses = ActionOutcomeMessageStatusFormatter.BuildStatuses(action);

        Assert.Contains(statuses, status => status.Token == "Success" && !status.IsDefined && status.Script == string.Empty);
        Assert.Contains(statuses, status => status.Token == "Failure" && !status.IsDefined);

        var tooltip = ActionOutcomeMessageStatusFormatter.BuildTooltipText(action);
        Assert.Contains("Success: undefined", tooltip);
        Assert.Contains("Failure: undefined", tooltip);
    }

    [Fact]
    public void BuildTooltipText_IncludesUnsupportedEntryWarningWhenPresent()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.SetGameProperty
        };

        action.OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyOnlyCode"] = "legacy script"
        };

        var tooltip = ActionOutcomeMessageStatusFormatter.BuildTooltipText(action);

        Assert.Contains("Unsupported preserved entry: 1 (ignored at runtime)", tooltip);
    }

    [Fact]
    public void BuildCompactStatusText_ReflectsTypedSetFlagTokens()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.SetFlag
        };

        action.OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SetFailed"] = "failed"
        };

        var compact = ActionOutcomeMessageStatusFormatter.BuildCompactStatusText(action);

        Assert.Equal("(echo scripts defined: 1/3, undefined: 2)", compact);
    }

    [Fact]
    public void BuildTooltipText_ListsTypedCheckGamePropertyTokens()
    {
        var action = new CommandAction
        {
            ActionType = CommandActionType.CheckGameProperty
        };

        var tooltip = ActionOutcomeMessageStatusFormatter.BuildTooltipText(action);

        Assert.Contains("PropertyMissing: undefined", tooltip);
        Assert.Contains("ValueMismatch: undefined", tooltip);
    }
}
