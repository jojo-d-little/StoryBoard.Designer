using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using Storyboard.Shared.GameServices.References;
using Storyboard.GameEngine.Configuration.ActionManifest;

namespace StoryboardDesigner.App.Tests;

public sealed class ActionEchoReferenceTokenProviderRegistryTests
{
    private static IReadOnlyList<string> GetManifestOutputVariableTokens(CommandActionType actionType)
    {
        var manifest = ActionManifestLoader.Load(Path.Combine(AppContext.BaseDirectory, "Config"));
        return manifest.GetAction(actionType.ToString()).OutputVariables.SelectMany(static output =>
        {
            var key = $"currentAction.{output.ActionOutputVariableKey}";
            var tokens = new List<string> { key };
            if (output.SupportsCount)
            {
                tokens.Add($"{key}Count");
            }

            if (output.SupportsIndexedValues)
            {
                tokens.Add($"{key}[n]");
            }

            return tokens;
        }).OrderBy(static token => token, StringComparer.OrdinalIgnoreCase).ToList();
    }

    [Fact]
    public void CreateDefault_HasProviderForEveryActionType()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        foreach (var actionType in CommandActionTypeValues.All)
        {
            // MaterializeObjectCopy is runtime-only and does not expose action echo tokens in designer.
            if (actionType == CommandActionType.MaterializeObjectCopy)
            {
                continue;
            }

            Assert.True(registry.HasProvider(actionType), $"Missing provider for action type '{actionType}'.");
        }
    }

    [Fact]
    public void GetTokens_BuildCompositeByParts_ReturnsOnlyActionPrefixedTokens()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.BuildCompositeByParts);

        Assert.NotEmpty(tokens);
        Assert.All(tokens, token => Assert.StartsWith("currentAction.", token, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("missingParts", tokens, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetTokens_BuildCompositeByParts_MatchesSharedCanonicalDescriptorOrder()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.BuildCompositeByParts);
        var sharedTokens = GetManifestOutputVariableTokens(CommandActionType.BuildCompositeByParts);

        Assert.NotEmpty(sharedTokens);
        Assert.Equal(sharedTokens, tokens);
    }

    [Fact]
    public void GetTokens_BreakCompositeItem_ReturnsOnlyActionPrefixedTokens()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.BreakCompositeItem);

        Assert.NotEmpty(tokens);
        Assert.All(tokens, token => Assert.StartsWith("currentAction.", token, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("currentAction.breakTarget", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.all", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("breakTarget", tokens, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetTokens_BuildCompositeByTarget_MatchesSharedCanonicalDescriptorOrder()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.BuildCompositeByTarget);
        var sharedTokens = GetManifestOutputVariableTokens(CommandActionType.BuildCompositeByTarget);

        Assert.NotEmpty(sharedTokens);
        Assert.Equal(sharedTokens, tokens);
    }

    [Fact]
    public void GetTokens_BuildCompositeByTarget_ContainsActionAllAndResultTokens()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.BuildCompositeByTarget);

        Assert.Contains("currentAction.success", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.resultCode", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.all", tokens, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetTokens_BreakCompositeItem_MatchesSharedCanonicalDescriptorOrder()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.BreakCompositeItem);
        var sharedTokens = GetManifestOutputVariableTokens(CommandActionType.BreakCompositeItem);

        Assert.NotEmpty(sharedTokens);
        Assert.Equal(sharedTokens, tokens);
    }

    [Fact]
    public void GetTokens_PutObjectInContainer_ReturnsContainerCapacityActionTokens()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.PutObjectInContainer);

        Assert.NotEmpty(tokens);
        Assert.All(tokens, token => Assert.StartsWith("currentAction.", token, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("currentAction.targetContainer", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.targetContainerName", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.primaryitem", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.failureReason", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.totalCapacity", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.totalRemainingCapacity", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.totalInventoryPointsNeeded", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.all", tokens, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetTokens_PutObjectInContainer_MatchesSharedCanonicalDescriptorOrder()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.PutObjectInContainer);
        var sharedTokens = GetManifestOutputVariableTokens(CommandActionType.PutObjectInContainer);

        Assert.NotEmpty(sharedTokens);
        Assert.Equal(sharedTokens, tokens);
    }

    [Fact]
    public void GetTokens_RemoveObjectFromContainer_ReturnsContainerCapacityActionTokens()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.RemoveObjectFromContainer);

        Assert.NotEmpty(tokens);
        Assert.All(tokens, token => Assert.StartsWith("currentAction.", token, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("currentAction.sourceContainer", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.sourceContainerName", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.removedObjectName", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.objectsNewLocationName", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.failureReason", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.totalCapacity", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.totalRemainingCapacity", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.totalInventoryPointsNeeded", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.isInternalPlayerTransfer", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.all", tokens, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetTokens_RemoveObjectFromContainer_MatchesSharedCanonicalDescriptorOrder()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.RemoveObjectFromContainer);
        var sharedTokens = GetManifestOutputVariableTokens(CommandActionType.RemoveObjectFromContainer);

        Assert.NotEmpty(sharedTokens);
        Assert.Equal(sharedTokens, tokens);
    }

    [Fact]
    public void GetTokens_NavigateDirection_MatchesSharedCanonicalDescriptorOrder()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.NavigateDirection);
        var sharedTokens = GetManifestOutputVariableTokens(CommandActionType.NavigateDirection);

        Assert.NotEmpty(sharedTokens);
        Assert.Equal(sharedTokens, tokens);
    }

    [Fact]
    public void GetTokens_NavigateDirection_ContainsActionAndRoomScopedTokens()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.NavigateDirection);

        Assert.Contains("currentAction.success", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.resultCode", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.all", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("currentRoom.Name", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("priorRoom.Name", tokens, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetTokens_NavigateToAdjacent_MatchesSharedCanonicalDescriptorOrder()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.NavigateToAdjacent);
        var sharedTokens = GetManifestOutputVariableTokens(CommandActionType.NavigateToAdjacent);

        Assert.NotEmpty(sharedTokens);
        Assert.Equal(sharedTokens, tokens);
    }

    [Fact]
    public void GetTokens_SetActiveRoomObject_MatchesSharedCanonicalDescriptorOrder()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.SetActiveRoomObject);
        var sharedTokens = GetManifestOutputVariableTokens(CommandActionType.SetActiveRoomObject);

        Assert.NotEmpty(sharedTokens);
        Assert.Equal(sharedTokens, tokens);
    }

    [Fact]
    public void GetTokens_SetActiveRoomObject_ContainsSelectedObjectNameToken()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.SetActiveRoomObject);

        Assert.Contains("currentAction.selectedObjectName", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("currentAction.selectedObjectNameCount", tokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("currentAction.all", tokens, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetTokens_MoveRoomObjectOnGrid_MatchesSharedCanonicalDescriptorOrder()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.MoveRoomObjectOnGrid);
        var sharedTokens = GetManifestOutputVariableTokens(CommandActionType.MoveRoomObjectOnGrid);

        Assert.NotEmpty(sharedTokens);
        Assert.Equal(sharedTokens, tokens);
    }

    [Fact]
    public void GetTokens_RotateRoomObjectOnGrid_MatchesSharedCanonicalDescriptorOrder()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.RotateRoomObjectOnGrid);
        var sharedTokens = GetManifestOutputVariableTokens(CommandActionType.RotateRoomObjectOnGrid);

        Assert.Empty(sharedTokens);
        Assert.Equal(sharedTokens, tokens);
    }

    [Fact]
    public void GetTokens_StackRoomObjectOnAnother_MatchesSharedCanonicalDescriptorOrder()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.StackRoomObjectOnAnother);
        var sharedTokens = GetManifestOutputVariableTokens(CommandActionType.StackRoomObjectOnAnother);

        Assert.NotEmpty(sharedTokens);
        Assert.Equal(sharedTokens, tokens);
    }

    [Fact]
    public void GetTokens_NonCompositeAction_ReturnsEmpty()
    {
        var registry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();

        var tokens = registry.GetTokens(CommandActionType.EchoMessage);

        Assert.Empty(tokens);
    }

    [Fact]
    public void Constructor_DuplicateActionTypeProviders_Throws()
    {
        var duplicateProviders = new IActionEchoReferenceTokenProvider[]
        {
            new EchoMessageActionEchoReferenceTokenProvider(),
            new EchoMessageActionEchoReferenceTokenProvider()
        };

        var ex = Assert.Throws<ArgumentException>(() => new ActionEchoReferenceTokenProviderRegistry(duplicateProviders));
        Assert.Contains(nameof(CommandActionType.EchoMessage), ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SharedDescriptorRegistry_BuildCompositeByParts_DeclaresExpectedPilotTokens()
    {
        var tokens = GetManifestOutputVariableTokens(CommandActionType.BuildCompositeByParts);

        Assert.Equal(
        [
            "currentAction.all",
            "currentAction.builtObjectNameInGame",
            "currentAction.candidateTargets",
            "currentAction.candidateTargetsCount",
            "currentAction.candidateTargets[n]",
            "currentAction.missingParts",
            "currentAction.missingPartsCount",
            "currentAction.missingParts[n]",
            "currentAction.providedParts",
            "currentAction.providedPartsCount",
            "currentAction.providedParts[n]",
            "currentAction.requestedTarget",
            "currentAction.targetContainerName"
        ],
        tokens);
    }

    [Fact]
    public void SharedDescriptorRegistry_BuildCompositeByTarget_DeclaresExpectedPilotTokens()
    {
        var tokens = GetManifestOutputVariableTokens(CommandActionType.BuildCompositeByTarget);

        Assert.Equal(
        [
            "currentAction.all",
            "currentAction.builtObjectNameInGame",
            "currentAction.resultCode",
            "currentAction.success"
        ],
        tokens);
    }

    [Fact]
    public void SharedDescriptorRegistry_BreakCompositeItem_DeclaresExpectedPilotTokens()
    {
        var tokens = GetManifestOutputVariableTokens(CommandActionType.BreakCompositeItem);

        Assert.Equal(
        [
            "currentAction.all",
            "currentAction.breakTarget",
            "currentAction.recipeParts",
            "currentAction.recipePartsCount",
            "currentAction.recipeParts[n]",
            "currentAction.restoredParts",
            "currentAction.restoredPartsCount",
            "currentAction.restoredParts[n]",
            "currentAction.returnedParts",
            "currentAction.returnedPartsCount",
            "currentAction.returnedParts[n]"
        ],
        tokens);
    }

    [Fact]
    public void SharedDescriptorRegistry_PutObjectInContainer_DeclaresExpectedPilotTokens()
    {
        var tokens = GetManifestOutputVariableTokens(CommandActionType.PutObjectInContainer);

        Assert.Equal(
        [
            "currentAction.all",
            "currentAction.failureReason",
            "currentAction.primaryitem",
            "currentAction.targetContainer",
            "currentAction.targetContainerName",
            "currentAction.totalCapacity",
            "currentAction.totalInventoryPointsNeeded",
            "currentAction.totalRemainingCapacity",
            "currentAction.totalUsedPoints"
        ],
        tokens);
    }

    [Fact]
    public void SharedDescriptorRegistry_RemoveObjectFromContainer_DeclaresExpectedPilotTokens()
    {
        var tokens = GetManifestOutputVariableTokens(CommandActionType.RemoveObjectFromContainer);

        Assert.Equal(
        [
            "currentAction.all",
            "currentAction.failureReason",
            "currentAction.isInternalPlayerTransfer",
            "currentAction.objectsNewLocationName",
            "currentAction.removedObjectName",
            "currentAction.sourceContainer",
            "currentAction.sourceContainerName",
            "currentAction.totalCapacity",
            "currentAction.totalInventoryPointsNeeded",
            "currentAction.totalRemainingCapacity",
            "currentAction.totalUsedPoints"
        ],
        tokens);
    }

    [Fact]
    public void SharedDescriptorRegistry_NavigateDirection_DeclaresExpectedPilotTokens()
    {
        var tokens = GetManifestOutputVariableTokens(CommandActionType.NavigateDirection);

        Assert.Equal(
        [
            "currentAction.all",
            "currentAction.destinationRoomName",
            "currentAction.navigatedDirection",
            "currentAction.priorRoomName",
            "currentAction.resultCode",
            "currentAction.success"
        ],
        tokens);
    }

    [Fact]
    public void SharedDescriptorRegistry_MoveRoomObjectOnGrid_DeclaresExpectedPilotTokens()
    {
        var tokens = GetManifestOutputVariableTokens(CommandActionType.MoveRoomObjectOnGrid);

        Assert.Equal(
        [
            "currentAction.all",
            "currentAction.moveDirection",
            "currentAction.moveDistance",
            "currentAction.movedObjectName",
            "currentAction.resultCode",
            "currentAction.success"
        ],
        tokens);
    }

    [Fact]
    public void SharedDescriptorRegistry_RotateRoomObjectOnGrid_DeclaresExpectedPilotTokens()
    {
        var tokens = GetManifestOutputVariableTokens(CommandActionType.RotateRoomObjectOnGrid);

        Assert.Empty(tokens);
    }

    [Fact]
    public void SharedDescriptorRegistry_StackRoomObjectOnAnother_DeclaresExpectedPilotTokens()
    {
        var tokens = GetManifestOutputVariableTokens(CommandActionType.StackRoomObjectOnAnother);

        Assert.Equal(
        [
            "currentAction.all",
            "currentAction.resultCode",
            "currentAction.stackDirection",
            "currentAction.stackDistance",
            "currentAction.stackedObjectName",
            "currentAction.stackTargetObjectName",
            "currentAction.success"
        ],
        tokens);
    }
}
