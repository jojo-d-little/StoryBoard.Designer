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
