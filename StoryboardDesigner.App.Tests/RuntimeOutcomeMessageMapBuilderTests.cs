namespace StoryboardDesigner.App.Tests;

public sealed class RuntimeOutcomeMessageMapBuilderTests
{
    [Fact]
    public void Create_BuildsBaselineMap_ForSupportedActionType()
    {
        var map = RuntimeOutcomeMessageMapBuilder.Create(
            CommandActionType.EchoMessage,
            successScript: "success script",
            failureScript: "failure script");

        Assert.Equal(2, map.Count);
        Assert.Equal("success script", map["Success"]);
        Assert.Equal("failure script", map["Failure"]);
    }

    [Fact]
    public void Create_SeedsBlankEntries_ForUnconfiguredSupportedTokens()
    {
        var map = RuntimeOutcomeMessageMapBuilder.Create(
            CommandActionType.NavigateDirection,
            successScript: string.Empty,
            failureScript: string.Empty,
            actionSpecificScripts: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Moved"] = "You move.",
                ["UnknownToken"] = "ignored"
            });

        Assert.Equal("You move.", map["Moved"]);
        Assert.Equal(string.Empty, map["NoExitInDirection"]);
        Assert.Equal(string.Empty, map["MoveFailed"]);
        Assert.False(map.ContainsKey("UnknownToken"));
    }

    [Fact]
    public void Create_IsCaseInsensitive_ForActionSpecificTokenOverrides()
    {
        var map = RuntimeOutcomeMessageMapBuilder.Create(
            CommandActionType.NavigateDirection,
            successScript: "success",
            failureScript: "failure",
            actionSpecificScripts: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mOvEd"] = "normalized move"
            });

        Assert.Equal("normalized move", map["Moved"]);
    }
}
