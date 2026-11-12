namespace StoryboardDesigner.App.Services;

/// <summary>Runs Simulator workflows using the runtime project produced by Designer.</summary>
public interface IExternalSimulatorWorkflowService
{
    /// <summary>Starts Simulator using the runtime file produced by the current save/export operation.</summary>
    /// <param name="request">The Simulator launch request.</param>
    /// <returns>The launch result.</returns>
    RunExternalSimulatorResult RunSimulator(RunExternalSimulatorRequest request);

    /// <summary>Opens Simulator setup and applies accepted machine preferences.</summary>
    /// <param name="request">The setup dialog request.</param>
    /// <returns>The setup result.</returns>
    SimulatorSetupDialogResult OpenSimulatorSetup(SimulatorSetupDialogRequest request);
}
