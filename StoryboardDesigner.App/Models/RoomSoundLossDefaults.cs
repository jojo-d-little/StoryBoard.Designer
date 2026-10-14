namespace StoryboardDesigner.App.Models;

/// <summary>Project sound-loss percentages applied when a room has no local override.</summary>
public sealed class RoomSoundLossDefaults
{
    public double IndoorSoundLossPercent { get; set; }
    public double OutdoorSoundLossPercent { get; set; }
    public double OpenSpaceSoundLossPercentPerCell { get; set; } = 0;
}
