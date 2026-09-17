using System;
using System.Collections.Generic;
using System.Linq;
using Storyboard.Shared.GameServices;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public static class CommandActionSummaryPresenter
{
    public static string BuildSummary(CommandAction action)
    {
        return BuildSummaryCore(action, previewMode: false);
    }

    public static string BuildSummaryPreview(CommandAction action)
    {
        return BuildSummaryCore(action, previewMode: true);
    }

    public static string BuildSummaryTooltip(CommandAction action)
    {
        if (action.ActionType == CommandActionType.EchoMessage)
        {
            var fullEcho = ActionPayloadAccessors.GetEchoMessage(action);
            if (!string.IsNullOrWhiteSpace(fullEcho))
            {
                return fullEcho;
            }
        }

        return BuildSummary(action);
    }

    private static string BuildSummaryCore(CommandAction action, bool previewMode)
    {
        var triggerSummary = BuildTriggerSummary(action);
        var actionSummary = action.ActionType switch
        {
            CommandActionType.LinkedActions => BuildLinkedFlowSummary(action),
            CommandActionType.Synonym => BuildSynonymSummary(action),
            CommandActionType.EchoMessage => previewMode ? BuildEchoPreviewSummary(action) : ActionPayloadAccessors.GetEchoMessage(action),
            CommandActionType.SetFlag => $"{ActionPayloadAccessors.GetSetFlagName(action)} = {ActionPayloadAccessors.GetSetFlagValue(action)}",
            CommandActionType.CheckGameProperty => $"{ActionPayloadAccessors.GetCheckPropertyName(action)} == {ActionPayloadAccessors.GetCheckExpectedValue(action)}",
            CommandActionType.SetGameProperty => $"{ActionPayloadAccessors.GetSetPropertyName(action)} = {ActionPayloadAccessors.GetSetPropertyValue(action)}",
            CommandActionType.NavigateDirection => "Navigate via traversal",
            CommandActionType.MoveRoomObjectOnGrid => BuildMoveRoomObjectOnGridSummary(action),
            CommandActionType.MoveRoomObjectByPoints => BuildMoveRoomObjectByPointsSummary(action),
            CommandActionType.RotateRoomObjectOnGrid => BuildRotateRoomObjectOnGridSummary(action),
            CommandActionType.StackRoomObjectOnAnother => BuildStackRoomObjectOnAnotherSummary(action),
            CommandActionType.SelectRoomObjectByPoint => BuildSelectRoomObjectByPointSummary(action),
            CommandActionType.ClearRoomObjectSelections => BuildClearRoomObjectSelectionsSummary(action),
            CommandActionType.PutObjectInContainer => BuildPutObjectInContainerSummary(action),
            CommandActionType.RemoveObjectFromContainer => BuildRemoveObjectFromContainerSummary(action),
            CommandActionType.InvokeProcedure => BuildInvokeProcedureSummary(action),
            CommandActionType.BuildCompositeByTarget => BuildCompositeByTargetSummary(action),
            CommandActionType.BuildCompositeByParts => BuildCompositeByPartsSummary(action),
            CommandActionType.BreakCompositeItem => BuildBreakCompositeSummary(action),
            _ => string.Empty
        };

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(triggerSummary))
        {
            parts.Add(triggerSummary);
        }

        if (!string.IsNullOrWhiteSpace(actionSummary))
        {
            parts.Add(actionSummary);
        }

        var summary = string.Join(" | ", parts);
        var forwardingSummary = BuildForwardingSummary(action);
        var outcomeEchoSummary = BuildOutcomeEchoSummary(action);
        if (string.IsNullOrWhiteSpace(forwardingSummary))
        {
            if (string.IsNullOrWhiteSpace(outcomeEchoSummary))
            {
                return previewMode ? ToSingleLine(summary) : summary;
            }

            if (string.IsNullOrWhiteSpace(summary))
            {
                return previewMode ? ToSingleLine(outcomeEchoSummary) : outcomeEchoSummary;
            }

            var value = $"{summary} | {outcomeEchoSummary}";
            return previewMode ? ToSingleLine(value) : value;
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            if (string.IsNullOrWhiteSpace(outcomeEchoSummary))
            {
                return previewMode ? ToSingleLine(forwardingSummary) : forwardingSummary;
            }

            var value = $"{forwardingSummary} | {outcomeEchoSummary}";
            return previewMode ? ToSingleLine(value) : value;
        }

        if (string.IsNullOrWhiteSpace(outcomeEchoSummary))
        {
            var value = $"{summary} | {forwardingSummary}";
            return previewMode ? ToSingleLine(value) : value;
        }

        var combined = $"{summary} | {forwardingSummary} | {outcomeEchoSummary}";
        return previewMode ? ToSingleLine(combined) : combined;
    }

    private static string BuildTriggerSummary(CommandAction action)
    {
        if (action.NoVerbLinkage)
        {
            return "No verb linkage";
        }

        var verbs = action.VerbListText;
        var direction = action.DirectionQualifierText;
        if (!string.IsNullOrWhiteSpace(direction)
            && GameCommandDirectionFormatting.TryParseToken(direction, out var parsedDirection))
        {
            direction = parsedDirection.ToDisplayLabel();
        }

        if (string.IsNullOrWhiteSpace(verbs) && string.IsNullOrWhiteSpace(direction))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(verbs))
        {
            return $"Direction: {direction}";
        }

        if (string.IsNullOrWhiteSpace(direction))
        {
            return $"Verbs: {verbs}";
        }

        return $"Verbs: {verbs}; Direction: {direction}";
    }

    private static string BuildLinkedFlowSummary(CommandAction action)
    {
        var linkedActions = ActionPayloadAccessors.GetLinkedActions(action);
        if (linkedActions.Count == 0)
        {
            return "Flow: no links";
        }

        var successCount = linkedActions.Count(link => link.RunWhen == LinkedActionRunWhen.OnSuccess);
        var failureCount = linkedActions.Count(link => link.RunWhen == LinkedActionRunWhen.OnFailure);
        var alwaysCount = linkedActions.Count(link => link.RunWhen == LinkedActionRunWhen.Always);
        return $"Flow links (success:{successCount}, failure:{failureCount}, always:{alwaysCount})";
    }

    private static string BuildSynonymSummary(CommandAction action)
    {
        var synonymTargetId = ActionPayloadAccessors.GetSynonymTargetActionId(action);
        return synonymTargetId.HasValue
            ? $"Synonym -> {synonymTargetId.Value:N}"
            : "Synonym -> (target not set)";
    }

    private static string BuildPutObjectInContainerSummary(CommandAction action)
    {
        var payload = ActionPayloadAccessors.GetContainerTransfer(action);
        var target = string.IsNullOrWhiteSpace(payload.TargetContainerId) ? "(target not set)" : payload.TargetContainerId;
        var success = string.IsNullOrWhiteSpace(action.GetOutcomeScript("Success")) ? "success msg: none" : "success msg: configured";
        var failure = string.IsNullOrWhiteSpace(action.GetOutcomeScript("Failure")) ? "failure msg: none" : "failure msg: configured";
        return $"Put in -> {target}; {success}; {failure}";
    }

    private static string BuildRemoveObjectFromContainerSummary(CommandAction action)
    {
        var payload = ActionPayloadAccessors.GetContainerTransfer(action);
        var source = string.IsNullOrWhiteSpace(payload.TargetContainerId) ? "(source not set)" : payload.TargetContainerId;
        var success = string.IsNullOrWhiteSpace(action.GetOutcomeScript("Success")) ? "success msg: none" : "success msg: configured";
        var failure = string.IsNullOrWhiteSpace(action.GetOutcomeScript("Failure")) ? "failure msg: none" : "failure msg: configured";
        return $"Remove from -> {source}; {success}; {failure}";
    }

    private static string BuildMoveRoomObjectOnGridSummary(CommandAction action)
    {
        var payload = ActionPayloadAccessors.GetMoveRoomObjectOnGridPayload(action);
        var direction = string.IsNullOrWhiteSpace(payload.DirectionToken) ? "heading" : payload.DirectionToken;
        var partial = payload.AllowPartialMove ? "allow partial" : "full only";
        var jump = payload.AllowJumpOver ? "jump-over path" : "no jump-over";
        return $"Move -> dir: {direction}; cells: {payload.DistanceInCells}; {partial}; {jump}; hint: {payload.VisualTransitionHint}";
    }

    private static string BuildMoveRoomObjectByPointsSummary(CommandAction action)
    {
        var payload = ActionPayloadAccessors.GetMoveRoomObjectByPointsPayload(action);
        var partial = payload.AllowPartialMove ? "allow partial" : "full only";
        var jump = payload.AllowJumpOver ? "jump-over path" : "no jump-over";
        return $"Move by points -> {partial}; {jump}; hint: {payload.VisualTransitionHint}";
    }

    private static string BuildRotateRoomObjectOnGridSummary(CommandAction action)
    {
        var payload = ActionPayloadAccessors.GetRotateRoomObjectOnGridPayload(action);
        if (payload.Mode == RuntimeRotateRoomObjectOnGridAttemptMode.Face)
        {
            var facing = string.IsNullOrWhiteSpace(payload.FacingDirectionToken)
                ? "(unset)"
                : payload.FacingDirectionToken;
            return $"Rotate -> mode: Face; facing: {facing}; hint: {payload.VisualTransitionHint}";
        }

        var turnText = payload.TurnDegrees.HasValue
            ? payload.TurnDegrees.Value.ToString()
            : "(from command)";
        return $"Rotate -> mode: Turn; degrees: {turnText}; hint: {payload.VisualTransitionHint}";
    }

    private static string BuildStackRoomObjectOnAnotherSummary(CommandAction action)
    {
        var payload = ActionPayloadAccessors.GetStackRoomObjectOnAnotherPayload(action);
        return $"Stack -> subject: command/object scope; target: command secondary; hint: {payload.VisualTransitionHint}";
    }

    private static string BuildSelectRoomObjectByPointSummary(CommandAction action)
    {
        var payload = ActionPayloadAccessors.GetSelectRoomObjectByPointPayload(action);
        var cue = string.IsNullOrWhiteSpace(payload.SelectionCueEffectKey)
            ? "(none)"
            : payload.SelectionCueEffectKey;
        return $"Select by point -> cue: {cue}";
    }

    private static string BuildClearRoomObjectSelectionsSummary(CommandAction action)
    {
        var payload = ActionPayloadAccessors.GetClearRoomObjectSelectionsPayload(action);
        return $"Clear selections -> scope: {payload.ClearScope}";
    }

    private static string BuildCompositeByTargetSummary(CommandAction action)
    {
        var payload = ActionPayloadAccessors.GetCompositeByTargetPayload(action);
        var target = payload.CompositeTargetObjectId.HasValue ? payload.CompositeTargetObjectId.Value.ToString("N") : "(target not set)";
        var parts = payload.CompositeRequiredPartObjectIds.Count;
        var success = string.IsNullOrWhiteSpace(action.GetOutcomeScript("Success")) ? "success msg: none" : "success msg: configured";
        var failure = string.IsNullOrWhiteSpace(action.GetOutcomeScript("Failure")) ? "failure msg: none" : "failure msg: configured";
        return $"Build target -> {target}; parts: {parts}; {success}; {failure}";
    }

    private static string BuildCompositeByPartsSummary(CommandAction action)
    {
        var payload = ActionPayloadAccessors.GetCompositeByPartsPayload(action);
        var target = payload.CompositeTargetObjectId.HasValue ? payload.CompositeTargetObjectId.Value.ToString("N") : "(target not set)";
        var parts = payload.CompositeRequiredPartObjectIds.Count;
        var mode = string.IsNullOrWhiteSpace(payload.CompositeMatchMode) ? "ExactPartSet" : payload.CompositeMatchMode;
        var ambiguity = string.IsNullOrWhiteSpace(payload.CompositeAmbiguityPolicy) ? "FailWithHint" : payload.CompositeAmbiguityPolicy;
        var minimum = payload.CompositeMinimumRequiredPartCount.HasValue ? $"; min: {payload.CompositeMinimumRequiredPartCount.Value}" : string.Empty;
        return $"Build parts -> {target}; parts: {parts}; mode: {mode}{minimum}; ambiguity: {ambiguity}";
    }

    private static string BuildBreakCompositeSummary(CommandAction action)
    {
        var payload = ActionPayloadAccessors.GetBreakCompositePayload(action);
        var target = payload.CompositeTargetObjectId.HasValue ? payload.CompositeTargetObjectId.Value.ToString("N") : "(target not set)";
        var success = string.IsNullOrWhiteSpace(action.GetOutcomeScript("Success")) ? "success msg: none" : "success msg: configured";
        var failure = string.IsNullOrWhiteSpace(action.GetOutcomeScript("Failure")) ? "failure msg: none" : "failure msg: configured";
        return $"Break target -> {target}; {success}; {failure}";
    }

    private static string BuildInvokeProcedureSummary(CommandAction action)
    {
        var procedureId = ActionPayloadAccessors.GetProcedureId(action);
        return procedureId.HasValue
            ? $"Invoke procedure -> {procedureId.Value:N}"
            : "Invoke procedure -> (procedure not set)";
    }

    private static string BuildForwardingSummary(CommandAction action)
    {
        var forwarding = action.ChildCommandForwardingMode switch
        {
            ChildCommandForwardingMode.ChildrenBeforeParent => "Forward: children before parent",
            ChildCommandForwardingMode.ChildrenAfterParent => "Forward: children after parent",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(forwarding))
        {
            return string.Empty;
        }

        var dispatch = action.SimilarChildDispatchMode == SimilarChildDispatchMode.AllMatchingChildren
            ? "Dispatch: all similar"
            : "Dispatch: single similar";

        return $"{forwarding}; {dispatch}";
    }

    private static string BuildOutcomeEchoSummary(CommandAction action)
    {
        var hasSuccess = action.OutcomeMessageMap.TryGetValue("Success", out var successScript)
            && !string.IsNullOrWhiteSpace(successScript);
        var hasFailure = action.OutcomeMessageMap.TryGetValue("Failure", out var failureScript)
            && !string.IsNullOrWhiteSpace(failureScript);

        return (hasSuccess, hasFailure) switch
        {
            (false, false) => string.Empty,
            (true, true) => "Outcome echo: success+failure",
            (true, false) => "Outcome echo: success",
            (false, true) => "Outcome echo: failure"
        };
    }

    private static string BuildEchoPreviewSummary(CommandAction action)
    {
        var firstLine = ToSingleLine(ActionPayloadAccessors.GetEchoMessage(action));
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return string.Empty;
        }

        return firstLine;
    }

    private static string ToSingleLine(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var firstLine = normalized.Split('\n', StringSplitOptions.None).FirstOrDefault() ?? string.Empty;
        return firstLine.Trim();
    }
}
