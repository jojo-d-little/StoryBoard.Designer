using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class SharedPropertyRelationshipOrderingTests
{
    [Fact]
    public void OrderForDisplay_SortsByPriorityThenRelationshipId()
    {
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000010");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000020");
        var thirdId = Guid.Parse("00000000-0000-0000-0000-000000000030");

        var relationships = new[]
        {
            new SharedPropertyRelationshipReviewItem { RelationshipId = thirdId, Priority = 2, CounterpartEndpoint = "C" },
            new SharedPropertyRelationshipReviewItem { RelationshipId = secondId, Priority = 1, CounterpartEndpoint = "B" },
            new SharedPropertyRelationshipReviewItem { RelationshipId = firstId, Priority = 1, CounterpartEndpoint = "A" }
        };

        var ordered = SharedPropertyRelationshipOrdering.OrderForDisplay(relationships).ToList();

        Assert.Equal(firstId, ordered[0].RelationshipId);
        Assert.Equal(secondId, ordered[1].RelationshipId);
        Assert.Equal(thirdId, ordered[2].RelationshipId);
    }

    [Fact]
    public void SharedIndicatorToolTip_UsesSharedTerminology()
    {
        var variable = new GamePropertyDefinition { Name = "isOpen" };
        var container = new GamePropertiesContainerNodeViewModel(PropertyResolutionScope.Object);
        var node = new GamePropertyNodeViewModel(variable, PropertyResolutionScope.Object, container);

        node.SetSharedState(2);

        Assert.Contains("Shared", node.SharedIndicatorToolTip, StringComparison.Ordinal);
        Assert.DoesNotContain("Linked", node.SharedIndicatorToolTip, StringComparison.OrdinalIgnoreCase);
    }
}
