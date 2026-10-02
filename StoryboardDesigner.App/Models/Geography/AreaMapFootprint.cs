namespace StoryboardDesigner.App.Models;

/// <summary>Grid geometry shared by map placement, adjacency, and presentation.</summary>
public readonly record struct AreaMapFootprint(int Column, int Row, int ColumnSpan, int RowSpan, int FloorElevation)
{
    public int Right => Column + ColumnSpan;
    public int Bottom => Row + RowSpan;

    public bool Fits(int columns, int rows) =>
        Column >= 0 && Row >= 0 && Right <= columns && Bottom <= rows;

    public bool Intersects(AreaMapFootprint other) =>
        Column < other.Right && Right > other.Column && Row < other.Bottom && Bottom > other.Row;

    public bool Overlaps(AreaMapFootprint other) =>
        FloorElevation == other.FloorElevation && Intersects(other);

    public bool TryGetAdjacentOffset(AreaMapFootprint other, bool allowDiagonals, out int dx, out int dy)
    {
        if (FloorElevation != other.FloorElevation)
        {
            dx = 0;
            dy = 0;
            return false;
        }

        dx = Right == other.Column ? 1 : other.Right == Column ? -1 : 0;
        dy = Bottom == other.Row ? 1 : other.Bottom == Row ? -1 : 0;

        var rowOverlap = Row < other.Bottom && Bottom > other.Row;
        var columnOverlap = Column < other.Right && Right > other.Column;
        if ((dx != 0 && dy == 0 && rowOverlap)
            || (dy != 0 && dx == 0 && columnOverlap)
            || (allowDiagonals && dx != 0 && dy != 0))
        {
            return true;
        }

        dx = 0;
        dy = 0;
        return false;
    }
}

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
