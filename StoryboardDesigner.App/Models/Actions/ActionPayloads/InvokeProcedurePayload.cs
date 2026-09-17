namespace StoryboardDesigner.App.Models;

public sealed record InvokeProcedurePayload(Guid? ProcedureId) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.InvokeProcedure;
}