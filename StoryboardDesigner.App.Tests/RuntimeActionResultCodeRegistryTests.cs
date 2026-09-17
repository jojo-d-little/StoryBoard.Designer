namespace StoryboardDesigner.App.Tests;

public sealed class RuntimeActionResultCodeRegistryTests
{
    [Fact]
    public void ExecutorSupportedActionTypes_AllDeclareResultCodeSets_WithBaselineTokensAndEnumOrder()
    {
        var actionTypes = RuntimeActionResultCodeRegistry.GetExecutorSupportedActionTypes();
        Assert.NotEmpty(actionTypes);

        foreach (var actionType in actionTypes)
        {
            Assert.True(RuntimeActionResultCodeRegistry.TryGetCodeSet(actionType, out var codeSet));
            Assert.NotNull(codeSet);
            Assert.NotEqual(typeof(void), codeSet.EnumType);
            Assert.True(codeSet.Codes.Count >= 2);

            var enumNames = Enum.GetNames(codeSet.EnumType);
            Assert.True(enumNames.Length >= 2);
            Assert.Equal(enumNames[0], codeSet.Codes[0].Token);
            Assert.Equal(enumNames[1], codeSet.Codes[1].Token);
        }
    }

    [Fact]
    public void NavigateDirection_ResultCodeSet_UsesExpectedEnumAndBaselineOrder()
    {
        var codeSet = RuntimeActionResultCodeRegistry.GetCodeSet(CommandActionType.NavigateDirection);

        Assert.Equal(typeof(RuntimeNavigateDirectionResultCode), codeSet.EnumType);
        Assert.NotEmpty(codeSet.Codes);
        Assert.Equal("Success", codeSet.Codes[0].Token);
        Assert.Equal("Failure", codeSet.Codes[1].Token);

        var tokens = codeSet.Codes.Select(code => code.Token).ToList();
        Assert.Contains("Moved", tokens);
        Assert.Contains("DirectionRequired", tokens);
        Assert.Contains("NoExitInDirection", tokens);
        Assert.Contains("NoTraversableExit", tokens);
        Assert.Contains("AmbiguousTraversal", tokens);
        Assert.Contains("TraversalBlocked", tokens);
        Assert.Contains("MoveFailed", tokens);
    }

    [Fact]
    public void Synonym_ResultCodeSet_UsesBaselineSuccessFailureTokens()
    {
        var codeSet = RuntimeActionResultCodeRegistry.GetCodeSet(CommandActionType.Synonym);

        Assert.Equal(typeof(RuntimeSynonymResultCode), codeSet.EnumType);
        Assert.NotEmpty(codeSet.Codes);
        Assert.Equal("Success", codeSet.Codes[0].Token);
        Assert.Equal("Failure", codeSet.Codes[1].Token);
    }

    [Fact]
    public void NavigateDirection_ResultCodeLookup_IsCaseInsensitive()
    {
        var found = RuntimeCommandActionExecutor.TryGetResultCodeDescriptor(
            CommandActionType.NavigateDirection,
            "mOvEd",
            out var descriptor);

        Assert.True(found);
        Assert.Equal("Moved", descriptor.Token);
        Assert.Equal(RuntimeActionResultCodeOutcome.Success, descriptor.Outcome);
    }

    [Fact]
    public void NavigateDirection_EmittedTokens_AreDeclaredInRegistry()
    {
        var emittedTokens = new[]
        {
            "Moved",
            "DirectionRequired",
            "NoExitInDirection",
            "NoTraversableExit",
            "AmbiguousTraversal",
            "TraversalBlocked",
            "MoveFailed"
        };

        foreach (var token in emittedTokens)
        {
            Assert.True(RuntimeActionResultCodeRegistry.IsSupportedToken(CommandActionType.NavigateDirection, token));
        }
    }

    [Fact]
    public void NavigateToAdjacent_ResultCodeSet_UsesExpectedEnumAndTokens()
    {
        var codeSet = RuntimeActionResultCodeRegistry.GetCodeSet(CommandActionType.NavigateToAdjacent);

        Assert.Equal(typeof(RuntimeNavigateToAdjacentResultCode), codeSet.EnumType);
        Assert.NotEmpty(codeSet.Codes);
        Assert.Equal("Success", codeSet.Codes[0].Token);
        Assert.Equal("Failure", codeSet.Codes[1].Token);

        var tokens = codeSet.Codes.Select(code => code.Token).ToList();
        Assert.Contains("Moved", tokens);
        Assert.Contains("UnknownDestination", tokens);
        Assert.Contains("DirectionRequired", tokens);
        Assert.Contains("DoorClosed", tokens);
        Assert.Contains("TraversalNotPassable", tokens);
        Assert.Contains("TraversalAmbiguous", tokens);
        Assert.Contains("MoveFailed", tokens);
    }

    [Fact]
    public void NavigateToAdjacent_ResultCodeLookup_IsCaseInsensitive()
    {
        var found = RuntimeCommandActionExecutor.TryGetResultCodeDescriptor(
            CommandActionType.NavigateToAdjacent,
            "tRaVeRsAlAmBiGuOuS",
            out var descriptor);

        Assert.True(found);
        Assert.Equal("TraversalAmbiguous", descriptor.Token);
        Assert.Equal(RuntimeActionResultCodeOutcome.Failure, descriptor.Outcome);
    }

    [Fact]
    public void SelectRoomObjectByPoint_ResultCodeSet_ExposesSetActiveFailures()
    {
        var codeSet = RuntimeActionResultCodeRegistry.GetCodeSet(CommandActionType.SelectRoomObjectByPoint);

        Assert.Equal(typeof(RuntimeSetActiveRoomObjectResultCode), codeSet.EnumType);
        var tokens = codeSet.Codes.Select(code => code.Token).ToList();
        Assert.Contains("ActiveObjectSet", tokens);
        Assert.Contains("NoResolvedTarget", tokens);
        Assert.Contains("TargetNotInCurrentRoom", tokens);
        Assert.Contains("InvalidConfiguration", tokens);
        Assert.Contains("PassiveTargetNotSelectable", tokens);
    }

    [Fact]
    public void MoveRoomObjectByPoints_ResultCodeSet_ExposesMovementFailureTokens()
    {
        var codeSet = RuntimeActionResultCodeRegistry.GetCodeSet(CommandActionType.MoveRoomObjectByPoints);

        Assert.Equal(typeof(RuntimeMoveRoomObjectOnGridResultCode), codeSet.EnumType);
        var tokens = codeSet.Codes.Select(code => code.Token).ToList();
        Assert.Contains("MovedAllLegs", tokens);
        Assert.Contains("MovedPartialLegs", tokens);
        Assert.Contains("FailedBeforeAnyLegMoved", tokens);
        Assert.Contains("NoResolvedTarget", tokens);
        Assert.Contains("BlockedByCollision", tokens);
        Assert.Contains("InvalidConfiguration", tokens);
    }

    [Fact]
    public void BuildCompositeByParts_ResultCodeSet_UsesTypedEnumAndExpectedTokens()
    {
        var codeSet = RuntimeActionResultCodeRegistry.GetCodeSet(CommandActionType.BuildCompositeByParts);

        Assert.Equal(typeof(RuntimeBuildCompositeByPartsResultCode), codeSet.EnumType);
        Assert.NotEmpty(codeSet.Codes);
        Assert.Equal("Success", codeSet.Codes[0].Token);
        Assert.Equal("Failure", codeSet.Codes[1].Token);

        var tokens = codeSet.Codes.Select(code => code.Token).ToList();
        Assert.Contains("IncompleteRecipe", tokens);
        Assert.Contains("SpecifyTarget", tokens);
        Assert.Contains("TargetMismatch", tokens);
        Assert.Contains("NoMatchingRecipe", tokens);
        Assert.Contains("ParticipantVariableRequirementsNotMet", tokens);
    }

    [Fact]
    public void BuildCompositeByParts_ResultCodeLookup_IsCaseInsensitive()
    {
        var found = RuntimeCommandActionExecutor.TryGetResultCodeDescriptor(
            CommandActionType.BuildCompositeByParts,
            "nOmAtChInGrEcIpE",
            out var descriptor);

        Assert.True(found);
        Assert.Equal("NoMatchingRecipe", descriptor.Token);
        Assert.Equal(RuntimeActionResultCodeOutcome.Failure, descriptor.Outcome);
    }

    [Fact]
    public void BuildCompositeByTarget_ResultCodeSet_UsesTypedEnumAndExpectedTokens()
    {
        var codeSet = RuntimeActionResultCodeRegistry.GetCodeSet(CommandActionType.BuildCompositeByTarget);

        Assert.Equal(typeof(RuntimeBuildCompositeByTargetResultCode), codeSet.EnumType);
        var tokens = codeSet.Codes.Select(code => code.Token).ToList();

        Assert.Contains("Success", tokens);
        Assert.Contains("Failure", tokens);
        Assert.Contains("IncompleteRecipe", tokens);
        Assert.Contains("SpecifyTarget", tokens);
        Assert.Contains("TargetMismatch", tokens);
        Assert.Contains("ParticipantVariableRequirementsNotMet", tokens);
    }

    [Fact]
    public void BuildCompositeByTarget_ResultCodeLookup_IsCaseInsensitive()
    {
        var found = RuntimeCommandActionExecutor.TryGetResultCodeDescriptor(
            CommandActionType.BuildCompositeByTarget,
            "sPeCiFyTaRgEt",
            out var descriptor);

        Assert.True(found);
        Assert.Equal("SpecifyTarget", descriptor.Token);
        Assert.Equal(RuntimeActionResultCodeOutcome.Failure, descriptor.Outcome);
    }

    [Fact]
    public void BreakCompositeItem_ResultCodeSet_UsesTypedEnumAndExpectedTokens()
    {
        var codeSet = RuntimeActionResultCodeRegistry.GetCodeSet(CommandActionType.BreakCompositeItem);

        Assert.Equal(typeof(RuntimeBreakCompositeItemResultCode), codeSet.EnumType);
        var tokens = codeSet.Codes.Select(code => code.Token).ToList();

        Assert.Contains("Success", tokens);
        Assert.Contains("Failure", tokens);
        Assert.Contains("ResolveTargetFailed", tokens);
        Assert.Contains("TargetStateInvalid", tokens);
        Assert.Contains("RestoreUnavailable", tokens);
        Assert.Contains("MutationFailed", tokens);
    }

    [Fact]
    public void BreakCompositeItem_ResultCodeLookup_IsCaseInsensitive()
    {
        var found = RuntimeCommandActionExecutor.TryGetResultCodeDescriptor(
            CommandActionType.BreakCompositeItem,
            "mUtAtIoNfAiLeD",
            out var descriptor);

        Assert.True(found);
        Assert.Equal("MutationFailed", descriptor.Token);
        Assert.Equal(RuntimeActionResultCodeOutcome.Failure, descriptor.Outcome);
    }

    [Fact]
    public void PutObjectInContainer_ResultCodeSet_UsesTypedEnumAndExpectedTokens()
    {
        var codeSet = RuntimeActionResultCodeRegistry.GetCodeSet(CommandActionType.PutObjectInContainer);

        Assert.Equal(typeof(RuntimePutObjectInContainerResultCode), codeSet.EnumType);
        var tokens = codeSet.Codes.Select(code => code.Token).ToList();

        Assert.Contains("Success", tokens);
        Assert.Contains("Failure", tokens);
        Assert.Contains("ResolveTargetFailed", tokens);
        Assert.Contains("PutFailed", tokens);
        Assert.Contains("CapacityExceeded", tokens);
    }

    [Fact]
    public void PutObjectInContainer_ResultCodeLookup_IsCaseInsensitive()
    {
        var found = RuntimeCommandActionExecutor.TryGetResultCodeDescriptor(
            CommandActionType.PutObjectInContainer,
            "rEsOlVeTaRgEtFaIlEd",
            out var descriptor);

        Assert.True(found);
        Assert.Equal("ResolveTargetFailed", descriptor.Token);
        Assert.Equal(RuntimeActionResultCodeOutcome.Failure, descriptor.Outcome);
    }

    [Fact]
    public void RemoveObjectFromContainer_ResultCodeSet_UsesTypedEnumAndExpectedTokens()
    {
        var codeSet = RuntimeActionResultCodeRegistry.GetCodeSet(CommandActionType.RemoveObjectFromContainer);

        Assert.Equal(typeof(RuntimeRemoveObjectFromContainerResultCode), codeSet.EnumType);
        var tokens = codeSet.Codes.Select(code => code.Token).ToList();

        Assert.Contains("Success", tokens);
        Assert.Contains("Failure", tokens);
        Assert.Contains("ResolveSourceFailed", tokens);
        Assert.Contains("RemoveFailed", tokens);
        Assert.Contains("ResolveSubjectFailed", tokens);
    }

    [Fact]
    public void RemoveObjectFromContainer_ResultCodeLookup_IsCaseInsensitive()
    {
        var found = RuntimeCommandActionExecutor.TryGetResultCodeDescriptor(
            CommandActionType.RemoveObjectFromContainer,
            "rEmOvEfAiLeD",
            out var descriptor);

        Assert.True(found);
        Assert.Equal("RemoveFailed", descriptor.Token);
        Assert.Equal(RuntimeActionResultCodeOutcome.Failure, descriptor.Outcome);
    }

    [Fact]
    public void SetGameProperty_ResultCodeSet_UsesTypedEnumAndExpectedTokens()
    {
        var codeSet = RuntimeActionResultCodeRegistry.GetCodeSet(CommandActionType.SetGameProperty);

        Assert.Equal(typeof(RuntimeSetGamePropertyResultCode), codeSet.EnumType);
        var tokens = codeSet.Codes.Select(code => code.Token).ToList();

        Assert.Contains("Success", tokens);
        Assert.Contains("Failure", tokens);
        Assert.Contains("SetFailed", tokens);
    }

    [Fact]
    public void SetGameProperty_ResultCodeLookup_IsCaseInsensitive()
    {
        var found = RuntimeCommandActionExecutor.TryGetResultCodeDescriptor(
            CommandActionType.SetGameProperty,
            "sEtFaIlEd",
            out var descriptor);

        Assert.True(found);
        Assert.Equal("SetFailed", descriptor.Token);
        Assert.Equal(RuntimeActionResultCodeOutcome.Failure, descriptor.Outcome);
    }

    [Fact]
    public void CheckGameProperty_ResultCodeSet_UsesTypedEnumAndExpectedTokens()
    {
        var codeSet = RuntimeActionResultCodeRegistry.GetCodeSet(CommandActionType.CheckGameProperty);

        Assert.Equal(typeof(RuntimeCheckGamePropertyResultCode), codeSet.EnumType);
        var tokens = codeSet.Codes.Select(code => code.Token).ToList();

        Assert.Contains("Success", tokens);
        Assert.Contains("Failure", tokens);
        Assert.Contains("PropertyMissing", tokens);
        Assert.Contains("ValueMismatch", tokens);
    }

    [Fact]
    public void CheckGameProperty_ResultCodeLookup_IsCaseInsensitive()
    {
        var found = RuntimeCommandActionExecutor.TryGetResultCodeDescriptor(
            CommandActionType.CheckGameProperty,
            "vAlUeMiSmAtCh",
            out var descriptor);

        Assert.True(found);
        Assert.Equal("ValueMismatch", descriptor.Token);
        Assert.Equal(RuntimeActionResultCodeOutcome.Failure, descriptor.Outcome);
    }

    [Fact]
    public void SetFlag_ResultCodeSet_UsesTypedEnumAndExpectedTokens()
    {
        var codeSet = RuntimeActionResultCodeRegistry.GetCodeSet(CommandActionType.SetFlag);

        Assert.Equal(typeof(RuntimeSetFlagResultCode), codeSet.EnumType);
        var tokens = codeSet.Codes.Select(code => code.Token).ToList();

        Assert.Contains("Success", tokens);
        Assert.Contains("Failure", tokens);
        Assert.Contains("SetFailed", tokens);
    }

    [Fact]
    public void SetFlag_ResultCodeLookup_IsCaseInsensitive()
    {
        var found = RuntimeCommandActionExecutor.TryGetResultCodeDescriptor(
            CommandActionType.SetFlag,
            "sEtFaIlEd",
            out var descriptor);

        Assert.True(found);
        Assert.Equal("SetFailed", descriptor.Token);
        Assert.Equal(RuntimeActionResultCodeOutcome.Failure, descriptor.Outcome);
    }
}
