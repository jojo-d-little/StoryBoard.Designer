using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class ActionScriptEditorDiagnosticsAnalyzerTests
{
    [Fact]
    public void Analyze_UnknownReference_IsWarningNotError()
    {
        var script = "ECHO {unknownToken}";

        var result = ActionScriptEditorDiagnosticsAnalyzer.Analyze(script, new[] { "self.name" });

        Assert.Empty(result.Errors);
        Assert.Contains(result.Warnings, warning => warning.Contains("Unknown reference", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_MalformedIndexedReference_AddsWarning()
    {
        var script = "ECHO {currentAction.parts[foo]}";

        var result = ActionScriptEditorDiagnosticsAnalyzer.Analyze(script, new[] { "currentAction.parts[0]" });

        Assert.Contains(result.Warnings, warning => warning.Contains("Malformed indexed reference", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_DoubleBraces_RemainsBlockingError()
    {
        var script = "ECHO {{self.name}}";

        var result = ActionScriptEditorDiagnosticsAnalyzer.Analyze(script, new[] { "self.name" });

        Assert.Contains(result.Errors, error => error.Contains("Double braces", StringComparison.OrdinalIgnoreCase));
    }
}
