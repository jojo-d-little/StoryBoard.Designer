using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class GlobalObjectsNodeViewModel : GameObjectsNodeViewModel
{
    private readonly ScopeNodeBase _globalScopeNode;

    public GlobalObjectsNodeViewModel(ProjectModel project, HierarchyNodeViewModel parent)
        : base("Game Objects", parent)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        _globalScopeNode = Project;
    }

    public ProjectModel Project { get; }
    public override List<GameObject> GameObjects => Project.GlobalScope.GameObjects;
    public override IScopedAwareNode ScopeNode => _globalScopeNode;
}
