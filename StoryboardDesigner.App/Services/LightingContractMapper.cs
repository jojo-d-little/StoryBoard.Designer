using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

internal static class LightingContractMapper
{
    public static ProjectPointLightDto? ToProjectPointLightDto(PointLight? source)
    {
        return source is null
            ? null
            : new ProjectPointLightDto
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
                SwayStyle = source.SwayStyle,
                FlickerAmount = source.FlickerAmount,
                FlickerHz = source.FlickerHz,
                FlickerStyle = source.FlickerStyle
            };
    }

    public static PointLight? ToPointLightModel(ProjectPointLightDto? source)
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
                SwayStyle = source.SwayStyle,
                FlickerAmount = source.FlickerAmount,
                FlickerHz = source.FlickerHz,
                FlickerStyle = source.FlickerStyle
            };
    }

    public static ProjectPointLightDefaultsDto? ToProjectPointLightDefaultsDto(PointLightDefaults? source)
    {
        return source is null
            ? null
            : new ProjectPointLightDefaultsDto
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
                SwayStyle = source.SwayStyle,
                FlickerAmount = source.FlickerAmount,
                FlickerHz = source.FlickerHz,
                FlickerStyle = source.FlickerStyle
            };
    }

    public static PointLightDefaults? ToPointLightDefaultsModel(ProjectPointLightDefaultsDto? source)
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
                SwayStyle = source.SwayStyle,
                FlickerAmount = source.FlickerAmount,
                FlickerHz = source.FlickerHz,
                FlickerStyle = source.FlickerStyle
            };
    }

    public static ProjectRoomAmbientLightingDto? ToProjectRoomAmbientLightingDto(RoomAmbientLighting? source)
    {
        return source is null
            ? null
            : new ProjectRoomAmbientLightingDto
            {
                Ambient = source.Ambient,
                AmbientColor = source.AmbientColor
            };
    }

    public static RoomAmbientLighting? ToRoomAmbientLightingModel(ProjectRoomAmbientLightingDto? source)
    {
        return source is null
            ? null
            : new RoomAmbientLighting
            {
                Ambient = source.Ambient,
                AmbientColor = source.AmbientColor
            };
    }

    public static ProjectPhaseAmbientOverlayDto? ToProjectPhaseAmbientOverlayDto(PhaseAmbientOverlay? source)
    {
        return source is null
            ? null
            : new ProjectPhaseAmbientOverlayDto
            {
                AmbientScale = source.AmbientScale,
                Color = source.Color,
                ColorMode = source.ColorMode,
                ColorBlend = source.ColorMode == PhaseAmbientColorMode.Blend ? source.ColorBlend : null
            };
    }

    public static PhaseAmbientOverlay? ToPhaseAmbientOverlayModel(ProjectPhaseAmbientOverlayDto? source)
    {
        return source is null
            ? null
            : new PhaseAmbientOverlay
            {
                AmbientScale = source.AmbientScale,
                Color = source.Color,
                ColorMode = source.ColorMode,
                ColorBlend = source.ColorMode == PhaseAmbientColorMode.Blend ? source.ColorBlend : null
            };
    }

    public static ProjectLightOcclusionDto? ToProjectLightOcclusionDto(LightOcclusion? source)
    {
        return source is null
            ? null
            : new ProjectLightOcclusionDto
            {
                Strength = source.Strength
            };
    }

    public static LightOcclusion? ToLightOcclusionModel(ProjectLightOcclusionDto? source)
    {
        return source is null
            ? null
            : new LightOcclusion
            {
                Strength = source.Strength
            };
    }

    public static RuntimePointLightDto? ToRuntimePointLightDto(PointLight? source)
    {
        return source is null
            ? null
            : new RuntimePointLightDto
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
                SwayStyle = source.SwayStyle,
                FlickerAmount = source.FlickerAmount,
                FlickerHz = source.FlickerHz,
                FlickerStyle = source.FlickerStyle
            };
    }

    public static RuntimePointLightDefaultsDto? ToRuntimePointLightDefaultsDto(PointLightDefaults? source)
    {
        return source is null
            ? null
            : new RuntimePointLightDefaultsDto
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
                SwayStyle = source.SwayStyle,
                FlickerAmount = source.FlickerAmount,
                FlickerHz = source.FlickerHz,
                FlickerStyle = source.FlickerStyle
            };
    }

    public static RuntimeRoomAmbientLightingDto? ToRuntimeRoomAmbientLightingDto(RoomAmbientLighting? source)
    {
        return source is null
            ? null
            : new RuntimeRoomAmbientLightingDto
            {
                Ambient = source.Ambient,
                AmbientColor = source.AmbientColor
            };
    }

    public static RuntimePhaseAmbientOverlayDto? ToRuntimePhaseAmbientOverlayDto(PhaseAmbientOverlay? source)
    {
        return source is null
            ? null
            : new RuntimePhaseAmbientOverlayDto
            {
                AmbientScale = source.AmbientScale,
                Color = source.Color,
                ColorMode = source.ColorMode,
                ColorBlend = source.ColorMode == PhaseAmbientColorMode.Blend ? source.ColorBlend : null
            };
    }

    public static RuntimeLightOcclusionDto? ToRuntimeLightOcclusionDto(LightOcclusion? source)
    {
        return source is null
            ? null
            : new RuntimeLightOcclusionDto
            {
                Strength = source.Strength
            };
    }
}
