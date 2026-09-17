namespace StoryboardDesigner.App.Services;

public interface IExternalSimulatorWorkflowService
{
    RunExternalSimulatorResult RunSimulator(RunExternalSimulatorRequest request);

    SimulatorSetupDialogResult OpenSimulatorSetup(SimulatorSetupDialogRequest request);
}
