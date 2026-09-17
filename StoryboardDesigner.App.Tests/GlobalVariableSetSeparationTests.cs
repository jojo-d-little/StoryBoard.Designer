using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;

namespace StoryboardDesigner.App.Tests;

public sealed class GlobalVariableSetSeparationTests
{
    [Fact]
    public void ValidationLookup_Distinguishes_TrueGlobal_From_GlobalObjectVariables_WhenNamesMatch()
    {
        var trueGlobal = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "state",
            DefaultValue = "off",
            ValueRestriction = GamePropertyValueRestriction.Unrestricted
        };

        var globalObjectScoped = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "state",
            DefaultValue = "on",
            ValueRestriction = GamePropertyValueRestriction.Unrestricted
        };

        var project = new ProjectModel
        {
            GlobalVariables = [trueGlobal],
            GlobalObjectVariables = [globalObjectScoped]
        };

        var lookup = ValidationLookupService.Build(project);

        Assert.True(lookup.TryGetPropertyByVariableId(trueGlobal.Id, out var globalDescriptor));
        Assert.True(lookup.TryGetPropertyByVariableId(globalObjectScoped.Id, out var globalObjectDescriptor));

        Assert.Equal("Global", globalDescriptor.ScopePath);
        Assert.Equal("ProjectGlobalVariable", globalDescriptor.SemanticHint);

        Assert.Equal("Global / Game Properties", globalObjectDescriptor.ScopePath);
        Assert.Equal("GlobalScopeVariable", globalObjectDescriptor.SemanticHint);

        Assert.NotEqual(globalDescriptor.VariableId, globalObjectDescriptor.VariableId);
    }

    [Fact]
    public void ValidationLookup_DoesNotMaskSameNameAcrossGlobalAndGlobalObjectScopes()
    {
        var trueGlobal = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "energy",
            DefaultValue = "10",
            ValueRestriction = GamePropertyValueRestriction.Numeric
        };

        var globalObjectScoped = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "energy",
            DefaultValue = "1",
            ValueRestriction = GamePropertyValueRestriction.Numeric
        };

        var project = new ProjectModel
        {
            GlobalVariables = [trueGlobal],
            GlobalObjectVariables = [globalObjectScoped]
        };

        var lookup = ValidationLookupService.Build(project);

        Assert.True(lookup.TryGetPropertyByVariableId(trueGlobal.Id, out var globalDescriptor));
        Assert.True(lookup.TryGetPropertyByVariableId(globalObjectScoped.Id, out var globalObjectDescriptor));

        Assert.Equal("energy", globalDescriptor.VariableName);
        Assert.Equal("energy", globalObjectDescriptor.VariableName);
        Assert.NotEqual(globalDescriptor.ScopePath, globalObjectDescriptor.ScopePath);
    }

    [Fact]
    public void ValidationLookup_BuildsSharedRelationshipsAcrossGlobalAndGlobalObjectVariables_WithoutCollapsingIdentity()
    {
        var trueGlobal = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "doorState",
            DefaultValue = "closed",
            ValueRestriction = GamePropertyValueRestriction.Unrestricted
        };

        var globalObjectScoped = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "doorState",
            DefaultValue = "closed",
            ValueRestriction = GamePropertyValueRestriction.Unrestricted
        };

        var sharedId = Guid.NewGuid();
        var project = new ProjectModel
        {
            GlobalVariables = [trueGlobal],
            GlobalObjectVariables = [globalObjectScoped],
            SharedVariables =
            [
                new SharedVariableDefinition
                {
                    Id = sharedId,
                    Name = "DoorState",
                    Participants =
                    [
                        new SharedVariableParticipant
                        {
                            Kind = "global",
                            OwnerId = trueGlobal.Id,
                            VariableName = trueGlobal.Name
                        },
                        new SharedVariableParticipant
                        {
                            Kind = "global",
                            OwnerId = globalObjectScoped.Id,
                            VariableName = globalObjectScoped.Name
                        }
                    ]
                }
            ]
        };

        var lookup = ValidationLookupService.Build(project);

        var globalRelationships = lookup.GetSharedPropertyRelationshipsForVariable(trueGlobal.Id);
        var globalObjectRelationships = lookup.GetSharedPropertyRelationshipsForVariable(globalObjectScoped.Id);

        Assert.Equal(2, globalRelationships.Count);
        Assert.Equal(2, globalObjectRelationships.Count);

        Assert.Contains(globalRelationships, relation =>
            relation.SourceVariableId == trueGlobal.Id
            && relation.TargetVariableId == globalObjectScoped.Id
            && string.Equals(relation.SourceScopePath, "Global", StringComparison.Ordinal)
            && string.Equals(relation.TargetScopePath, "Global / Game Properties", StringComparison.Ordinal));

        Assert.Contains(globalRelationships, relation =>
            relation.SourceVariableId == globalObjectScoped.Id
            && relation.TargetVariableId == trueGlobal.Id
            && string.Equals(relation.SourceScopePath, "Global / Game Properties", StringComparison.Ordinal)
            && string.Equals(relation.TargetScopePath, "Global", StringComparison.Ordinal));

        Assert.Contains(globalObjectRelationships, relation =>
            relation.SourceVariableId == trueGlobal.Id
            && relation.TargetVariableId == globalObjectScoped.Id
            && string.Equals(relation.SourceScopePath, "Global", StringComparison.Ordinal)
            && string.Equals(relation.TargetScopePath, "Global / Game Properties", StringComparison.Ordinal));

        Assert.Contains(globalObjectRelationships, relation =>
            relation.SourceVariableId == globalObjectScoped.Id
            && relation.TargetVariableId == trueGlobal.Id
            && string.Equals(relation.SourceScopePath, "Global / Game Properties", StringComparison.Ordinal)
            && string.Equals(relation.TargetScopePath, "Global", StringComparison.Ordinal));
    }
}
