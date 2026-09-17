namespace StoryboardDesigner.App.Services;

public sealed record ActionOutcomeMessageInlineHints(string StatusText, string StatusToolTip, string ButtonToolTip);

public static class ActionOutcomeMessageInlineHintFormatter
{
    public static ActionOutcomeMessageInlineHints Build(string compactStatusText, string detailedToolTip)
    {
        var compact = compactStatusText ?? string.Empty;
        var detail = detailedToolTip ?? string.Empty;

        var statusText = $"Mapped echoes {compact}";
        var statusToolTip = $"{detail}{Environment.NewLine}{Environment.NewLine}Use 'Edit Outcome Echoes...' to configure per-result-code scripts.";
        var buttonToolTip = "Open per-result-code mapping editor. Current status: " + compact;

        return new ActionOutcomeMessageInlineHints(statusText, statusToolTip, buttonToolTip);
    }
}
