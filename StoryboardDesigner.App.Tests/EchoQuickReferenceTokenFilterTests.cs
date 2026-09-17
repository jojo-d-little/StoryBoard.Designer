using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class EchoQuickReferenceTokenFilterTests
{
    [Fact]
    public void FilterQuickBraceTokens_IncludesManifestAnchorRootedTokens()
    {
        var source = new[]
        {
            "self.name",
            "currentAction.resultCode",
            "currentPlanet.name",
            "currentCountry.nameInGame",
            "activePlayer.name",
            "currentRoom.Name",
            "lantern.description",
            "room.temperature"
        };

        var filtered = EchoQuickReferenceTokenFilter.FilterQuickBraceTokens(source);

        Assert.Equal(new[]
        {
            "currentAction.resultCode",
            "self.name",
            "activePlayer.name",
            "currentCountry.nameInGame",
            "currentPlanet.name",
            "currentRoom.Name"
        }, filtered);
    }

    [Fact]
    public void FilterQuickBraceTokens_DeduplicatesAndSortsWithSelfFirst()
    {
        var source = new[]
        {
            "currentAction.alpha",
            "self.name",
            "Self.Name"
        };

        var filtered = EchoQuickReferenceTokenFilter.FilterQuickBraceTokens(source);

        Assert.Equal(new[] { "currentAction.alpha", "self.name" }, filtered);
    }

    [Fact]
    public void FilterQuickBraceTokens_EmptyInput_ReturnsEmpty()
    {
        var filtered = EchoQuickReferenceTokenFilter.FilterQuickBraceTokens(Array.Empty<string>());

        Assert.Empty(filtered);
    }
}
