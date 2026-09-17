namespace StoryboardDesigner.App.Models;

public sealed class ProcedureDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Procedure";
    public string ProcedureSummary { get; set; } = string.Empty;
    public string ProcedureDescription { get; set; } = string.Empty;
    public List<ProcedureParticipantRequirement> Participants { get; set; } = new();
    public List<ProcedureParticipantMutation> ParticipantMutations { get; set; } = new();
}