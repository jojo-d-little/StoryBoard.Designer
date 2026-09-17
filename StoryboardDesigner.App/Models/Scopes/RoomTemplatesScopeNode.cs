namespace StoryboardDesigner.App.Models;

public sealed class RoomTemplatesScopeNode : ScopeNodeBase
{
    private readonly ProjectModel _project;

    public RoomTemplatesScopeNode(ProjectModel project)
    {
        _project = project;
    }

    public override ScopeNodeKind ScopeKind => ScopeNodeKind.RoomTemplates;
    public override string ScopeName => "Room Templates";
    public override IEnumerable<string> ScopeTokens => new[] { "roomtemplates", "templates" };
    public override List<string> IgnoredValidationRuleIds
    {
        get => _project.RoomTemplatesIgnoredValidationRuleIds;
        set => _project.RoomTemplatesIgnoredValidationRuleIds = value ?? new List<string>();
    }
    public override IEnumerable<IScopedAwareNode> ChildScopes => _project.RoomTemplates;

    protected override bool CanAcceptChild(IScopedAwareNode child)
    {
        return child.ScopeKind == ScopeNodeKind.Room;
    }

    protected override bool TryAddChildCore(IScopedAwareNode child)
    {
        if (child is not Room room)
        {
            return false;
        }

        if (_project.RoomTemplates.Contains(room))
        {
            return true;
        }

        _project.RoomTemplates.Add(room);
        return true;
    }

    protected override bool TryRemoveChildCore(IScopedAwareNode child)
    {
        return child is Room room && _project.RoomTemplates.Remove(room);
    }
}
