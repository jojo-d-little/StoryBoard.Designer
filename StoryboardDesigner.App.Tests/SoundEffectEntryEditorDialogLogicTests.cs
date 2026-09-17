using StoryboardDesigner.App.Views;

namespace StoryboardDesigner.App.Tests;

public class SoundEffectEntryEditorDialogLogicTests
{
    [Theory]
    [InlineData("None", "None")]
    [InlineData("RepeatCount", "RepeatCount")]
    [InlineData("RepeatForDuration", "RepeatForDuration")]
    [InlineData("UntilCanceled", "UntilCanceled")]
    [InlineData("UntilCancelled", "UntilCanceled")]
    [InlineData("UntilCancel", "UntilCanceled")]
    [InlineData("Count", "RepeatCount")]
    [InlineData("Duration", "RepeatForDuration")]
    [InlineData("Loop", "RepeatForDuration")]
    [InlineData("", "None")]
    public void NormalizeRepeatMode_NormalizesLegacyTokens(string input, string expected)
    {
        var actual = SoundEffectEntryEditorDialog.NormalizeRepeatMode(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolveRepeatModeEditorState_None_DisablesRepeatFields()
    {
        var state = SoundEffectEntryEditorDialog.ResolveRepeatModeEditorState("None");

        Assert.False(state.UsesRepeatSettings);
        Assert.False(state.UsesCount);
        Assert.False(state.UsesDuration);
        Assert.False(state.UsesIntervalAndCooldown);
    }

    [Fact]
    public void ResolveRepeatModeEditorState_RepeatCount_EnablesCountAndSharedRepeatFields()
    {
        var state = SoundEffectEntryEditorDialog.ResolveRepeatModeEditorState("RepeatCount");

        Assert.True(state.UsesRepeatSettings);
        Assert.True(state.UsesCount);
        Assert.False(state.UsesDuration);
        Assert.True(state.UsesIntervalAndCooldown);
    }

    [Fact]
    public void ResolveRepeatModeEditorState_RepeatForDuration_EnablesDurationAndSharedRepeatFields()
    {
        var state = SoundEffectEntryEditorDialog.ResolveRepeatModeEditorState("RepeatForDuration");

        Assert.True(state.UsesRepeatSettings);
        Assert.False(state.UsesCount);
        Assert.True(state.UsesDuration);
        Assert.True(state.UsesIntervalAndCooldown);
    }

    [Fact]
    public void ResolveRepeatModeEditorState_UntilCanceled_EnablesSharedRepeatFieldsWithoutCountOrDuration()
    {
        var state = SoundEffectEntryEditorDialog.ResolveRepeatModeEditorState("UntilCanceled");

        Assert.True(state.UsesRepeatSettings);
        Assert.False(state.UsesCount);
        Assert.False(state.UsesDuration);
        Assert.True(state.UsesIntervalAndCooldown);
    }

    [Fact]
    public void NormalizeRepeatFieldsForSave_None_ClearsRepeatFields()
    {
        int? repeatCount = 3;
        int? repeatDurationMs = 2000;
        int? repeatIntervalMs = 150;
        int? repeatCooldownMs = 400;

        SoundEffectEntryEditorDialog.NormalizeRepeatFieldsForSave(
            "None",
            ref repeatCount,
            ref repeatDurationMs,
            ref repeatIntervalMs,
            ref repeatCooldownMs);

        Assert.Null(repeatCount);
        Assert.Null(repeatDurationMs);
        Assert.Null(repeatIntervalMs);
        Assert.Null(repeatCooldownMs);
    }

    [Fact]
    public void NormalizeRepeatFieldsForSave_RepeatCount_ClearsOnlyDuration()
    {
        int? repeatCount = 3;
        int? repeatDurationMs = 2000;
        int? repeatIntervalMs = 150;
        int? repeatCooldownMs = 400;

        SoundEffectEntryEditorDialog.NormalizeRepeatFieldsForSave(
            "RepeatCount",
            ref repeatCount,
            ref repeatDurationMs,
            ref repeatIntervalMs,
            ref repeatCooldownMs);

        Assert.Equal(3, repeatCount);
        Assert.Null(repeatDurationMs);
        Assert.Equal(150, repeatIntervalMs);
        Assert.Equal(400, repeatCooldownMs);
    }

    [Fact]
    public void NormalizeRepeatFieldsForSave_RepeatForDuration_ClearsOnlyCount()
    {
        int? repeatCount = 3;
        int? repeatDurationMs = 2000;
        int? repeatIntervalMs = 150;
        int? repeatCooldownMs = 400;

        SoundEffectEntryEditorDialog.NormalizeRepeatFieldsForSave(
            "RepeatForDuration",
            ref repeatCount,
            ref repeatDurationMs,
            ref repeatIntervalMs,
            ref repeatCooldownMs);

        Assert.Null(repeatCount);
        Assert.Equal(2000, repeatDurationMs);
        Assert.Equal(150, repeatIntervalMs);
        Assert.Equal(400, repeatCooldownMs);
    }

    [Fact]
    public void NormalizeRepeatFieldsForSave_UntilCanceled_ClearsCountAndDuration()
    {
        int? repeatCount = 3;
        int? repeatDurationMs = 2000;
        int? repeatIntervalMs = 150;
        int? repeatCooldownMs = 400;

        SoundEffectEntryEditorDialog.NormalizeRepeatFieldsForSave(
            "UntilCanceled",
            ref repeatCount,
            ref repeatDurationMs,
            ref repeatIntervalMs,
            ref repeatCooldownMs);

        Assert.Null(repeatCount);
        Assert.Null(repeatDurationMs);
        Assert.Equal(150, repeatIntervalMs);
        Assert.Equal(400, repeatCooldownMs);
    }

    [Fact]
    public void BuildCategorySuggestions_AddsDefault_AndDedupesCaseInsensitive()
    {
        var values = SoundEffectEntryEditorDialog.BuildCategorySuggestions(
            "ambient",
            ["General", "Ambient", "ambient", "UI"]);

        Assert.NotEmpty(values);
        Assert.Equal("General", values[0]);
        Assert.Contains(values, value => string.Equals(value, "Ambient", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(values, value => string.Equals(value, "UI", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, values.Count(value => string.Equals(value, "Ambient", StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData(null, "General")]
    [InlineData("", "General")]
    [InlineData("   ", "General")]
    [InlineData(" Ambient ", "Ambient")]
    public void ResolveCategoryForSave_UsesRequiredDefaultAndTrim(string? input, string expected)
    {
        var actual = SoundEffectEntryEditorDialog.ResolveCategoryForSave(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildCategorySuggestions_WhenNoInput_StillContainsGeneral()
    {
        var values = SoundEffectEntryEditorDialog.BuildCategorySuggestions(null, null);
        var first = Assert.Single(values);
        Assert.Equal("General", first);
    }

    [Theory]
    [InlineData("Neutral", "Info")]
    [InlineData("Active", "Info")]
    [InlineData("Success", "Info")]
    [InlineData("Warning", "Warning")]
    [InlineData("Error", "Error")]
    [InlineData("unknown", "Info")]
    public void MapPreviewSeverityToDiagnosticLevel_MapsExpectedLevels(string severity, string expected)
    {
        var actual = SoundEffectEntryEditorDialog.MapPreviewSeverityToDiagnosticLevel(severity);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolvePreviewIterationFade_RepeatCount_FirstPassIsFadeInOnly_LastPassIsFadeOutOnly()
    {
        var first = SoundEffectEntryEditorDialog.ResolvePreviewIterationFade("RepeatCount", iteration: 1, repeatCount: 2, fadeInMs: 3000, fadeOutMs: 3000);
        var last = SoundEffectEntryEditorDialog.ResolvePreviewIterationFade("RepeatCount", iteration: 2, repeatCount: 2, fadeInMs: 3000, fadeOutMs: 3000);

        Assert.Equal((3000, 0), first);
        Assert.Equal((0, 3000), last);
    }

    [Fact]
    public void ResolvePreviewIterationFade_RepeatForDuration_OnlyFirstPassFadesIn_AndNoPassFadesOut()
    {
        var first = SoundEffectEntryEditorDialog.ResolvePreviewIterationFade("RepeatForDuration", iteration: 1, repeatCount: 99, fadeInMs: 3000, fadeOutMs: 3000);
        var later = SoundEffectEntryEditorDialog.ResolvePreviewIterationFade("RepeatForDuration", iteration: 3, repeatCount: 99, fadeInMs: 3000, fadeOutMs: 3000);

        Assert.Equal((3000, 0), first);
        Assert.Equal((0, 0), later);
    }

    [Fact]
    public void ResolvePreviewIterationFade_None_UsesConfiguredFadeInAndFadeOut()
    {
        var single = SoundEffectEntryEditorDialog.ResolvePreviewIterationFade("None", iteration: 1, repeatCount: 1, fadeInMs: 3000, fadeOutMs: 3000);
        Assert.Equal((3000, 3000), single);
    }

    [Fact]
    public void ResolvePreviewIterationFade_UntilCanceled_OnlyFirstPassFadesIn_AndNoPassFadesOut()
    {
        var first = SoundEffectEntryEditorDialog.ResolvePreviewIterationFade("UntilCanceled", iteration: 1, repeatCount: 99, fadeInMs: 3000, fadeOutMs: 3000);
        var later = SoundEffectEntryEditorDialog.ResolvePreviewIterationFade("UntilCanceled", iteration: 4, repeatCount: 99, fadeInMs: 3000, fadeOutMs: 3000);

        Assert.Equal((3000, 0), first);
        Assert.Equal((0, 0), later);
    }

    [Fact]
    public void ResolvePreviewGapMs_WhenCooldownIsUnset_ReturnsZeroGap()
    {
        Assert.Equal(0, SoundEffectEntryEditorDialog.ResolvePreviewGapMs(repeatIntervalMs: 0, repeatCooldownMs: 0, completedIterations: 1));
        Assert.Equal(0, SoundEffectEntryEditorDialog.ResolvePreviewGapMs(repeatIntervalMs: 250, repeatCooldownMs: 0, completedIterations: 1));
        Assert.Equal(0, SoundEffectEntryEditorDialog.ResolvePreviewGapMs(repeatIntervalMs: 250, repeatCooldownMs: 0, completedIterations: 3));
    }

    [Fact]
    public void ResolvePreviewGapMs_WhenCooldownIsSet_UsesIntervalThenCooldown()
    {
        Assert.Equal(150, SoundEffectEntryEditorDialog.ResolvePreviewGapMs(repeatIntervalMs: 150, repeatCooldownMs: 400, completedIterations: 1));
        Assert.Equal(400, SoundEffectEntryEditorDialog.ResolvePreviewGapMs(repeatIntervalMs: 150, repeatCooldownMs: 400, completedIterations: 2));
    }
}
