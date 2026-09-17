namespace StoryboardDesigner.App.Orchestration.SaveGuard;

public interface ISaveGuardPolicyService
{
    CloseWorkflowSaveGuardDecision EvaluateCloseProjectPolicy(bool isProjectDirty, bool saveIfDirty);
}