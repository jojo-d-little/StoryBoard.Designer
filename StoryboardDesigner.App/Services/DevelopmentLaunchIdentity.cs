namespace StoryboardDesigner.App.Services;

/// <summary>
/// Stable identity values shared by Designer, GameHost, and the downstream WebPortal attach pass.
/// </summary>
public sealed record DevelopmentLaunchIdentity(Guid GameId, string GameKey);
