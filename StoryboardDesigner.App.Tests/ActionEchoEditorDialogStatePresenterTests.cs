using System.Collections.ObjectModel;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class ActionEchoEditorDialogStatePresenterTests
{
    [Fact]
    public void Build_NoSelectionAndNoUnsupported_ButtonsDisabledAndWarningHidden()
    {
        var entries = new List<ActionEchoEditorEntry>
        {
            new()
            {
                Token = "Success",
                IsSupported = true,
                OutcomeLabel = "Success",
                Script = string.Empty
            }
        }.AsReadOnly();

        var state = ActionEchoEditorDialogStatePresenter.Build(entries, selectedEntry: null);

        Assert.False(state.HasSelection);
        Assert.False(state.IsUnsupportedWarningVisible);
        Assert.Equal(string.Empty, state.UnsupportedWarningText);
    }

    [Fact]
    public void Build_SelectionPresent_HasSelectionTrue()
    {
        var selected = new ActionEchoEditorEntry
        {
            Token = "Failure",
            IsSupported = true,
            OutcomeLabel = "Failure",
            Script = "script"
        };

        var entries = new List<ActionEchoEditorEntry>
        {
            selected
        }.AsReadOnly();

        var state = ActionEchoEditorDialogStatePresenter.Build(entries, selected);

        Assert.True(state.HasSelection);
        Assert.False(state.IsUnsupportedWarningVisible);
    }

    [Fact]
    public void Build_UnsupportedEntriesPresent_ShowsWarningWithCount()
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
                Token = "LegacyOnlyCode",
                IsSupported = false,
                OutcomeLabel = "Unsupported",
                Script = "legacy"
            },
            new()
            {
                Token = "LegacyOnlyCode2",
                IsSupported = false,
                OutcomeLabel = "Unsupported",
                Script = "legacy"
            }
        }.AsReadOnly();

        var state = ActionEchoEditorDialogStatePresenter.Build(entries, selectedEntry: null);

        Assert.True(state.IsUnsupportedWarningVisible);
        Assert.Contains("2 unsupported result-code entries", state.UnsupportedWarningText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ignored by runtime execution", state.UnsupportedWarningText, StringComparison.OrdinalIgnoreCase);
    }
}
