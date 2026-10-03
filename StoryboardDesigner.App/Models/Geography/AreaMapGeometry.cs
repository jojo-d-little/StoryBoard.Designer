namespace StoryboardDesigner.App.Models;

public static class AreaMapGeometry
{
    public const double CellWidth = 180;
    public const double CellHeight = 140;
    public const double OriginX = 24;
    public const double OriginY = 24;
    public const double RoomCardWidth = 128;
    public const double RoomCardHeight = 96;

    public static bool IsValidSpan(int span) => span is >= 1 and <= 3;

    public static double CardWidth(int columnSpan) =>
        (Math.Clamp(columnSpan, 1, 3) - 1) * CellWidth + RoomCardWidth;

    public static double CardHeight(int rowSpan) =>
        (Math.Clamp(rowSpan, 1, 3) - 1) * CellHeight + RoomCardHeight;

    public static (int column, int row) ToGridCell(double x, double y) =>
        ((int)Math.Round((x - OriginX) / CellWidth, MidpointRounding.AwayFromZero),
         (int)Math.Round((y - OriginY) / CellHeight, MidpointRounding.AwayFromZero));

    public static AreaMapFootprint FromPlacement(AreaRoomPlacement placement)
    {
        var (column, row) = ToGridCell(placement.X, placement.Y);
        return new AreaMapFootprint(column, row,
            Math.Clamp(placement.ColumnSpan, 1, 3),
            Math.Clamp(placement.RowSpan, 1, 3),
            placement.FloorElevation);
    }
}
