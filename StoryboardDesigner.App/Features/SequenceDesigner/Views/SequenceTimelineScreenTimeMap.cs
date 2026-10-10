namespace StoryboardDesigner.App.Features.SequenceDesigner.Views;

internal sealed class SequenceTimelineScreenTimeMap
{
    internal readonly record struct TimelineCell(
        int OffsetMs,
        int RangeStartOffsetMs,
        int RangeEndOffsetMs,
        bool IsRange,
        bool HasSteps,
        double X,
        double Width);

    private const int SnapIntervalMs = 50;
    private readonly IReadOnlyList<TimelineCell> _cells;

    public SequenceTimelineScreenTimeMap(IEnumerable<TimelineCell> cells)
    {
        _cells = cells
            .Where(static cell => cell.Width > 0d)
            .OrderBy(static cell => cell.X)
            .ToList();
    }

    public int GetOffsetAtX(double x)
    {
        if (_cells.Count == 0)
        {
            return 0;
        }

        for (var index = 0; index < _cells.Count; index++)
        {
            var cell = _cells[index];
            if (x < cell.X || x >= cell.X + cell.Width)
            {
                continue;
            }

            if (cell.HasSteps)
            {
                return cell.OffsetMs;
            }

            var (startOffsetMs, endOffsetMs) = GetCellTimeInterval(index);
            var mappedOffset = cell.IsRange
                ? MapLinear(x, cell.X, cell.X + cell.Width, cell.RangeStartOffsetMs, cell.RangeEndOffsetMs)
                : MapLinear(x, cell.X, cell.X + cell.Width, startOffsetMs, endOffsetMs);
            var minimum = cell.IsRange ? cell.RangeStartOffsetMs : startOffsetMs;
            var maximum = cell.IsRange ? cell.RangeEndOffsetMs : endOffsetMs;
            return SnapOffset(mappedOffset, minimum, maximum);
        }

        var first = _cells[0];
        if (x < first.X)
        {
            return first.IsRange ? first.RangeStartOffsetMs : first.OffsetMs;
        }

        var lastIndex = _cells.Count - 1;
        var last = _cells[lastIndex];
        return last.IsRange
            ? last.RangeEndOffsetMs
            : GetCellTimeInterval(lastIndex).EndOffsetMs;
    }

    public double GetXAtOffset(int offsetMs)
    {
        if (_cells.Count == 0)
        {
            return 0d;
        }

        foreach (var cell in _cells)
        {
            if (cell.HasSteps && cell.OffsetMs == offsetMs)
            {
                return cell.X;
            }
        }

        for (var index = 0; index < _cells.Count; index++)
        {
            var cell = _cells[index];
            if (cell.HasSteps)
            {
                continue;
            }

            var startOffsetMs = cell.IsRange ? cell.RangeStartOffsetMs : cell.OffsetMs;
            var endOffsetMs = cell.IsRange
                ? cell.RangeEndOffsetMs
                : GetCellTimeInterval(index).EndOffsetMs;
            if (offsetMs < startOffsetMs || offsetMs > endOffsetMs)
            {
                continue;
            }

            return MapLinear(
                offsetMs,
                startOffsetMs,
                cell.X,
                endOffsetMs,
                cell.X + cell.Width);
        }

        var first = _cells[0];
        if (offsetMs < (first.IsRange ? first.RangeStartOffsetMs : first.OffsetMs))
        {
            return first.X;
        }

        var last = _cells[^1];
        return GetInterpolatedXBetweenAnchors(offsetMs) ?? last.X + last.Width;
    }

    private double? GetInterpolatedXBetweenAnchors(int offsetMs)
    {
        (int OffsetMs, double X)? left = null;
        (int OffsetMs, double X)? right = null;

        void ConsiderAnchor(int anchorOffsetMs, double anchorX)
        {
            if (anchorOffsetMs < offsetMs
                && (!left.HasValue || anchorOffsetMs > left.Value.OffsetMs))
            {
                left = (anchorOffsetMs, anchorX);
            }

            if (anchorOffsetMs > offsetMs
                && (!right.HasValue || anchorOffsetMs < right.Value.OffsetMs))
            {
                right = (anchorOffsetMs, anchorX);
            }
        }

        for (var index = 0; index < _cells.Count; index++)
        {
            var cell = _cells[index];
            if (cell.IsRange)
            {
                ConsiderAnchor(cell.RangeStartOffsetMs, cell.X);
                ConsiderAnchor(cell.RangeEndOffsetMs, cell.X + cell.Width);
            }
            else if (cell.HasSteps)
            {
                ConsiderAnchor(cell.OffsetMs, cell.X);
            }
            else
            {
                var (startOffsetMs, endOffsetMs) = GetCellTimeInterval(index);
                ConsiderAnchor(startOffsetMs, cell.X);
                ConsiderAnchor(endOffsetMs, cell.X + cell.Width);
            }
        }

        if (left.HasValue && right.HasValue)
        {
            return MapLinear(
                offsetMs,
                left.Value.OffsetMs,
                left.Value.X,
                right.Value.OffsetMs,
                right.Value.X);
        }

        return left?.X ?? right?.X;
    }

    private (int StartOffsetMs, int EndOffsetMs) GetCellTimeInterval(int index)
    {
        var cell = _cells[index];
        var startOffsetMs = cell.IsRange ? cell.RangeStartOffsetMs : cell.OffsetMs;
        if (index + 1 < _cells.Count)
        {
            var next = _cells[index + 1];
            var nextOffsetMs = next.IsRange ? next.RangeStartOffsetMs : next.OffsetMs;
            if (nextOffsetMs > startOffsetMs)
            {
                return (startOffsetMs, nextOffsetMs);
            }
        }

        return (startOffsetMs, Math.Max(startOffsetMs, startOffsetMs + SnapIntervalMs));
    }

    private static double MapLinear(
        double value,
        double fromValue,
        double toValue,
        double fromResult,
        double toResult)
    {
        if (toValue <= fromValue)
        {
            return toResult;
        }

        var fraction = Math.Clamp((value - fromValue) / (toValue - fromValue), 0d, 1d);
        return fromResult + (toResult - fromResult) * fraction;
    }

    private static double MapLinear(
        double value,
        int fromValue,
        double fromResult,
        int toValue,
        double toResult)
    {
        if (toValue <= fromValue)
        {
            return toResult;
        }

        var fraction = Math.Clamp((value - fromValue) / (toValue - fromValue), 0d, 1d);
        return fromResult + (toResult - fromResult) * fraction;
    }

    private static int SnapOffset(double offsetMs, int minimumOffsetMs, int maximumOffsetMs)
    {
        var snapped = (int)Math.Round(offsetMs / (double)SnapIntervalMs, MidpointRounding.AwayFromZero) * SnapIntervalMs;
        return Math.Clamp(snapped, minimumOffsetMs, maximumOffsetMs);
    }
}
