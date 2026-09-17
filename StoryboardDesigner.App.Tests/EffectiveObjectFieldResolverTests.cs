using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public class EffectiveObjectFieldResolverTests
{
    [Fact]
    public void GetEffectiveNameInGame_UsesInstanceValue_WhenLinked()
    {
        var linkedBaseObjectId = Guid.NewGuid();
        var definition = new GameObject
        {
            ObjectId = linkedBaseObjectId,
            Name = "Door",
            NameInGame = "Rusty Door"
        };

        var instance = new GameObject
        {
            Name = "Door",
            NameInGame = "Local stale value",
            LinkedBaseObjectId = linkedBaseObjectId
        };

        var value = EffectiveObjectFieldResolver.GetEffectiveNameInGame(
            instance,
            id => id == linkedBaseObjectId ? definition : null);

        Assert.Equal("Local stale value", value);
    }

    [Fact]
    public void GetEffectiveNameInGame_DoesNotFallbackToDefinitionName_WhenDefinitionNameInGameBlank()
    {
        var linkedBaseObjectId = Guid.NewGuid();
        var definition = new GameObject
        {
            ObjectId = linkedBaseObjectId,
            Name = "Door",
            NameInGame = string.Empty
        };

        var instance = new GameObject
        {
            Name = "Door",
            LinkedBaseObjectId = linkedBaseObjectId
        };

        var value = EffectiveObjectFieldResolver.GetEffectiveNameInGame(
            instance,
            id => id == linkedBaseObjectId ? definition : null);

        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void GetEffectiveNameInGame_UsesInstanceValue_WhenLegacyOverrideExists()
    {
        var linkedBaseObjectId = Guid.NewGuid();
        var definition = new GameObject
        {
            ObjectId = linkedBaseObjectId,
            Name = "Door",
            NameInGame = "Rusty Door"
        };

        var instance = new GameObject
        {
            Name = "Door",
            LinkedBaseObjectId = linkedBaseObjectId,
            InstanceOverrides = new Dictionary<string, string?>
            {
                [EffectiveObjectFieldResolver.NameInGameField] = "North Door"
            }
        };

        var value = EffectiveObjectFieldResolver.GetEffectiveNameInGame(
            instance,
            id => id == linkedBaseObjectId ? definition : null);

        Assert.Equal("North Door", value);
    }

    [Fact]
    public void GetEffectiveDescription_UsesDefinitionValue_WhenNoOverride()
    {
        var linkedBaseObjectId = Guid.NewGuid();
        var definition = new GameObject
        {
            ObjectId = linkedBaseObjectId,
            Name = "Door",
            Description = "A heavy iron door"
        };

        var instance = new GameObject
        {
            Name = "Door",
            Description = "Local stale value",
            LinkedBaseObjectId = linkedBaseObjectId
        };

        var description = EffectiveObjectFieldResolver.GetEffectiveDescription(
            instance,
            id => id == linkedBaseObjectId ? definition : null);

        Assert.Equal("A heavy iron door", description);
    }

    [Fact]
    public void GetEffectiveDescription_UsesOverride_WhenPresent()
    {
        var linkedBaseObjectId = Guid.NewGuid();
        var definition = new GameObject
        {
            ObjectId = linkedBaseObjectId,
            Name = "Door",
            Description = "A heavy iron door"
        };

        var instance = new GameObject
        {
            Name = "Door",
            Description = "Local stale value",
            LinkedBaseObjectId = linkedBaseObjectId,
            InstanceOverrides = new Dictionary<string, string?>
            {
                [EffectiveObjectFieldResolver.DescriptionField] = "Painted green door"
            }
        };

        var description = EffectiveObjectFieldResolver.GetEffectiveDescription(
            instance,
            id => id == linkedBaseObjectId ? definition : null);

        Assert.Equal("Painted green door", description);
    }

    [Fact]
    public void GetEffectiveName_UsesInstanceValue_WhenLinked()
    {
        var linkedBaseObjectId = Guid.NewGuid();
        var definition = new GameObject
        {
            ObjectId = linkedBaseObjectId,
            Name = "Standard Door"
        };

        var instance = new GameObject
        {
            Name = "Legacy copy",
            LinkedBaseObjectId = linkedBaseObjectId
        };

        var name = EffectiveObjectFieldResolver.GetEffectiveName(
            instance,
            id => id == linkedBaseObjectId ? definition : null);

        Assert.Equal("Legacy copy", name);
    }

    [Fact]
    public void GetEffectiveName_PrefersInstanceValue_WhenLegacyNameOverrideExists()
    {
        var linkedBaseObjectId = Guid.NewGuid();
        var definition = new GameObject
        {
            ObjectId = linkedBaseObjectId,
            Name = "Standard Door"
        };

        var instance = new GameObject
        {
            Name = "Legacy copy",
            LinkedBaseObjectId = linkedBaseObjectId,
            InstanceOverrides = new Dictionary<string, string?>
            {
                ["Name"] = "Legacy override"
            }
        };

        var name = EffectiveObjectFieldResolver.GetEffectiveName(
            instance,
            id => id == linkedBaseObjectId ? definition : null);

        Assert.Equal("Legacy copy", name);
    }

    [Fact]
    public void GetEffectiveNameSynonyms_UsesDefinitionByDefault_WhenLinked()
    {
        var linkedBaseObjectId = Guid.NewGuid();
        var definition = new GameObject
        {
            ObjectId = linkedBaseObjectId,
            NameSynonyms = ["blade", "knife"]
        };

        var instance = new GameObject
        {
            LinkedBaseObjectId = linkedBaseObjectId,
            NameSynonyms = ["stale"]
        };

        var synonyms = EffectiveObjectFieldResolver.GetEffectiveNameSynonyms(
            instance,
            id => id == linkedBaseObjectId ? definition : null);

        Assert.Equal(["blade", "knife"], synonyms);
    }

    [Fact]
    public void GetEffectiveNameSynonyms_UsesOverride_WhenPresent()
    {
        var linkedBaseObjectId = Guid.NewGuid();
        var definition = new GameObject
        {
            ObjectId = linkedBaseObjectId,
            NameSynonyms = ["blade", "knife"]
        };

        var instance = new GameObject
        {
            LinkedBaseObjectId = linkedBaseObjectId,
            InstanceOverrides = new Dictionary<string, string?>
            {
                [EffectiveObjectFieldResolver.NameSynonymsField] = "north blade, sharp edge"
            }
        };

        var synonyms = EffectiveObjectFieldResolver.GetEffectiveNameSynonyms(
            instance,
            id => id == linkedBaseObjectId ? definition : null);

        Assert.Equal(["north blade", "sharp edge"], synonyms);
    }

    [Fact]
    public void SetOverrideAndClearOverride_ManageSparseOverridePresence()
    {
        var instance = new GameObject();

        Assert.False(EffectiveObjectFieldResolver.HasOverride(instance, EffectiveObjectFieldResolver.NameInGameField));

        EffectiveObjectFieldResolver.SetOverride(instance, EffectiveObjectFieldResolver.NameInGameField, "North Door");

        Assert.True(EffectiveObjectFieldResolver.HasOverride(instance, EffectiveObjectFieldResolver.NameInGameField));
        Assert.Equal("North Door", instance.InstanceOverrides[EffectiveObjectFieldResolver.NameInGameField]);

        EffectiveObjectFieldResolver.ClearOverride(instance, EffectiveObjectFieldResolver.NameInGameField);

        Assert.False(EffectiveObjectFieldResolver.HasOverride(instance, EffectiveObjectFieldResolver.NameInGameField));
    }
}

