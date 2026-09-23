using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;

namespace StoryboardDesigner.App.Services;

/// <summary>
/// Builds the established one-entry runtime registration document used by a Designer development launch.
/// </summary>
public static class DevelopmentLaunchRegistration
{
    /// <summary>
    /// The environment variable consumed by GameHost for an invocation-scoped registration catalog.
    /// </summary>
    public const string DiscoveryJsonPathEnvironmentVariable = "STORYBOARD_RUNTIME_GAME_DISCOVERY_JSON_PATH";

    /// <summary>
    /// The environment variable used to locate the host-served WebPortal bundle.
    /// </summary>
    public const string WebPortalRootEnvironmentVariable = "STORYBOARD_WEBPORTAL_ROOT";

    /// <summary>
    /// The environment variable optionally used to override the GameHost executable path.
    /// </summary>
    public const string GameHostExecutableEnvironmentVariable = "STORYBOARD_GAMEHOST_EXECUTABLE_PATH";

    /// <summary>
    /// The WebPortal query parameter that selects the Designer development bootstrap mode.
    /// </summary>
    public const string WebPortalLaunchModeQueryParameter = "mode";

    /// <summary>
    /// The WebPortal launch mode used by the Designer development GameHost action.
    /// </summary>
    public const string DevelopmentSimulatorLaunchMode = "devsimulator";

    /// <summary>
    /// The WebPortal query parameter that identifies the development username to authenticate.
    /// </summary>
    public const string DevelopmentUsernameQueryParameter = "username";

    /// <summary>
    /// The WebPortal query parameter that requests automatic session startup.
    /// </summary>
    public const string AutoStartSessionQueryParameter = "autoStartSession";

    /// <summary>
    /// The default local development username used when no Designer preference is configured.
    /// </summary>
    public const string DefaultDevelopmentUsername = "dev";

    /// <summary>
    /// Creates a stable development identity for a project path.
    /// </summary>
    /// <param name="projectFilePath">The authored project file path.</param>
    /// <returns>The deterministic development identity.</returns>
    public static DevelopmentLaunchIdentity CreateIdentity(string projectFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);

        var canonicalPath = Path.GetFullPath(projectFilePath.Trim());
        var identityBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"storyboard-designer-development\0{canonicalPath}"));
        var gameId = new Guid(identityBytes[..16]);
        var gameKey = $"designer.dev.{Convert.ToHexString(identityBytes[..10]).ToLowerInvariant()}";
        return new DevelopmentLaunchIdentity(gameId, gameKey);
    }

    /// <summary>
    /// Serializes an invocation-scoped one-entry registration catalog.
    /// </summary>
    /// <param name="identity">The development game identity.</param>
    /// <param name="runtimeProjectPath">The absolute runtime export path.</param>
    /// <returns>The registration catalog JSON.</returns>
    public static string Serialize(DevelopmentLaunchIdentity identity, string runtimeProjectPath)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeProjectPath);

        var document = new RuntimeGameRegistrationCatalogDocument
        {
            Games =
            [
                new RuntimeGameRegistrationEntryDocument
                {
                    GameId = identity.GameId,
                    GameKey = identity.GameKey,
                    RuntimeProjectPath = Path.GetFullPath(runtimeProjectPath.Trim()),
                    IsEnabled = true,
                    TenantId = "local-tenant",
                    OrgId = "local-org"
                }
            ]
        };

        return JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private sealed class RuntimeGameRegistrationCatalogDocument
    {
        public List<RuntimeGameRegistrationEntryDocument> Games { get; init; } = [];
    }

    private sealed class RuntimeGameRegistrationEntryDocument
    {
        public Guid GameId { get; init; }
        public string GameKey { get; init; } = string.Empty;
        public string RuntimeProjectPath { get; init; } = string.Empty;
        public bool IsEnabled { get; init; }
        public string TenantId { get; init; } = string.Empty;
        public string OrgId { get; init; } = string.Empty;
    }
}
