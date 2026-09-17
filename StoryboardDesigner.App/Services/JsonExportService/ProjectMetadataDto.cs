using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;
internal sealed class ProjectMetadataDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

