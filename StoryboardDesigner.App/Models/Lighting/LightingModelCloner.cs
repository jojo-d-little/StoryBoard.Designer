namespace StoryboardDesigner.App.Models;

internal static class LightingModelCloner
{
    public static PointLight? ClonePointLight(PointLight? source)
    {
        return source is null
            ? null
            : new PointLight
            {
                OffsetXPx = source.OffsetXPx,
                OffsetYPx = source.OffsetYPx,
                RadiusPx = source.RadiusPx,
                DirectionDeg = source.DirectionDeg,
                ConeAngleDeg = source.ConeAngleDeg,
                MotionMode = source.MotionMode,
                Phase = source.Phase,
                UseInstancePhaseShifting = source.UseInstancePhaseShifting,
                Color = source.Color,
                OuterColor = source.OuterColor,
                GradientExponent = source.GradientExponent,
                IntensityScale = source.IntensityScale,
                LightHeightCells = source.LightHeightCells,
                SwayAmountPx = source.SwayAmountPx,
                SwayHz = source.SwayHz,
                SwayDirectionDeg = source.SwayDirectionDeg,
                FlickerAmount = source.FlickerAmount,
                FlickerHz = source.FlickerHz,
                FlickerStyle = source.FlickerStyle
            };
    }

    public static PointLightDefaults? ClonePointLightDefaults(PointLightDefaults? source)
    {
        return source is null
            ? null
            : new PointLightDefaults
            {
                RadiusPx = source.RadiusPx,
                IntensityScale = source.IntensityScale,
                Color = source.Color,
                OuterColor = source.OuterColor,
                GradientExponent = source.GradientExponent,
                LightHeightCells = source.LightHeightCells,
                SwayAmountPx = source.SwayAmountPx,
                SwayHz = source.SwayHz,
                SwayDirectionDeg = source.SwayDirectionDeg,
                FlickerAmount = source.FlickerAmount,
                FlickerHz = source.FlickerHz,
                FlickerStyle = source.FlickerStyle
            };
    }

    public static RoomAmbientLighting? CloneRoomAmbientLighting(RoomAmbientLighting? source)
    {
        return source is null
            ? null
            : new RoomAmbientLighting
            {
                Ambient = source.Ambient,
                AmbientColor = source.AmbientColor
            };
    }

    public static PhaseAmbientOverlay? ClonePhaseAmbientOverlay(PhaseAmbientOverlay? source)
    {
        return source is null
            ? null
            : new PhaseAmbientOverlay
            {
                AmbientScale = source.AmbientScale,
                Color = source.Color,
                ColorMode = source.ColorMode,
                ColorBlend = source.ColorBlend
            };
    }

    public static LightOcclusion? CloneLightOcclusion(LightOcclusion? source)
    {
        return source is null
            ? null
            : new LightOcclusion
            {
                Strength = source.Strength
            };
    }
}
