namespace StoryboardDesigner.App.SmokeTests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class DeepUiFactAttribute : FactAttribute
{
    private const string RunDeepUiEnvironmentVariable = "STORYBOARD_DESIGNER_RUN_DEEP_UI";

    public DeepUiFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunDeepUiEnvironmentVariable), "1", StringComparison.OrdinalIgnoreCase))
        {
            Skip = $"Opt-in deep UI test. Set {RunDeepUiEnvironmentVariable}=1 to run.";
        }
    }
}
