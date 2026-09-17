using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class EchoTokenMetadataProviderTests
{
    [Fact]
    public void GetMetadata_SelfToken_ReturnsSelfCategoryWithExample()
    {
        var metadata = EchoTokenMetadataProvider.GetMetadata("self.name");

        Assert.Equal("Self", metadata.Category);
        Assert.Contains("current action-attached object", metadata.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{self.name}", metadata.Example, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetMetadata_ActionToken_ReturnsActionCategoryWithExample()
    {
        var metadata = EchoTokenMetadataProvider.GetMetadata("currentAction.missingPartsCount");

        Assert.Equal("Action", metadata.Category);
        Assert.Contains("execution context", metadata.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("currentAction.", metadata.Example, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetMetadata_ChooserEntry_ReturnsChooserCategory()
    {
        var metadata = EchoTokenMetadataProvider.GetMetadata("Choose variable...");

        Assert.Equal("Chooser", metadata.Category);
        Assert.Contains("full variable chooser", metadata.Description, StringComparison.OrdinalIgnoreCase);
    }
}
