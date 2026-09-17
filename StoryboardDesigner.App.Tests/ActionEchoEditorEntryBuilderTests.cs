using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class ActionEchoEditorEntryBuilderTests
{
    [Fact]
    public void BuildEntries_IncludesAllSupportedTokensAndCanonicalizesSupportedSourceKeys()
    {
        var source = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["success"] = "mapped success",
            ["CustomFutureCode"] = "future"
        };

        var entries = ActionEchoEditorEntryBuilder.BuildEntries(CommandActionType.SetGameProperty, source);

        Assert.Contains(entries, entry => entry.Token == "Success" && entry.IsSupported && entry.Script == "mapped success");
        Assert.Contains(entries, entry => entry.Token == "Failure" && entry.IsSupported && entry.Script == string.Empty);
        Assert.Contains(entries, entry => entry.Token == "CustomFutureCode" && !entry.IsSupported && entry.Script == "future");
    }

    [Fact]
    public void ToOutcomeMessageMap_PreservesBlankEntries()
    {
        var entries = new List<ActionEchoEditorEntry>
        {
            new()
            {
                Token = "Success",
                IsSupported = true,
                OutcomeLabel = "Success",
                Script = string.Empty
            },
            new()
            {
                Token = "Failure",
                IsSupported = true,
                OutcomeLabel = "Failure",
                Script = "mapped failure"
            }
        };

        var map = ActionEchoEditorEntryBuilder.ToOutcomeMessageMap(entries);

        Assert.True(map.ContainsKey("Success"));
        Assert.Equal(string.Empty, map["Success"]);
        Assert.Equal("mapped failure", map["Failure"]);
    }

    [Fact]
    public void BuildEntries_SetsSupportStatusAndHintForUnsupportedEntries()
    {
        var source = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyOnlyCode"] = "legacy"
        };

        var entries = ActionEchoEditorEntryBuilder.BuildEntries(CommandActionType.SetGameProperty, source);
        var unsupported = Assert.Single(entries, static entry => !entry.IsSupported);

        Assert.Equal("LegacyOnlyCode", unsupported.Token);
        Assert.Equal("Unsupported (inert at runtime)", unsupported.SupportStatus);
        Assert.Contains("ignored by runtime execution", unsupported.SupportHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScriptPreview_ReturnsFirstLineOnly_ForMultiLineScript()
    {
        var entry = new ActionEchoEditorEntry
        {
            Token = "Success",
            IsSupported = true,
            OutcomeLabel = "Success",
            Script = "First line\r\nSecond line\r\nThird line"
        };

        Assert.Equal("First line", entry.ScriptPreview);
    }

    [Fact]
    public void ScriptPreview_TrimsLeadingAndTrailingWhitespaceOnFirstLine()
    {
        var entry = new ActionEchoEditorEntry
        {
            Token = "Failure",
            IsSupported = true,
            OutcomeLabel = "Failure",
            Script = "   preview text   \nnext"
        };

        Assert.Equal("preview text", entry.ScriptPreview);
    }
}
