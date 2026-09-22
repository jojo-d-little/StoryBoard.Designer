using System.Text.Json;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class DevelopmentLaunchRegistrationTests
{
    [Fact]
    public void CreateIdentity_IsStableForSameCanonicalProjectPath()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Project Folder With Spaces", "Example.sbe.json");

        var first = DevelopmentLaunchRegistration.CreateIdentity(projectPath);
        var second = DevelopmentLaunchRegistration.CreateIdentity(Path.Combine(Path.GetTempPath(), "Project Folder With Spaces", ".", "Example.sbe.json"));

        Assert.Equal(first, second);
        Assert.StartsWith("designer.dev.", first.GameKey);
        Assert.NotEqual(Guid.Empty, first.GameId);
    }

    [Fact]
    public void Serialize_UsesEstablishedOneEntryCatalogShapeAndAbsoluteExportPath()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Project Folder With Spaces", "Example.sbe.json");
        var runtimePath = Path.Combine(Path.GetTempPath(), "Project Folder With Spaces", "GameRuntimeJson", "Example.sbr.runtime.json");
        var identity = DevelopmentLaunchRegistration.CreateIdentity(projectPath);

        using var document = JsonDocument.Parse(DevelopmentLaunchRegistration.Serialize(identity, runtimePath));
        var games = document.RootElement.GetProperty("games");

        Assert.Equal(JsonValueKind.Array, games.ValueKind);
        var entry = Assert.Single(games.EnumerateArray());
        Assert.Equal(identity.GameId, entry.GetProperty("gameId").GetGuid());
        Assert.Equal(identity.GameKey, entry.GetProperty("gameKey").GetString());
        Assert.Equal(Path.GetFullPath(runtimePath), entry.GetProperty("runtimeProjectPath").GetString());
        Assert.True(entry.GetProperty("isEnabled").GetBoolean());
        Assert.Equal("local-tenant", entry.GetProperty("tenantId").GetString());
        Assert.Equal("local-org", entry.GetProperty("orgId").GetString());
    }
}
