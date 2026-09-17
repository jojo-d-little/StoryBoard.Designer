namespace StoryboardDesigner.App.Models;

public sealed class SharedVariableParticipant
{
    public string Kind { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public string VariableName { get; set; } = string.Empty;
    public string? Leg { get; set; }
}
