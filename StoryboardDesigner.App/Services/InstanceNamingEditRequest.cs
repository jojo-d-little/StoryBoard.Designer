namespace StoryboardDesigner.App.Services;

public sealed record InstanceNamingEditRequest(
    string BaseName,
    string BaseNameInGame,
    string BaseNameSynonyms,
    string InstanceName,
    string InstanceNameInGame,
    string InstanceNameSynonyms);
