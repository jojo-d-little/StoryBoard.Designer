namespace StoryboardDesigner.App.Models;

public sealed class ProjectUiState
{
    public string PlanetName { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    public Guid? MapDesignerAreaId { get; set; }
    public Guid? RoomId { get; set; }
    public int SelectedWorkspaceTabIndex { get; set; }
    public string LastSelectedNodePath { get; set; } = string.Empty;
    public string LastTreeValidationActionId { get; set; } = string.Empty;
    public string LastTreeValidationCompletionMode { get; set; } = string.Empty;
}
