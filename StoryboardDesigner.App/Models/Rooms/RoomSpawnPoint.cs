namespace StoryboardDesigner.App.Models;

/// <summary>Room-space pixel coordinate and optional render order for the single runtime player object.</summary>
public sealed record RoomSpawnPoint(double X, double Y, int? RenderZOrder = null);
