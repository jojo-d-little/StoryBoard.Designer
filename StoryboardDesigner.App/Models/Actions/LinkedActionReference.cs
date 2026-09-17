namespace StoryboardDesigner.App.Models;

public sealed class LinkedActionReference
{
    public Guid ActionId { get; set; }
    public LinkedActionRunWhen RunWhen { get; set; } = LinkedActionRunWhen.OnSuccess;
    public int Order { get; set; }
}
