namespace StoryboardDesigner.App.Orchestration.Diagnostics;

public sealed record ShellDiagnosticMessage(
    DateTimeOffset TimestampUtc,
    ShellDiagnosticSeverity Severity,
    string Source,
    string Message);