namespace StoryboardDesigner.App.ViewModels;

public sealed class CountrySettingsNodeViewModel : HierarchyNodeViewModel
{
    public CountrySettingsNodeViewModel(CountryNodeViewModel countryNode)
        : base("Country Settings", countryNode)
    {
        CountryNode = countryNode;
        NodeTypeLabel = "Country Settings";
    }

    public CountryNodeViewModel CountryNode { get; }

    protected override void RenameModel(string newName)
    {
    }
}
