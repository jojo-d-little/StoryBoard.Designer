using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Tests;

public class GameObjectFeatureVariableContractTests
{
    [Fact]
    public void ApplyFeatureVariableContract_AddsAndRemovesKnownVariables()
    {
        var gameObject = new GameObject();

        gameObject.IsInventoriable = true;
        gameObject.IsContainer = true;
        gameObject.IsCapacityPointShareDividerEnabled = true;
        gameObject.IsOpenable = true;
        gameObject.IsLockable = true;
        gameObject.IsActivatable = true;
        gameObject.IsHidable = true;
        gameObject.IsQuantifiable = true;

        Assert.Contains(gameObject.Variables, v => v.Name == "inventoryPoints");
        Assert.Contains(gameObject.Variables, v => v.Name == "containerPoints");
        Assert.Contains(gameObject.Variables, v => v.Name == "containerPointsRemaining");
        Assert.Contains(gameObject.Variables, v => v.Name == "capacityPointShareDivider");
        Assert.Contains(gameObject.Variables, v => v.Name == "isOpen");
        Assert.Contains(gameObject.Variables, v => v.Name == "isLocked");
        Assert.Contains(gameObject.Variables, v => v.Name == "isActive");
        Assert.Contains(gameObject.Variables, v => v.Name == "isHidden");
        Assert.Contains(gameObject.Variables, v => v.Name == "isQuantifiable");
        Assert.Contains(gameObject.Variables, v => v.Name == "quantity");

        gameObject.IsInventoriable = false;
        gameObject.IsContainer = false;
        gameObject.IsOpenable = false;
        gameObject.IsLockable = false;
        gameObject.IsActivatable = false;
        gameObject.IsHidable = false;
        gameObject.IsQuantifiable = false;

        Assert.DoesNotContain(gameObject.Variables, v => v.Name == "inventoryPoints");
        Assert.DoesNotContain(gameObject.Variables, v => v.Name == "containerPoints");
        Assert.DoesNotContain(gameObject.Variables, v => v.Name == "containerPointsRemaining");
        Assert.DoesNotContain(gameObject.Variables, v => v.Name == "capacityPointShareDivider");
        Assert.DoesNotContain(gameObject.Variables, v => v.Name == "isOpen");
        Assert.DoesNotContain(gameObject.Variables, v => v.Name == "isLocked");
        Assert.DoesNotContain(gameObject.Variables, v => v.Name == "isActive");
        Assert.DoesNotContain(gameObject.Variables, v => v.Name == "isHidden");
        Assert.DoesNotContain(gameObject.Variables, v => v.Name == "isQuantifiable");
        Assert.DoesNotContain(gameObject.Variables, v => v.Name == "quantity");
    }

    [Fact]
    public void ApplyFeatureVariableContract_UsesConfiguredDefaultValues()
    {
        var gameObject = new GameObject
        {
            IsInventoriable = true,
            InventoryPointsDefaultValue = 3,
            IsContainer = true,
            ContainerPointsDefaultValue = 8,
            IsCapacityPointShareDividerEnabled = true,
            CapacityPointShareDividerDefaultValue = 4,
            IsOpenable = true,
            IsOpenDefaultValue = true,
            IsLockable = true,
            IsLockedDefaultValue = true,
            IsActivatable = true,
            IsActiveDefaultValue = false,
            IsHidable = true,
            IsHiddenDefaultValue = true,
            IsQuantifiable = true,
            Quantity = 6
        };

        Assert.Equal("3", gameObject.Variables.Single(v => v.Name == "inventoryPoints").DefaultValue);
        Assert.Equal("8", gameObject.Variables.Single(v => v.Name == "containerPoints").DefaultValue);
        Assert.Equal("8", gameObject.Variables.Single(v => v.Name == "containerPointsRemaining").DefaultValue);
        Assert.Equal("4", gameObject.Variables.Single(v => v.Name == "capacityPointShareDivider").DefaultValue);
        Assert.Equal("true", gameObject.Variables.Single(v => v.Name == "isOpen").DefaultValue);
        Assert.Equal("true", gameObject.Variables.Single(v => v.Name == "isLocked").DefaultValue);
        Assert.Equal("false", gameObject.Variables.Single(v => v.Name == "isActive").DefaultValue);
        Assert.Equal("true", gameObject.Variables.Single(v => v.Name == "isHidden").DefaultValue);
        Assert.Equal("true", gameObject.Variables.Single(v => v.Name == "isQuantifiable").DefaultValue);
        Assert.Equal("6", gameObject.Variables.Single(v => v.Name == "quantity").DefaultValue);
    }

    [Fact]
    public void InitializeFeatureFlagsFromKnownVariables_SetsFeatureFlagsFromExistingVariables()
    {
        var gameObject = new GameObject
        {
            Variables = new List<GamePropertyDefinition>
            {
                new() { Name = "inventoryPoints", DefaultValue = "7", ValueRestriction = GamePropertyValueRestriction.Numeric },
                new() { Name = "containerPoints", DefaultValue = "9", ValueRestriction = GamePropertyValueRestriction.Numeric },
                new() { Name = "capacityPointShareDivider", DefaultValue = "2", ValueRestriction = GamePropertyValueRestriction.Numeric },
                new() { Name = "isOpen", DefaultValue = "true", ValueRestriction = GamePropertyValueRestriction.TrueFalse },
                new() { Name = "isLocked", DefaultValue = "false", ValueRestriction = GamePropertyValueRestriction.TrueFalse },
                new() { Name = "isActive", DefaultValue = "true", ValueRestriction = GamePropertyValueRestriction.TrueFalse },
                new() { Name = "isHidden", DefaultValue = "false", ValueRestriction = GamePropertyValueRestriction.TrueFalse },
                new() { Name = "isQuantifiable", DefaultValue = "true", ValueRestriction = GamePropertyValueRestriction.TrueFalse },
                new() { Name = "quantity", DefaultValue = "12", ValueRestriction = GamePropertyValueRestriction.Numeric }
            }
        };

        gameObject.InitializeFeatureFlagsFromKnownVariables();

        Assert.True(gameObject.IsInventoriable);
        Assert.Equal(7, gameObject.InventoryPointsDefaultValue);
        Assert.True(gameObject.IsContainer);
        Assert.Equal(9, gameObject.ContainerPointsDefaultValue);
        Assert.True(gameObject.IsCapacityPointShareDividerEnabled);
        Assert.Equal(2, gameObject.CapacityPointShareDividerDefaultValue);
        Assert.True(gameObject.IsOpenable);
        Assert.True(gameObject.IsOpenDefaultValue);
        Assert.True(gameObject.IsLockable);
        Assert.False(gameObject.IsLockedDefaultValue);
        Assert.True(gameObject.IsActivatable);
        Assert.True(gameObject.IsActiveDefaultValue);
        Assert.True(gameObject.IsHidable);
        Assert.False(gameObject.IsHiddenDefaultValue);
        Assert.True(gameObject.IsQuantifiable);
        Assert.Equal(12, gameObject.Quantity);
    }

    [Fact]
    public void InventoryPointsDefaultValue_EnforcesMinimumOfOne()
    {
        var gameObject = new GameObject
        {
            IsInventoriable = true,
            InventoryPointsDefaultValue = 0
        };

        Assert.Equal(1, gameObject.InventoryPointsDefaultValue);
        Assert.Equal("1", gameObject.Variables.Single(v => v.Name == "inventoryPoints").DefaultValue);
    }

    [Fact]
    public void ContainerPointsDefaultValue_EnforcesMinimumOfOne()
    {
        var gameObject = new GameObject
        {
            IsContainer = true,
            ContainerPointsDefaultValue = 0
        };

        Assert.Equal(1, gameObject.ContainerPointsDefaultValue);
        Assert.Equal("1", gameObject.Variables.Single(v => v.Name == "containerPoints").DefaultValue);
        Assert.Equal("1", gameObject.Variables.Single(v => v.Name == "containerPointsRemaining").DefaultValue);
    }

    [Fact]
    public void CapacityPointShareDividerDefaultValue_EnforcesMinimumOfOne()
    {
        var gameObject = new GameObject
        {
            IsContainer = true,
            IsCapacityPointShareDividerEnabled = true,
            CapacityPointShareDividerDefaultValue = 0
        };

        Assert.Equal(1, gameObject.CapacityPointShareDividerDefaultValue);
        Assert.Equal("1", gameObject.Variables.Single(v => v.Name == "capacityPointShareDivider").DefaultValue);
    }

    [Fact]
    public void Quantity_EnforcesMinimumOfOne()
    {
        var gameObject = new GameObject
        {
            IsQuantifiable = true,
            Quantity = 0
        };

        Assert.Equal(1, gameObject.Quantity);
        Assert.Equal("1", gameObject.Variables.Single(v => v.Name == "quantity").DefaultValue);
    }
}
