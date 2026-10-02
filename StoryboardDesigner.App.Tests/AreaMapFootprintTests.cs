using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Tests;

public sealed class AreaMapFootprintTests
{
    [Fact]
    public void MultiCellFootprint_UsesWholeRectangleForOverlapAndBounds()
    {
        var large = new AreaMapFootprint(2, 1, 3, 3, 0);

        Assert.True(large.Fits(5, 4));
        Assert.False(large.Fits(4, 4));
        Assert.True(large.Overlaps(new AreaMapFootprint(4, 3, 1, 1, 0)));
        Assert.False(large.Overlaps(new AreaMapFootprint(4, 3, 1, 1, 1)));
        Assert.True(large.Intersects(new AreaMapFootprint(4, 3, 1, 1, 1)));
    }

    [Fact]
    public void MultiCellFootprint_DistinguishesSharedSideFromCorner()
    {
        var large = new AreaMapFootprint(0, 0, 3, 3, 0);
        for (var column = 0; column < 3; column++)
        {
            Assert.True(large.TryGetAdjacentOffset(new AreaMapFootprint(column, 3, 1, 1, 0),
                allowDiagonals: false, out var dx, out var dy));
            Assert.Equal(0, dx);
            Assert.Equal(1, dy);
        }

        var corner = new AreaMapFootprint(3, 3, 1, 1, 0);
        Assert.False(large.TryGetAdjacentOffset(corner, allowDiagonals: false, out _, out _));
        Assert.True(large.TryGetAdjacentOffset(corner, allowDiagonals: true, out var cornerDx, out var cornerDy));
        Assert.Equal((1, 1), (cornerDx, cornerDy));
    }
}
