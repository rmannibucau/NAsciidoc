namespace NAsciidoc.Ascii2SVG;

public record Point(int X, int Y, Point.PointHint Hint)
{
    public override string ToString()
    {
        return $"({X}, {Y})";
    }

    public bool IsHorizontal(Point p2)
    {
        int d = X - p2.X;
        return d <= 1 && d >= -1 && Y == p2.Y;
    }

    public bool IsVertical(Point p2)
    {
        int d = Y - p2.Y;
        return d <= 1 && d >= -1 && X == p2.X;
    }

    public bool IsDiagonalSE(Point p2)
    {
        return (X - p2.X) == -1 && (Y - p2.Y) == -1;
    }

    public bool IsDiagonalSW(Point p2)
    {
        return (X - p2.X) == 1 && (Y - p2.Y) == -1;
    }

    public bool IsDiagonalNW(Point p2)
    {
        return (X - p2.X) == 1 && (Y - p2.Y) == 1;
    }

    public bool IsDiagonalNE(Point p2)
    {
        return (X - p2.X) == -1 && (Y - p2.Y) == 1;
    }

    public enum PointHint
    {
        NONE,
        ROUNDED_CORNER,
        START_MARKER,
        END_MARKER,
        TICK,
        DOT,
    }
}
