using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopedProcedureEntryNodeViewModel : HierarchyNodeViewModel
{
    public ScopedProcedureEntryNodeViewModel(ProcedureDefinition procedure, ScopedProceduresNodeViewModel parent)
        : base(string.IsNullOrWhiteSpace(procedure.Name) ? "Procedure" : procedure.Name, parent)
    {
        Procedure = procedure;
        ParentProceduresNode = parent;
        NodeTypeLabel = "Procedure";
    }

    public ProcedureDefinition Procedure { get; }
    public ScopedProceduresNodeViewModel ParentProceduresNode { get; }

    protected override void RenameModel(string newName)
    {
    }
}
