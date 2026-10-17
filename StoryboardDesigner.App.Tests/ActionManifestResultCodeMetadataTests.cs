using Storyboard.GameEngine.Configuration.ActionManifest;

namespace StoryboardDesigner.App.Tests;

public sealed class ActionManifestResultCodeMetadataTests
{
    private static ActionManifest LoadManifest() =>
        ActionManifestLoader.Load(Path.Combine(AppContext.BaseDirectory, "Config"));

    [Fact]
    public void EveryIndexedActionHasValidDistinctResultCodeMetadata()
    {
        var manifest = LoadManifest();

        Assert.NotEmpty(manifest.Actions);
        foreach (var action in manifest.Actions.Values)
        {
            Assert.Equal(action.ResultCodes.Count, action.ResultCodes.Select(static code => code.Token).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(action.ResultCodes, code =>
            {
                Assert.False(string.IsNullOrWhiteSpace(code.Token));
                Assert.False(string.IsNullOrWhiteSpace(code.Label));
                Assert.Contains(code.Outcome, new[] { "Success", "Failure" });
            });
        }
    }

    [Theory]
    [InlineData("NavigateDirection", "Moved", "Success")]
    [InlineData("NavigateToAdjacent", "TraversalAmbiguous", "Failure")]
    [InlineData("BuildCompositeByParts", "NoMatchingRecipe", "Failure")]
    [InlineData("BuildCompositeByTarget", "SpecifyTarget", "Failure")]
    [InlineData("BreakCompositeItem", "MutationFailed", "Failure")]
    [InlineData("PutObjectInContainer", "CapacityExceeded", "Failure")]
    [InlineData("RemoveObjectFromContainer", "RemoveFailed", "Failure")]
    [InlineData("SetGameProperty", "SetFailed", "Failure")]
    [InlineData("CheckGameProperty", "ValueMismatch", "Failure")]
    [InlineData("SetFlag", "SetFailed", "Failure")]
    public void ResultCodeEntriesPreserveTokenAndOutcome(string actionType, string token, string outcome)
    {
        var action = LoadManifest().GetAction(actionType);
        var resultCode = Assert.Single(action.ResultCodes, code => code.Token == token);

        Assert.Equal(outcome, resultCode.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(resultCode.Label));
    }

    [Fact]
    public void NavigateDirectionIncludesItsAuthoringChoices()
    {
        var action = LoadManifest().GetAction("NavigateDirection");
        var tokens = action.ResultCodes.Select(static code => code.Token).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("Moved", tokens);
        Assert.Contains("DirectionRequired", tokens);
        Assert.Contains("NoExitInDirection", tokens);
        Assert.Contains("NoTraversableExit", tokens);
        Assert.Contains("AmbiguousTraversal", tokens);
        Assert.Contains("TraversalBlocked", tokens);
        Assert.Contains("MoveFailed", tokens);
    }
}
