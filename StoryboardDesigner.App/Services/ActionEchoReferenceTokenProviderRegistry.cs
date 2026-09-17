namespace StoryboardDesigner.App.Services;

public sealed class ActionEchoReferenceTokenProviderRegistry
{
    private readonly IReadOnlyDictionary<CommandActionType, IActionEchoReferenceTokenProvider> _providers;

    public ActionEchoReferenceTokenProviderRegistry(IEnumerable<IActionEchoReferenceTokenProvider> providers)
    {
        var groupedProviders = (providers ?? Array.Empty<IActionEchoReferenceTokenProvider>())
            .GroupBy(provider => provider.ActionType)
            .ToList();

        var duplicates = groupedProviders
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicates.Count > 0)
        {
            throw new ArgumentException($"Duplicate action echo token providers registered for: {string.Join(", ", duplicates)}", nameof(providers));
        }

        _providers = groupedProviders.ToDictionary(group => group.Key, group => group.First());
    }

    public bool HasProvider(CommandActionType actionType)
    {
        return _providers.ContainsKey(actionType);
    }

    public IReadOnlyList<string> GetTokens(CommandActionType actionType)
    {
        return _providers.TryGetValue(actionType, out var provider)
            ? provider.GetTokens()
            : Array.Empty<string>();
    }

    public static ActionEchoReferenceTokenProviderRegistry CreateDefault()
    {
        return new ActionEchoReferenceTokenProviderRegistry(
        [
            new LinkedActionsActionEchoReferenceTokenProvider(),
            new SynonymActionEchoReferenceTokenProvider(),
            new EchoMessageActionEchoReferenceTokenProvider(),
            new SetFlagActionEchoReferenceTokenProvider(),
            new CheckGamePropertyActionEchoReferenceTokenProvider(),
            new SetGamePropertyActionEchoReferenceTokenProvider(),
            new PutObjectInContainerActionEchoReferenceTokenProvider(),
            new RemoveObjectFromContainerActionEchoReferenceTokenProvider(),
            new OpenObjectActionEchoReferenceTokenProvider(),
            new CloseObjectActionEchoReferenceTokenProvider(),
            new UnlockObjectActionEchoReferenceTokenProvider(),
            new LockObjectActionEchoReferenceTokenProvider(),
            new InvokeProcedureActionEchoReferenceTokenProvider(),
            new NavigateDirectionActionEchoReferenceTokenProvider(),
            new NavigateToAdjacentActionEchoReferenceTokenProvider(),
            new SetActiveRoomObjectActionEchoReferenceTokenProvider(),
            new ClearActiveRoomObjectsActionEchoReferenceTokenProvider(),
            new ParameterizedEmptyActionEchoReferenceTokenProvider(CommandActionType.PlaySoundEffect),
            new ParameterizedEmptyActionEchoReferenceTokenProvider(CommandActionType.ClearRoomObjectSelections),
            new ParameterizedEmptyActionEchoReferenceTokenProvider(CommandActionType.NextPhase),
            new ParameterizedEmptyActionEchoReferenceTokenProvider(CommandActionType.PreviousPhase),
            new ParameterizedEmptyActionEchoReferenceTokenProvider(CommandActionType.SetPhase),
            new ParameterizedEmptyActionEchoReferenceTokenProvider(CommandActionType.ManageNarrativePhaseAmbientSounds),
            new StartTimerActionEchoReferenceTokenProvider(),
            new CancelTimerActionEchoReferenceTokenProvider(),
            new BuildCompositeByTargetActionEchoReferenceTokenProvider(),
            new BuildCompositeByPartsActionEchoReferenceTokenProvider(),
            new BreakCompositeItemActionEchoReferenceTokenProvider(),
            new MoveRoomObjectOnGridActionEchoReferenceTokenProvider(),
            new MoveRoomObjectByPointsActionEchoReferenceTokenProvider(),
            new RotateRoomObjectOnGridActionEchoReferenceTokenProvider(),
            new SelectRoomObjectByPointActionEchoReferenceTokenProvider(),
            new StackRoomObjectOnAnotherActionEchoReferenceTokenProvider()
        ]);
    }
}
