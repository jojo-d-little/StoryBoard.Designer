namespace StoryboardDesigner.App.Services;

public static class ActionOutcomeMessageTextFormatter
{
    public static string BuildCompactStatusText(int definedCount, int totalCount)
    {
        if (totalCount <= 0)
        {
            return BuildNoResultCodesText();
        }

        var safeDefinedCount = Math.Clamp(definedCount, 0, totalCount);
        var undefinedCount = totalCount - safeDefinedCount;
        return $"(echo scripts defined: {safeDefinedCount}/{totalCount}, undefined: {undefinedCount})";
    }

    public static string BuildNoResultCodesText()
    {
        return "(no result codes)";
    }
}
