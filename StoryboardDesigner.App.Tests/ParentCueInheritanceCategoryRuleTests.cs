using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Objects;

namespace StoryboardDesigner.App.Tests;

public sealed class ParentCueInheritanceCategoryRuleTests
{
    [Fact]
    public void RejectsUnknownDuplicateAndBlankInheritanceCategories()
    {
        var parent = new GameObject { Name = "Parent", IsContainer = true };
        var child = new GameObject
        {
            Name = "Child",
            IsPartOfParent = true,
            Placement = new StructuralPartPlacement { Mode = StructuralPartPlacementKind.ParentRelative },
            InheritParentCueCategories = ["Movement", "PointParticleEffect", "Appearance", "Movement", " "]
        };
        parent.ContainedObjects = [child];
        var room = new Room { Name = "Room", GameObjects = [parent] };
        var area = new Area { Name = "Area", Rooms = [room] };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel { Name = "InheritanceValidation", Planets = [planet] };
        ScopeHierarchy.AttachParents(project);

        var issues = Evaluate(project);

        Assert.Equal(3, issues.Count);
        Assert.Contains(issues, issue => issue.Description.Contains("'PointParticleEffect' is not eligible", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Description.Contains("'Movement' is listed more than once", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Description.Contains("IDs must be nonempty", StringComparison.Ordinal));
    }

    [Fact]
    public void AllowsUnsetEmptyAndEligibleSelections()
    {
        var children = new[]
        {
            new GameObject { Name = "Unset", IsPartOfParent = true, Placement = new StructuralPartPlacement() },
            new GameObject { Name = "Empty", IsPartOfParent = true, Placement = new StructuralPartPlacement(), InheritParentCueCategories = [] },
            new GameObject { Name = "Selected", IsPartOfParent = true, Placement = new StructuralPartPlacement(), InheritParentCueCategories = ["Movement", "Highlight", "Appearance", "Disappearance"] }
        };
        var parent = new GameObject { Name = "Parent", IsContainer = true, ContainedObjects = children.ToList() };
        var room = new Room { Name = "Room", GameObjects = [parent] };
        var area = new Area { Name = "Area", Rooms = [room] };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel { Name = "InheritanceValidation", Planets = [planet] };
        ScopeHierarchy.AttachParents(project);

        var issues = Evaluate(project);

        Assert.Empty(issues);
    }

    private static List<StoryboardDesigner.App.Validation.Contracts.ValidationIssue> Evaluate(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new ParentCueInheritanceCategoryRule());
        return new ValidationEngine(registry)
            .Execute(new ValidationExecutionRequest(project))
            .Issues.ToList();
    }
}
