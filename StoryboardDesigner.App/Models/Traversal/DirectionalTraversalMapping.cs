namespace StoryboardDesigner.App.Models;

public sealed class DirectionalTraversalMapping
{
    public string Token { get; set; } = string.Empty;
    public Direction10 TraversalDirection { get; set; } = Direction10.North;
}