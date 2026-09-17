using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Tests;

public sealed class GameObjectCompositeRecipeIdTests
{
    [Fact]
    public void IsCompositeTarget_SetTrue_GeneratesRecipeIdWhenEmpty()
    {
        var obj = new GameObject
        {
            CompositeRecipeId = Guid.Empty,
            IsCompositeTarget = false
        };

        obj.IsCompositeTarget = true;

        Assert.NotEqual(Guid.Empty, obj.CompositeRecipeId);
    }

    [Fact]
    public void IsCompositeTarget_SetFalse_ClearsRecipeId()
    {
        var obj = new GameObject
        {
            IsCompositeTarget = true
        };

        var generated = obj.CompositeRecipeId;
        Assert.NotEqual(Guid.Empty, generated);

        obj.IsCompositeTarget = false;

        Assert.Equal(Guid.Empty, obj.CompositeRecipeId);
    }
}
