namespace StoryboardDesigner.App.Tests;

internal static class StaTestThreadRunner
{
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(30);

    public static void StartAndWait(Thread thread, string operation)
    {
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        if (!thread.Join(CompletionTimeout))
        {
            throw new TimeoutException(
                $"STA test operation '{operation}' did not finish within {CompletionTimeout.TotalSeconds:0} seconds.");
        }
    }
}
