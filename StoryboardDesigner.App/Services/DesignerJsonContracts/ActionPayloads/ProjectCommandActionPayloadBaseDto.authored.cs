using System.Text.Json.Serialization;

namespace StoryboardDesigner.App.Services;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$payloadType")]
[JsonDerivedType(typeof(ProjectEchoMessageActionPayloadDto), typeDiscriminator: "echoMessage")]
[JsonDerivedType(typeof(ProjectBreakCompositeItemActionPayloadDto), typeDiscriminator: "breakCompositeItem")]
[JsonDerivedType(typeof(ProjectBuildCompositeByPartsActionPayloadDto), typeDiscriminator: "buildCompositeByParts")]
[JsonDerivedType(typeof(ProjectBuildCompositeByTargetActionPayloadDto), typeDiscriminator: "buildCompositeByTarget")]
[JsonDerivedType(typeof(ProjectCancelSoundEffectActionPayloadDto), typeDiscriminator: "cancelSoundEffect")]
[JsonDerivedType(typeof(ProjectCancelTimerActionPayloadDto), typeDiscriminator: "cancelTimer")]
[JsonDerivedType(typeof(ProjectCheckGamePropertyActionPayloadDto), typeDiscriminator: "checkGameProperty")]
[JsonDerivedType(typeof(ProjectInvokeProcedureActionPayloadDto), typeDiscriminator: "invokeProcedure")]
[JsonDerivedType(typeof(ProjectClearActiveRoomObjectsActionPayloadDto), typeDiscriminator: "clearActiveRoomObjects")]
[JsonDerivedType(typeof(ProjectCloseObjectActionPayloadDto), typeDiscriminator: "closeObject")]
[JsonDerivedType(typeof(ProjectLockObjectActionPayloadDto), typeDiscriminator: "lockObject")]
[JsonDerivedType(typeof(ProjectMaterializeObjectCopyActionPayloadDto), typeDiscriminator: "materializeObjectCopy")]
[JsonDerivedType(typeof(ProjectMoveRoomObjectByPointsActionPayloadDto), typeDiscriminator: "moveRoomObjectByPoints")]
[JsonDerivedType(typeof(ProjectMoveRoomObjectOnGridActionPayloadDto), typeDiscriminator: "moveRoomObjectOnGrid")]
[JsonDerivedType(typeof(ProjectNavigateDirectionActionPayloadDto), typeDiscriminator: "navigateDirection")]
[JsonDerivedType(typeof(ProjectNavigateToAdjacentActionPayloadDto), typeDiscriminator: "navigateToAdjacent")]
[JsonDerivedType(typeof(ProjectOpenObjectActionPayloadDto), typeDiscriminator: "openObject")]
[JsonDerivedType(typeof(ProjectPutObjectInContainerActionPayloadDto), typeDiscriminator: "putObjectInContainer")]
[JsonDerivedType(typeof(ProjectRemoveObjectFromContainerActionPayloadDto), typeDiscriminator: "removeObjectFromContainer")]
[JsonDerivedType(typeof(ProjectRotateRoomObjectOnGridActionPayloadDto), typeDiscriminator: "rotateRoomObjectOnGrid")]
[JsonDerivedType(typeof(ProjectSelectRoomObjectByPointActionPayloadDto), typeDiscriminator: "selectRoomObjectByPoint")]
[JsonDerivedType(typeof(ProjectSetActiveRoomObjectActionPayloadDto), typeDiscriminator: "setActiveRoomObject")]
[JsonDerivedType(typeof(ProjectSetGamePropertyActionPayloadDto), typeDiscriminator: "setGameProperty")]
[JsonDerivedType(typeof(ProjectSetFlagActionPayloadDto), typeDiscriminator: "setFlag")]
[JsonDerivedType(typeof(ProjectStackRoomObjectOnAnotherActionPayloadDto), typeDiscriminator: "stackRoomObjectOnAnother")]
[JsonDerivedType(typeof(ProjectStartTimerActionPayloadDto), typeDiscriminator: "startTimer")]
[JsonDerivedType(typeof(ProjectSynonymActionPayloadDto), typeDiscriminator: "synonym")]
[JsonDerivedType(typeof(ProjectUnlockObjectActionPayloadDto), typeDiscriminator: "unlockObject")]
[JsonDerivedType(typeof(ProjectLinkedFlowActionPayloadDto), typeDiscriminator: "linkedFlow")]
[JsonDerivedType(typeof(ProjectClearRoomObjectSelectionsActionPayloadDto), typeDiscriminator: "clearRoomObjectSelections")]
internal partial class ProjectCommandActionPayloadBaseDto
{
}
