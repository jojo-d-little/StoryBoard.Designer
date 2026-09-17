namespace StoryboardDesigner.App.Models;

public sealed class SharedVariableDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string DefaultValue { get; set; } = string.Empty;
    public GamePropertyValueRestriction ValueRestriction { get; set; } = GamePropertyValueRestriction.Unrestricted;
    public List<SharedVariableParticipant> Participants { get; set; } = new();
}
