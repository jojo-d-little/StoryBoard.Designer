using System.Text.Json.Serialization;

namespace StoryboardDesigner.App.Models;

public sealed partial class CommandAction
{
    private ContainerTransferPayload _containerFacet
    {
        get => GetFacet(static () => new ContainerTransferPayload(string.Empty));
        set => SetFacet(value);
    }

    private NavigatePayload _navigateFacet
    {
        get => GetFacet(static () => new NavigatePayload());
        set => SetFacet(value);
    }

    private MoveRoomObjectOnGridPayload _moveFacet
    {
        get => GetFacet(static () => new MoveRoomObjectOnGridPayload());
        set => SetFacet(value);
    }

    private RotateRoomObjectOnGridPayload _rotateFacet
    {
        get => GetFacet(static () => new RotateRoomObjectOnGridPayload());
        set => SetFacet(value);
    }

    private StackRoomObjectOnAnotherPayload _stackFacet
    {
        get => GetFacet(static () => new StackRoomObjectOnAnotherPayload());
        set => SetFacet(value);
    }

    private SetActiveRoomObjectPayload _setActiveRoomObjectFacet
    {
        get => GetFacet(static () => new SetActiveRoomObjectPayload(string.Empty));
        set => SetFacet(value);
    }

    private ClearActiveRoomObjectsPayload _clearActiveRoomObjectsFacet
    {
        get => GetFacet(static () => new ClearActiveRoomObjectsPayload(RuntimeClearActiveRoomObjectsScope.Both));
        set => SetFacet(value);
    }

    private CompositeByTargetPayload _compositeByTargetFacet
    {
        get => GetFacet(static () => new CompositeByTargetPayload(null, null, new List<Guid>(), null, null, "ContainedInComposite"));
        set => SetFacet(value);
    }

    private CompositeByPartsPayload _compositeByPartsFacet
    {
        get => GetFacet(static () => new CompositeByPartsPayload(null, null, new List<Guid>(), null, null, string.Empty, string.Empty, "ContainedInComposite", string.Empty));
        set => SetFacet(value);
    }

    private BreakCompositePayload _breakCompositeFacet
    {
        get => GetFacet(static () => new BreakCompositePayload(null, null, new List<Guid>(), null, null, "ContainedInComposite"));
        set => SetFacet(value);
    }

    [JsonIgnore]
    public IActionPayload? Payload
    {
        get => CreatePayloadSnapshot();
        set
        {
            if (value is null)
            {
                return;
            }

            if (!TryApplyPayload(value))
            {
                return;
            }

            OnPropertyChanged();
        }
    }

    public IActionPayload? CreatePayloadSnapshot()
    {
        return ActionPayloadSchemaMap.IsPayloadCompatible(ActionType, _payload)
            ? CommandActionPayloadCoordinator.CreateSnapshot(ActionType, _payload, LinkedActions)
            : null;
    }

    public bool TryApplyPayload(IActionPayload payload)
    {
        if (!ActionPayloadSchemaMap.IsPayloadCompatible(ActionType, payload))
        {
            return false;
        }

        _payload = CommandActionPayloadCoordinator.ClonePayload(payload);
        SyncCompatibilityStateFromPayload(_payload);
        RaiseProjectionChangedForPayload(_payload);

        return true;
    }

    private void SyncCompatibilityStateFromPayload(IActionPayload payload)
    {
        if (!CommandActionPayloadCoordinator.TryReadCompatibilityState(payload, out var linkedActions))
        {
            return;
        }

        if (linkedActions is not null)
        {
            _linkedActions = linkedActions;
        }
    }

    private void RaiseProjectionChangedForPayload(IActionPayload payload)
    {
        OnPropertyChanged(nameof(Payload));

        foreach (var propertyName in CommandActionPayloadCoordinator.GetProjectionPropertyNames(payload))
        {
            OnPropertyChanged(propertyName);
        }

        RaiseSummaryChanged();
    }

    private TPayload GetFacet<TPayload>(Func<TPayload> fallbackFactory)
        where TPayload : IActionPayload
    {
        return _payload is TPayload facet ? facet : fallbackFactory();
    }

    private void SetFacet<TPayload>(TPayload facet)
        where TPayload : IActionPayload
    {
        if (_payload is not TPayload)
        {
            return;
        }

        _payload = facet;
    }

    private void EnsurePayloadForCurrentActionType()
    {
        if (ActionPayloadSchemaMap.IsPayloadCompatible(ActionType, _payload))
        {
            return;
        }

        _payload = CommandActionPayloadCoordinator.CreateDefaultPayload(ActionType);
    }
}
