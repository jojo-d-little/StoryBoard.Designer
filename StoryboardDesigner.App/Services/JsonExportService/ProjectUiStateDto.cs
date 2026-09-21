using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;
internal sealed class ProjectUiStateDto
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
    public double QuickAccessRecentSectionRatio { get; set; } = 0.5;
    public List<ProjectHierarchyQuickAccessEntryDto> RecentHierarchyNodes { get; set; } = [];
    public List<ProjectHierarchyQuickAccessEntryDto> HierarchyBookmarks { get; set; } = [];
}

