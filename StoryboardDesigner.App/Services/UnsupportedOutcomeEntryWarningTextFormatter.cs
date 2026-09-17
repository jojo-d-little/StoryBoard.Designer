namespace StoryboardDesigner.App.Services;

public static class UnsupportedOutcomeEntryWarningTextFormatter
{
    public static string BuildBannerText(int unsupportedCount)
    {
        if (unsupportedCount <= 0)
        {
            return string.Empty;
        }

        var noun = unsupportedCount == 1 ? "entry" : "entries";
        return $"{unsupportedCount} unsupported result-code {noun} will be preserved in saved data but ignored by runtime execution.";
    }

    public static string BuildTooltipSummaryLine(int unsupportedCount)
    {
        if (unsupportedCount <= 0)
        {
            return string.Empty;
        }

        var noun = unsupportedCount == 1 ? "entry" : "entries";
        return $"Unsupported preserved {noun}: {unsupportedCount} (ignored at runtime)";
    }
}
