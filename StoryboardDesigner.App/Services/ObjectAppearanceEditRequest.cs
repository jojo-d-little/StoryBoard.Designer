namespace StoryboardDesigner.App.Services;

public sealed record ObjectAppearanceEditRequest(
    string FullImagePath,
    string GrayMapImagePath,
    string NormalMapImagePath,
    double RotationDegrees,
    double Scale,
    double LocalOffsetX,
    double LocalOffsetY);
