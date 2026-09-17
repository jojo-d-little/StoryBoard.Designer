using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class VariableReferenceSyntaxTests
{
    [Fact]
    public void BuildAnchoredReference_WithSubPropertyAndVariable_ComposesExpectedSyntax()
    {
        var result = VariableReferenceSyntax.BuildAnchoredReference("currentCommand", "primaryCommandObject", "isBent");

        Assert.Equal("currentCommand::primaryCommandObject.isBent", result);
    }

    [Fact]
    public void BuildAnchoredReference_WithAnchorAndPath_ComposesExpectedSyntax()
    {
        var result = VariableReferenceSyntax.BuildAnchoredReference("currentAction", "procedure.id");

        Assert.Equal("currentAction::procedure.id", result);
    }

    [Fact]
    public void BuildAnchoredReference_WithoutAnchor_ReturnsReferencePath()
    {
        var result = VariableReferenceSyntax.BuildAnchoredReference("", "primaryCommandObject.isBent");

        Assert.Equal("primaryCommandObject.isBent", result);
    }

    [Fact]
    public void TryParseAnchoredReference_ReturnsAnchorAndPath_WhenSyntaxIsAnchored()
    {
        var parsed = VariableReferenceSyntax.TryParseAnchoredReference(
            "currentCommand::primaryCommandObject.isBent",
            out var anchor,
            out var path);

        Assert.True(parsed);
        Assert.Equal("currentCommand", anchor);
        Assert.Equal("primaryCommandObject.isBent", path);
    }

    [Fact]
    public void TryParseAnchoredReference_ReturnsFalse_ForLegacyDotSyntax()
    {
        var parsed = VariableReferenceSyntax.TryParseAnchoredReference(
            "primaryCommandObject.isBent",
            out var anchor,
            out var path);

        Assert.False(parsed);
        Assert.Equal(string.Empty, anchor);
        Assert.Equal(string.Empty, path);
    }
}
