# Package Versioning Plan

## Designer package references

The Designer intentionally uses floating prerelease package ranges so a restore can pick up the latest local prerelease:

- `Storyboard.GameEngine`: `1.0.*-*`
- `Storyboard.Contracts`: `1.0.*-*`
- `Storyboard.Foundation`: `0.1.*-*`

Keep these ranges in `StoryboardDesigner.App/StoryboardDesigner.App.csproj`. Do not replace them with exact versions when refreshing packages or rebuilding. Refresh through NuGet restore and rebuild while preserving the ranges. Only change this policy when the user explicitly asks for a pinned version.
