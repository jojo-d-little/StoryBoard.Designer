namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopedProceduresNodeViewModel : HierarchyNodeViewModel
{
    public ScopedProceduresNodeViewModel(PropertyResolutionScope scope, IList<Guid> procedureIds, HierarchyNodeViewModel parent)
        : base("Procedures", parent)
    {
        Scope = scope;
        ProcedureIds = procedureIds;
        NodeTypeLabel = "Procedures";
    }

    public PropertyResolutionScope Scope { get; }
    public IList<Guid> ProcedureIds { get; }

    protected override void RenameModel(string newName)
    {
    }
}
