using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class ActionManifestCatalogTests
{
    [Fact]
    public void Load_MissingIndex_ReportsRequiredManifestLocationAndCause()
    {
        var configDirectory = CreateTemporaryConfigDirectory();
        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => ActionManifestCatalog.Load(configDirectory));

            Assert.Contains("required action authoring metadata", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Path.Combine(configDirectory, "action-manifest.index.json"), exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(exception.InnerException);
        }
        finally
        {
            Directory.Delete(configDirectory, recursive: true);
        }
    }

    [Fact]
    public void Load_InvalidIndex_PreservesLoaderDiagnostic()
    {
        var configDirectory = CreateTemporaryConfigDirectory();
        try
        {
            File.WriteAllText(Path.Combine(configDirectory, "action-manifest.index.json"), "{");

            var exception = Assert.Throws<InvalidOperationException>(() => ActionManifestCatalog.Load(configDirectory));

            Assert.Contains("action-manifest.index.json", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(exception.InnerException);
            Assert.Contains("action-manifest.index.json", exception.InnerException.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(configDirectory, recursive: true);
        }
    }

    [Fact]
    public void GetOutputVariableSuggestions_OffersCollectionFormsOnlyWhenDeclared()
    {
        var suggestions = ActionManifestCatalog.GetOutputVariableSuggestions(
            [nameof(CommandActionType.BuildCompositeByParts), nameof(CommandActionType.NavigateDirection)]);
        var keys = suggestions.Select(static suggestion => suggestion.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("missingParts", keys);
        Assert.Contains("missingPartsCount", keys);
        Assert.Contains("missingParts[n]", keys);
        Assert.Contains("success", keys);
        Assert.DoesNotContain("successCount", keys);
        Assert.DoesNotContain("success[n]", keys);
    }

    [Fact]
    public void GetPayloadArgumentSuggestions_IncludesManifestBackedEventInputKeys()
    {
        var suggestions = ActionManifestCatalog.GetPayloadArgumentSuggestions();

        Assert.Contains(suggestions, suggestion => suggestion.ActionType == nameof(CommandActionType.PlaySoundEffect)
            && suggestion.Key == "sound.play.overrideSoundEffectKey");
        Assert.Contains(suggestions, suggestion => suggestion.ActionType == nameof(CommandActionType.CancelSoundEffect)
            && suggestion.Key == "sound.cancel.playRequestInstanceId");
        Assert.Contains(suggestions, suggestion => suggestion.ActionType == nameof(CommandActionType.AddObjectPresentationCue)
            && suggestion.Key == "effectKey");
    }

    private static string CreateTemporaryConfigDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"designer-action-manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
