namespace StoryboardDesigner.App.Orchestration.SaveGuard;

internal sealed class SaveGuardPolicyService : ISaveGuardPolicyService
{
    public CloseWorkflowSaveGuardDecision EvaluateCloseProjectPolicy(bool isProjectDirty, bool saveIfDirty)
    {
        if (!isProjectDirty)
        {
            return new CloseWorkflowSaveGuardDecision(CloseWorkflowSaveGuardDecisionKind.Proceed, null);
        }

        if (saveIfDirty)
        {
            return new CloseWorkflowSaveGuardDecision(CloseWorkflowSaveGuardDecisionKind.AttemptSave, null);
        }

        return new CloseWorkflowSaveGuardDecision(
            CloseWorkflowSaveGuardDecisionKind.Block,
            "Close project blocked because unsaved changes are present and saveIfDirty is false.");
    }
}