using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class JsonExportServicePayloadTypeDiscriminatorTests
{
    [Fact]
    public void SaveAndLoadProjectModel_UsesPayloadTypeDiscriminator_ForEchoOpenCloseUnlockLockSetFlagCheckGamePropertySetGamePropertySetActiveClearActiveNavigateAndNavigateAdjacentMovePutRemoveContainerInvokeProcedureMaterializeStartTimerCancelTimerSynonymAndLinkedActions()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var targetAction = new CommandAction
            {
                Name = "Target",
                ActionType = CommandActionType.EchoMessage,
                Payload = new EchoPayload()
            };
            targetAction.EchoMessage = "target";

            var synonymTargetId = Guid.NewGuid();
            var setFlagAction = new CommandAction
            {
                Name = "SetFlag",
                ActionType = CommandActionType.SetFlag,
                FlagName = "questStarted",
                FlagValue = true,
                Payload = new SetFlagPayload("questStarted", true)
            };

            var openAction = new CommandAction
            {
                Name = "Open",
                ActionType = CommandActionType.OpenObject,
                Payload = new EchoPayload()
            };
            openAction.EchoMessage = "opened";

            var closeAction = new CommandAction
            {
                Name = "Close",
                ActionType = CommandActionType.CloseObject,
                Payload = new EchoPayload()
            };
            closeAction.EchoMessage = "closed";

            var unlockAction = new CommandAction
            {
                Name = "Unlock",
                ActionType = CommandActionType.UnlockObject,
                Payload = new EchoPayload()
            };
            unlockAction.EchoMessage = "unlocked";

            var lockAction = new CommandAction
            {
                Name = "Lock",
                ActionType = CommandActionType.LockObject,
                Payload = new EchoPayload()
            };
            lockAction.EchoMessage = "locked";

            var checkGamePropertyAction = new CommandAction
            {
                Name = "CheckGameProperty",
                ActionType = CommandActionType.CheckGameProperty,
                FlagName = "hasKey",
                FlagValue = true,
                Payload = new CheckGamePropertyPayload("hasKey", true)
            };

            var setGamePropertyAction = new CommandAction
            {
                Name = "SetGameProperty",
                ActionType = CommandActionType.SetGameProperty,
                GamePropertyName = "playerTitle",
                GamePropertyValue = "Ranger",
                Payload = new SetGamePropertyPayload("playerTitle", "Ranger")
            };

            var setActiveAction = new CommandAction
            {
                Name = "SetActive",
                ActionType = CommandActionType.SetActiveRoomObject,
                SelectionCueEffectKey = "selection-outline-blue",
                Payload = new SetActiveRoomObjectPayload("selection-outline-blue")
            };

            var clearActiveAction = new CommandAction
            {
                Name = "ClearActive",
                ActionType = CommandActionType.ClearActiveRoomObjects,
                ClearActiveRoomObjectsScope = RuntimeClearActiveRoomObjectsScope.SecondaryOnly,
                Payload = new ClearActiveRoomObjectsPayload(RuntimeClearActiveRoomObjectsScope.SecondaryOnly)
            };

            var putInContainerAction = new CommandAction
            {
                Name = "PutInContainer",
                ActionType = CommandActionType.PutObjectInContainer,
                TargetContainerId = "bag-1",
                Payload = new ContainerTransferPayload("bag-1")
            };

            var removeFromContainerAction = new CommandAction
            {
                Name = "RemoveFromContainer",
                ActionType = CommandActionType.RemoveObjectFromContainer,
                TargetContainerId = "bag-1",
                Payload = new ContainerTransferPayload("bag-1")
            };

            var navigateAction = new CommandAction
            {
                Name = "Navigate",
                ActionType = CommandActionType.NavigateDirection,
                DirectionQualifierText = "N",
                Payload = new NavigatePayload()
            };

            var navigateAdjacentAction = new CommandAction
            {
                Name = "NavigateAdjacent",
                ActionType = CommandActionType.NavigateToAdjacent,
                Payload = new NavigatePayload()
            };

            var moveAction = new CommandAction
            {
                Name = "Move",
                ActionType = CommandActionType.MoveRoomObjectOnGrid,
                DirectionQualifierText = "E",
                MoveDistanceInCells = 2,
                MoveAllowPartialMove = true,
                MoveAllowJumpOver = true,
                MoveVisualTransitionHint = RuntimeMovementVisualTransitionHint.Fast,
                MoveTravelVisualizationMode = RuntimeMovementTravelVisualizationMode.Direct,
                Payload = new MoveRoomObjectOnGridPayload(
                    directionToken: "E",
                    distanceInCells: 2,
                    allowPartialMove: true,
                    allowJumpOver: true,
                    visualTransitionHint: RuntimeMovementVisualTransitionHint.Fast,
                    travelVisualizationMode: RuntimeMovementTravelVisualizationMode.Direct)
            };

            var rotateAction = new CommandAction
            {
                Name = "Rotate",
                ActionType = CommandActionType.RotateRoomObjectOnGrid,
                RotateMode = RuntimeRotateRoomObjectOnGridAttemptMode.Face,
                RotateFacingDirectionToken = "SE",
                RotateVisualTransitionHint = RuntimeMovementVisualTransitionHint.Fast,
                Payload = new RotateRoomObjectOnGridPayload(
                    mode: RuntimeRotateRoomObjectOnGridAttemptMode.Face,
                    turnDegrees: null,
                    facingDirectionToken: "SE",
                    visualTransitionHint: RuntimeMovementVisualTransitionHint.Fast)
            };

            var stackAction = new CommandAction
            {
                Name = "Stack",
                ActionType = CommandActionType.StackRoomObjectOnAnother,
                StackVisualTransitionHint = RuntimeMovementVisualTransitionHint.Slow,
                Payload = new StackRoomObjectOnAnotherPayload(RuntimeMovementVisualTransitionHint.Slow)
            };

            var compositeTargetObjectId = Guid.NewGuid();
            var compositeRecipeId = Guid.NewGuid();
            var compositePartA = Guid.NewGuid();
            var compositePartB = Guid.NewGuid();
            var buildCompositeByTargetAction = new CommandAction
            {
                Name = "BuildByTarget",
                ActionType = CommandActionType.BuildCompositeByTarget,
                Payload = new CompositeByTargetPayload(
                    CompositeTargetObjectId: compositeTargetObjectId,
                    CompositeRecipeId: compositeRecipeId,
                    CompositeRequiredPartObjectIds: new List<Guid> { compositePartA },
                    CompositeStrictPartCountEnforcement: true,
                    CompositeMinimumRequiredPartCount: 1,
                    CompositePartConsumptionMode: "ContainedInComposite")
            };

            var buildCompositeByPartsAction = new CommandAction
            {
                Name = "BuildByParts",
                ActionType = CommandActionType.BuildCompositeByParts,
                Payload = new CompositeByPartsPayload(
                    CompositeTargetObjectId: compositeTargetObjectId,
                    CompositeRecipeId: compositeRecipeId,
                    CompositeRequiredPartObjectIds: new List<Guid> { compositePartA, compositePartB },
                    CompositeStrictPartCountEnforcement: true,
                    CompositeMinimumRequiredPartCount: 2,
                    CompositeMatchMode: "AllRequired",
                    CompositeAmbiguityPolicy: "FailWithHint",
                    CompositePartConsumptionMode: "ContainedInComposite",
                    CompositeResolvedTargetOutputTemplate: string.Empty)
            };

            var breakCompositeAction = new CommandAction
            {
                Name = "BreakComposite",
                ActionType = CommandActionType.BreakCompositeItem,
                Payload = new BreakCompositePayload(
                    CompositeTargetObjectId: compositeTargetObjectId,
                    CompositeRecipeId: null,
                    CompositeRequiredPartObjectIds: new List<Guid>(),
                    CompositeStrictPartCountEnforcement: null,
                    CompositeMinimumRequiredPartCount: null,
                    CompositePartConsumptionMode: "ContainedInComposite")
            };

            var materializeSourceObjectId = Guid.NewGuid();
            var materializeAction = new CommandAction
            {
                Name = "Materialize",
                ActionType = CommandActionType.MaterializeObjectCopy,
                MaterializeSourceObjectId = materializeSourceObjectId,
                Payload = new MaterializeObjectCopyPayload(materializeSourceObjectId)
            };

            var procedureId = Guid.NewGuid();
            var invokeProcedureAction = new CommandAction
            {
                Name = "InvokeProcedure",
                ActionType = CommandActionType.InvokeProcedure,
                ProcedureId = procedureId,
                Payload = new InvokeProcedurePayload(procedureId)
            };

            var startTimerAction = new CommandAction
            {
                Name = "StartTimer",
                ActionType = CommandActionType.StartTimer,
                Payload = new StartTimerPayload("door-close", TimerOwnerType.Room)
            };

            var cancelTimerOwnerId = Guid.NewGuid();
            var cancelTimerAction = new CommandAction
            {
                Name = "CancelTimer",
                ActionType = CommandActionType.CancelTimer,
                Payload = new CancelTimerPayload("door-close", TimerOwnerType.Room, cancelTimerOwnerId)
            };

            var synonymAction = new CommandAction
            {
                Name = "Synonym",
                ActionType = CommandActionType.Synonym,
                SynonymTargetActionId = Guid.NewGuid(),
                Payload = new SynonymPayload(synonymTargetId)
            };

            var linkedReferences = new List<LinkedActionReference>
            {
                new()
                {
                    ActionId = targetAction.Id,
                    RunWhen = LinkedActionRunWhen.Always,
                    Order = 2
                }
            };

            var linkedFlowAction = new CommandAction
            {
                Name = "LinkedFlow",
                ActionType = CommandActionType.LinkedActions,
                LinkedActions = linkedReferences,
                Payload = new LinkedFlowPayload(linkedReferences)
            };

            var room = new Room
            {
                Name = "Room A",
                AvailableActions = [targetAction, openAction, closeAction, unlockAction, lockAction, setFlagAction, checkGamePropertyAction, setGamePropertyAction, setActiveAction, clearActiveAction, navigateAction, navigateAdjacentAction, moveAction, rotateAction, stackAction, buildCompositeByTargetAction, buildCompositeByPartsAction, breakCompositeAction, putInContainerAction, removeFromContainerAction, invokeProcedureAction, materializeAction, startTimerAction, cancelTimerAction, synonymAction, linkedFlowAction]
            };

            var area = new Area { Name = "Area A", Rooms = [room] };
            var country = new Country { Name = "Country A", Areas = [area] };
            var planet = new Planet { Name = "Planet A", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "PayloadTypeDiscriminator",
                Planets = [planet],
                StartingPlanetName = planet.Name
            };

            var projectFilePath = Path.Combine(tempRoot, "PayloadTypeDiscriminator.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var roomFilePath = Path.Combine(BuildRoomsFolderPath(projectFilePath), $"{room.Id:N}.room.json");
            using var roomDoc = JsonDocument.Parse(File.ReadAllText(roomFilePath, Encoding.UTF8));
            var actions = roomDoc.RootElement.GetProperty("availableGameActions").EnumerateArray().ToList();

            var synonymJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Synonym", StringComparison.Ordinal));
            var linkedJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "LinkedFlow", StringComparison.Ordinal));
            var echoJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Target", StringComparison.Ordinal));
            var openJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Open", StringComparison.Ordinal));
            var closeJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Close", StringComparison.Ordinal));
            var unlockJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Unlock", StringComparison.Ordinal));
            var lockJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Lock", StringComparison.Ordinal));
            var setFlagJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "SetFlag", StringComparison.Ordinal));
            var checkJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "CheckGameProperty", StringComparison.Ordinal));
            var setGamePropertyJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "SetGameProperty", StringComparison.Ordinal));
            var setActiveJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "SetActive", StringComparison.Ordinal));
            var clearActiveJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "ClearActive", StringComparison.Ordinal));
            var navigateJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Navigate", StringComparison.Ordinal));
            var navigateAdjacentJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "NavigateAdjacent", StringComparison.Ordinal));
            var moveJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Move", StringComparison.Ordinal));
            var rotateJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Rotate", StringComparison.Ordinal));
            var stackJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Stack", StringComparison.Ordinal));
            var buildByTargetJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "BuildByTarget", StringComparison.Ordinal));
            var buildByPartsJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "BuildByParts", StringComparison.Ordinal));
            var breakCompositeJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "BreakComposite", StringComparison.Ordinal));
            var putInContainerJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "PutInContainer", StringComparison.Ordinal));
            var removeFromContainerJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "RemoveFromContainer", StringComparison.Ordinal));
            var invokeProcedureJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "InvokeProcedure", StringComparison.Ordinal));
            var materializeJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Materialize", StringComparison.Ordinal));
            var startTimerJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "StartTimer", StringComparison.Ordinal));
            var cancelTimerJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "CancelTimer", StringComparison.Ordinal));

            Assert.Equal("echoMessage", echoJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("openObject", openJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("closeObject", closeJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("unlockObject", unlockJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("lockObject", lockJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("setFlag", setFlagJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("checkGameProperty", checkJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("setGameProperty", setGamePropertyJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("setActiveRoomObject", setActiveJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("clearActiveRoomObjects", clearActiveJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("navigateDirection", navigateJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("navigateToAdjacent", navigateAdjacentJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("moveRoomObjectOnGrid", moveJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("rotateRoomObjectOnGrid", rotateJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("stackRoomObjectOnAnother", stackJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("buildCompositeByTarget", buildByTargetJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("buildCompositeByParts", buildByPartsJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("breakCompositeItem", breakCompositeJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("putObjectInContainer", putInContainerJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("removeObjectFromContainer", removeFromContainerJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("invokeProcedure", invokeProcedureJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("materializeObjectCopy", materializeJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("startTimer", startTimerJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("cancelTimer", cancelTimerJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("synonym", synonymJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("linkedFlow", linkedJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.False(linkedJson.TryGetProperty("linkedActions", out _));

            var roomNode = JsonNode.Parse(File.ReadAllText(roomFilePath, Encoding.UTF8))!.AsObject();
            var actionArray = roomNode["availableGameActions"]!.AsArray();
            foreach (var actionNode in actionArray.OfType<JsonObject>())
            {
                var actionName = actionNode["name"]?.GetValue<string>();
                if (string.Equals(actionName, "Synonym", StringComparison.Ordinal))
                {
                    actionNode.Remove("synonymTargetActionId");
                }

                if (string.Equals(actionName, "SetFlag", StringComparison.Ordinal))
                {
                    actionNode.Remove("flagName");
                    actionNode.Remove("flagValue");
                }

                if (string.Equals(actionName, "CheckGameProperty", StringComparison.Ordinal))
                {
                    actionNode.Remove("flagName");
                    actionNode.Remove("flagValue");
                }

                if (string.Equals(actionName, "SetGameProperty", StringComparison.Ordinal))
                {
                    actionNode.Remove("gamePropertyName");
                    actionNode.Remove("gamePropertyValue");
                }

                if (string.Equals(actionName, "SetActive", StringComparison.Ordinal))
                {
                    actionNode.Remove("selectionCueEffectKey");
                }

                if (string.Equals(actionName, "ClearActive", StringComparison.Ordinal))
                {
                    actionNode.Remove("clearActiveSelectionScope");
                }

                if (string.Equals(actionName, "PutInContainer", StringComparison.Ordinal)
                    || string.Equals(actionName, "RemoveFromContainer", StringComparison.Ordinal))
                {
                    actionNode.Remove("targetContainerId");
                }

                if (string.Equals(actionName, "Move", StringComparison.Ordinal))
                {
                    actionNode.Remove("moveDirectionToken");
                    actionNode.Remove("moveDistanceInCells");
                    actionNode.Remove("moveAllowPartialMove");
                    actionNode.Remove("moveAllowJumpOver");
                    actionNode.Remove("moveVisualTransitionHint");
                    actionNode.Remove("moveTravelVisualizationMode");
                }

                if (string.Equals(actionName, "Rotate", StringComparison.Ordinal))
                {
                    actionNode.Remove("rotateMode");
                    actionNode.Remove("rotateTurnDegrees");
                    actionNode.Remove("rotateFacingDirectionToken");
                    actionNode.Remove("rotateVisualTransitionHint");
                }

                if (string.Equals(actionName, "Stack", StringComparison.Ordinal))
                {
                    actionNode.Remove("stackVisualTransitionHint");
                }

                if (string.Equals(actionName, "BuildByTarget", StringComparison.Ordinal)
                    || string.Equals(actionName, "BuildByParts", StringComparison.Ordinal)
                    || string.Equals(actionName, "BreakComposite", StringComparison.Ordinal))
                {
                    actionNode.Remove("compositeTargetObjectId");
                    actionNode.Remove("compositeRecipeId");
                    actionNode.Remove("compositeRequiredPartObjectIds");
                    actionNode.Remove("compositeStrictPartCountEnforcement");
                    actionNode.Remove("compositeMinimumRequiredPartCount");
                    actionNode.Remove("compositeMatchMode");
                    actionNode.Remove("compositeAmbiguityPolicy");
                    actionNode.Remove("compositePartConsumptionMode");
                    actionNode.Remove("compositeResolvedTargetOutputTemplate");
                }

                if (string.Equals(actionName, "Materialize", StringComparison.Ordinal))
                {
                    actionNode.Remove("materializeSourceObjectId");
                }

                if (string.Equals(actionName, "InvokeProcedure", StringComparison.Ordinal))
                {
                    actionNode.Remove("procedureId");
                }

                if (string.Equals(actionName, "LinkedFlow", StringComparison.Ordinal))
                {
                    actionNode.Remove("linkedActions");
                }
            }

            File.WriteAllText(roomFilePath, roomNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedRoom = Assert.Single(Assert.Single(Assert.Single(Assert.Single(loaded!.Planets).Countries).Areas).Rooms);
            var loadedSetFlag = loadedRoom.AvailableActions.Single(action => action.Name == "SetFlag");
            var loadedCheckGameProperty = loadedRoom.AvailableActions.Single(action => action.Name == "CheckGameProperty");
            var loadedSetGameProperty = loadedRoom.AvailableActions.Single(action => action.Name == "SetGameProperty");
            var loadedSetActive = loadedRoom.AvailableActions.Single(action => action.Name == "SetActive");
            var loadedClearActive = loadedRoom.AvailableActions.Single(action => action.Name == "ClearActive");
            var loadedNavigate = loadedRoom.AvailableActions.Single(action => action.Name == "Navigate");
            var loadedNavigateAdjacent = loadedRoom.AvailableActions.Single(action => action.Name == "NavigateAdjacent");
            var loadedMove = loadedRoom.AvailableActions.Single(action => action.Name == "Move");
            var loadedRotate = loadedRoom.AvailableActions.Single(action => action.Name == "Rotate");
            var loadedStack = loadedRoom.AvailableActions.Single(action => action.Name == "Stack");
            var loadedBuildByTarget = loadedRoom.AvailableActions.Single(action => action.Name == "BuildByTarget");
            var loadedBuildByParts = loadedRoom.AvailableActions.Single(action => action.Name == "BuildByParts");
            var loadedBreakComposite = loadedRoom.AvailableActions.Single(action => action.Name == "BreakComposite");
            var loadedOpen = loadedRoom.AvailableActions.Single(action => action.Name == "Open");
            var loadedClose = loadedRoom.AvailableActions.Single(action => action.Name == "Close");
            var loadedUnlock = loadedRoom.AvailableActions.Single(action => action.Name == "Unlock");
            var loadedLock = loadedRoom.AvailableActions.Single(action => action.Name == "Lock");
            var loadedPutInContainer = loadedRoom.AvailableActions.Single(action => action.Name == "PutInContainer");
            var loadedRemoveFromContainer = loadedRoom.AvailableActions.Single(action => action.Name == "RemoveFromContainer");
            var loadedInvokeProcedure = loadedRoom.AvailableActions.Single(action => action.Name == "InvokeProcedure");
            var loadedMaterialize = loadedRoom.AvailableActions.Single(action => action.Name == "Materialize");
            var loadedStartTimer = loadedRoom.AvailableActions.Single(action => action.Name == "StartTimer");
            var loadedCancelTimer = loadedRoom.AvailableActions.Single(action => action.Name == "CancelTimer");
            var loadedSynonym = loadedRoom.AvailableActions.Single(action => action.Name == "Synonym");
            var loadedLinked = loadedRoom.AvailableActions.Single(action => action.Name == "LinkedFlow");
            var loadedEcho = loadedRoom.AvailableActions.Single(action => action.Name == "Target");

            Assert.Equal("target", loadedEcho.EchoMessage);
            Assert.IsType<EchoPayload>(loadedEcho.Payload);

            Assert.Equal("opened", loadedOpen.EchoMessage);
            Assert.IsType<EchoPayload>(loadedOpen.Payload);

            Assert.Equal("closed", loadedClose.EchoMessage);
            Assert.IsType<EchoPayload>(loadedClose.Payload);

            Assert.Equal("unlocked", loadedUnlock.EchoMessage);
            Assert.IsType<EchoPayload>(loadedUnlock.Payload);

            Assert.Equal("locked", loadedLock.EchoMessage);
            Assert.IsType<EchoPayload>(loadedLock.Payload);

            Assert.Equal("questStarted", loadedSetFlag.FlagName);
            Assert.True(loadedSetFlag.FlagValue);
            Assert.IsType<SetFlagPayload>(loadedSetFlag.Payload);

            Assert.Equal("hasKey", loadedCheckGameProperty.FlagName);
            Assert.True(loadedCheckGameProperty.FlagValue);
            Assert.IsType<CheckGamePropertyPayload>(loadedCheckGameProperty.Payload);

            Assert.Equal("playerTitle", loadedSetGameProperty.GamePropertyName);
            Assert.Equal("Ranger", loadedSetGameProperty.GamePropertyValue);
            Assert.IsType<SetGamePropertyPayload>(loadedSetGameProperty.Payload);

            Assert.Equal("selection-outline-blue", loadedSetActive.SelectionCueEffectKey);
            Assert.IsType<SetActiveRoomObjectPayload>(loadedSetActive.Payload);

            Assert.Equal(RuntimeClearActiveRoomObjectsScope.SecondaryOnly, loadedClearActive.ClearActiveRoomObjectsScope);
            Assert.IsType<ClearActiveRoomObjectsPayload>(loadedClearActive.Payload);

            Assert.IsType<NavigatePayload>(loadedNavigate.Payload);
            Assert.IsType<NavigatePayload>(loadedNavigateAdjacent.Payload);

            var loadedMovePayload = ActionPayloadAccessors.GetMoveRoomObjectOnGridPayload(loadedMove);
            Assert.IsType<MoveRoomObjectOnGridPayload>(loadedMove.Payload);
            Assert.Equal("east", loadedMovePayload.DirectionToken);
            Assert.Equal(2, loadedMovePayload.DistanceInCells);
            Assert.True(loadedMovePayload.AllowPartialMove);
            Assert.True(loadedMovePayload.AllowJumpOver);
            Assert.Equal(RuntimeMovementVisualTransitionHint.Fast, loadedMovePayload.VisualTransitionHint);
            Assert.Equal(RuntimeMovementTravelVisualizationMode.Direct, loadedMovePayload.TravelVisualizationMode);

            var loadedRotatePayload = ActionPayloadAccessors.GetRotateRoomObjectOnGridPayload(loadedRotate);
            Assert.IsType<RotateRoomObjectOnGridPayload>(loadedRotate.Payload);
            Assert.Equal(RuntimeRotateRoomObjectOnGridAttemptMode.Face, loadedRotatePayload.Mode);
            Assert.Equal("SE", loadedRotatePayload.FacingDirectionToken);
            Assert.Equal(RuntimeMovementVisualTransitionHint.Fast, loadedRotatePayload.VisualTransitionHint);

            var loadedStackPayload = ActionPayloadAccessors.GetStackRoomObjectOnAnotherPayload(loadedStack);
            Assert.IsType<StackRoomObjectOnAnotherPayload>(loadedStack.Payload);
            Assert.Equal(RuntimeMovementVisualTransitionHint.Slow, loadedStackPayload.VisualTransitionHint);

            var loadedBuildByTargetPayload = ActionPayloadAccessors.GetCompositeByTargetPayload(loadedBuildByTarget);
            Assert.IsType<CompositeByTargetPayload>(loadedBuildByTarget.Payload);
            Assert.Equal(compositeTargetObjectId, loadedBuildByTargetPayload.CompositeTargetObjectId);
            Assert.Equal(compositeRecipeId, loadedBuildByTargetPayload.CompositeRecipeId);
            Assert.Equal(new[] { compositePartA }, loadedBuildByTargetPayload.CompositeRequiredPartObjectIds);
            Assert.True(loadedBuildByTargetPayload.CompositeStrictPartCountEnforcement.GetValueOrDefault());
            Assert.Equal(1, loadedBuildByTargetPayload.CompositeMinimumRequiredPartCount.GetValueOrDefault());
            Assert.Equal("ContainedInComposite", loadedBuildByTargetPayload.CompositePartConsumptionMode);

            var loadedBuildByPartsPayload = ActionPayloadAccessors.GetCompositeByPartsPayload(loadedBuildByParts);
            Assert.IsType<CompositeByPartsPayload>(loadedBuildByParts.Payload);
            Assert.Equal(compositeTargetObjectId, loadedBuildByPartsPayload.CompositeTargetObjectId);
            Assert.Equal(compositeRecipeId, loadedBuildByPartsPayload.CompositeRecipeId);
            Assert.Equal(new[] { compositePartA, compositePartB }, loadedBuildByPartsPayload.CompositeRequiredPartObjectIds);
            Assert.True(loadedBuildByPartsPayload.CompositeStrictPartCountEnforcement.GetValueOrDefault());
            Assert.Equal(2, loadedBuildByPartsPayload.CompositeMinimumRequiredPartCount.GetValueOrDefault());
            Assert.Equal("AllRequired", loadedBuildByPartsPayload.CompositeMatchMode);
            Assert.Equal("FailWithHint", loadedBuildByPartsPayload.CompositeAmbiguityPolicy);
            Assert.Equal("ContainedInComposite", loadedBuildByPartsPayload.CompositePartConsumptionMode);
            Assert.Equal(string.Empty, loadedBuildByPartsPayload.CompositeResolvedTargetOutputTemplate);

            var loadedBreakPayload = ActionPayloadAccessors.GetBreakCompositePayload(loadedBreakComposite);
            Assert.IsType<BreakCompositePayload>(loadedBreakComposite.Payload);
            Assert.Equal(compositeTargetObjectId, loadedBreakPayload.CompositeTargetObjectId);
            Assert.Equal("ContainedInComposite", loadedBreakPayload.CompositePartConsumptionMode);
            Assert.Null(loadedBreakPayload.CompositeRecipeId);
            Assert.Empty(loadedBreakPayload.CompositeRequiredPartObjectIds);
            Assert.Null(loadedBreakPayload.CompositeStrictPartCountEnforcement);
            Assert.Null(loadedBreakPayload.CompositeMinimumRequiredPartCount);

            Assert.Equal("bag-1", loadedPutInContainer.TargetContainerId);
            Assert.IsType<ContainerTransferPayload>(loadedPutInContainer.Payload);

            Assert.Equal("bag-1", loadedRemoveFromContainer.TargetContainerId);
            Assert.IsType<ContainerTransferPayload>(loadedRemoveFromContainer.Payload);

            Assert.Equal(procedureId, loadedInvokeProcedure.ProcedureId);
            Assert.IsType<InvokeProcedurePayload>(loadedInvokeProcedure.Payload);

            Assert.Equal(materializeSourceObjectId, loadedMaterialize.MaterializeSourceObjectId);
            Assert.IsType<MaterializeObjectCopyPayload>(loadedMaterialize.Payload);

            var loadedStartTimerPayload = Assert.IsType<StartTimerPayload>(loadedStartTimer.Payload);
            Assert.Equal("door-close", loadedStartTimerPayload.TimerKey);
            Assert.Equal(TimerOwnerType.Room, loadedStartTimerPayload.OwnerScopeKindOverride);

            var loadedCancelTimerPayload = Assert.IsType<CancelTimerPayload>(loadedCancelTimer.Payload);
            Assert.Equal("door-close", loadedCancelTimerPayload.TimerKey);
            Assert.Equal(TimerOwnerType.Room, loadedCancelTimerPayload.ScopeQualifierKind);
            Assert.Equal(cancelTimerOwnerId, loadedCancelTimerPayload.ScopeQualifierId);

            Assert.Equal(synonymTargetId, loadedSynonym.SynonymTargetActionId);
            Assert.IsType<SynonymPayload>(loadedSynonym.Payload);

            var linkedPayload = Assert.IsType<LinkedFlowPayload>(loadedLinked.Payload);
            var linkedAction = Assert.Single(loadedLinked.LinkedActions);
            Assert.Equal(targetAction.Id, linkedAction.ActionId);
            Assert.Equal(LinkedActionRunWhen.Always, linkedAction.RunWhen);
            Assert.Single(linkedPayload.LinkedActions);
            Assert.Equal(targetAction.Id, linkedPayload.LinkedActions[0].ActionId);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadProjectModel_WhenPayloadTypeIsUnknown_ThrowsInvalidOperationExceptionWithRoomPath()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var action = new CommandAction
            {
                Name = "Synonym",
                ActionType = CommandActionType.Synonym,
                Payload = new SynonymPayload(Guid.NewGuid())
            };

            var room = new Room
            {
                Name = "Room A",
                AvailableActions = [action]
            };

            var area = new Area { Name = "Area A", Rooms = [room] };
            var country = new Country { Name = "Country A", Areas = [area] };
            var planet = new Planet { Name = "Planet A", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "PayloadTypeUnknown",
                Planets = [planet],
                StartingPlanetName = planet.Name
            };

            var projectFilePath = Path.Combine(tempRoot, "PayloadTypeUnknown.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var roomFilePath = Path.Combine(BuildRoomsFolderPath(projectFilePath), $"{room.Id:N}.room.json");
            var roomNode = JsonNode.Parse(File.ReadAllText(roomFilePath, Encoding.UTF8))!.AsObject();
            var actionNode = roomNode["availableGameActions"]!.AsArray().OfType<JsonObject>().Single();
            actionNode["payload"]!["$payloadType"] = "unknownType";
            File.WriteAllText(roomFilePath, roomNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

            var ex = Assert.Throws<InvalidOperationException>(() => service.TryLoadProjectModel(projectFilePath));
            Assert.Contains(roomFilePath, ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveAndLoadProjectModel_DoesNotPersistConditionalLinkedActions_OnNonLinkedActionTypes()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var followUp = new CommandAction
            {
                Name = "FollowUp",
                ActionType = CommandActionType.EchoMessage,
                Payload = new EchoPayload()
            };
            ActionPayloadAccessors.SetEchoMessage(followUp, "follow up");

            var first = new CommandAction
            {
                Name = "First",
                ActionType = CommandActionType.OpenObject,
                Payload = new EchoPayload(),
                LinkedActions =
                [
                    new LinkedActionReference
                    {
                        ActionId = followUp.Id,
                        RunWhen = LinkedActionRunWhen.OnFailure,
                        Order = 1
                    }
                ]
            };
            ActionPayloadAccessors.SetEchoMessage(first, "opened");

            var room = new Room
            {
                Name = "Room A",
                AvailableActions = [first, followUp]
            };

            var area = new Area { Name = "Area A", Rooms = [room] };
            var country = new Country { Name = "Country A", Areas = [area] };
            var planet = new Planet { Name = "Planet A", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "NonLinkedConditionalFlow",
                Planets = [planet],
                StartingPlanetName = planet.Name
            };

            var projectFilePath = Path.Combine(tempRoot, "NonLinkedConditionalFlow.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var roomFilePath = Path.Combine(BuildRoomsFolderPath(projectFilePath), $"{room.Id:N}.room.json");
            using var roomDoc = JsonDocument.Parse(File.ReadAllText(roomFilePath, Encoding.UTF8));
            var actions = roomDoc.RootElement.GetProperty("availableGameActions").EnumerateArray().ToList();
            var firstJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "First", StringComparison.Ordinal));

            Assert.Equal("openObject", firstJson.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.False(firstJson.TryGetProperty("linkedActions", out _));

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedRoom = Assert.Single(Assert.Single(Assert.Single(Assert.Single(loaded!.Planets).Countries).Areas).Rooms);
            var loadedFirst = loadedRoom.AvailableActions.Single(action => action.Name == "First");

            Assert.Empty(loadedFirst.LinkedActions);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveAndLoadProjectModel_PreservesConditionalSecondStep_ForLinkedFlowGraph()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var second = new CommandAction
            {
                Name = "Second",
                ActionType = CommandActionType.EchoMessage,
                Payload = new EchoPayload()
            };
            ActionPayloadAccessors.SetEchoMessage(second, "second");

            var first = new CommandAction
            {
                Name = "First",
                ActionType = CommandActionType.OpenObject,
                Payload = new EchoPayload(),
                LinkedActions =
                [
                    new LinkedActionReference
                    {
                        ActionId = second.Id,
                        RunWhen = LinkedActionRunWhen.OnFailure,
                        Order = 0
                    }
                ]
            };
            ActionPayloadAccessors.SetEchoMessage(first, "first");

            var flowRoot = new CommandAction
            {
                Name = "Root",
                ActionType = CommandActionType.LinkedActions,
                Payload = new LinkedFlowPayload(new List<LinkedActionReference>()),
                LinkedActions =
                [
                    new LinkedActionReference
                    {
                        ActionId = first.Id,
                        RunWhen = LinkedActionRunWhen.Always,
                        Order = 0
                    }
                ]
            };

            var room = new Room
            {
                Name = "Room A",
                AvailableActions = [flowRoot, first, second]
            };

            var area = new Area { Name = "Area A", Rooms = [room] };
            var country = new Country { Name = "Country A", Areas = [area] };
            var planet = new Planet { Name = "Planet A", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "LinkedFlowConditionalGraph",
                Planets = [planet],
                StartingPlanetName = planet.Name
            };

            var projectFilePath = Path.Combine(tempRoot, "LinkedFlowConditionalGraph.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedRoom = Assert.Single(Assert.Single(Assert.Single(Assert.Single(loaded!.Planets).Countries).Areas).Rooms);

            var loadedRoot = loadedRoom.AvailableActions.Single(action => action.Name == "Root");
            var loadedFirst = loadedRoom.AvailableActions.Single(action => action.Name == "First");
            var loadedSecond = loadedRoom.AvailableActions.Single(action => action.Name == "Second");

            var rootLink = Assert.Single(loadedRoot.LinkedActions);
            Assert.Equal(loadedFirst.Id, rootLink.ActionId);
            Assert.Equal(LinkedActionRunWhen.Always, rootLink.RunWhen);

            var secondStepLink = Assert.Single(loadedFirst.LinkedActions);
            Assert.Equal(loadedSecond.Id, secondStepLink.ActionId);
            Assert.Equal(LinkedActionRunWhen.OnFailure, secondStepLink.RunWhen);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string BuildRoomsFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, "Room");
    }
}
