namespace NAsciidoc.Ascii2SVG;

public record Object(
    Point[] Points,
    Point[] Corners,
    bool IsText,
    bool IsTagDefinition,
    bool IsClosed,
    bool IsDashed,
    char[] Text,
    string Tag
) : IComparable<Object>
{
    public bool hasPoint(Point p)
    {
        bool hasPoint = false;
        int nCorners = Corners.Length;
        int j = nCorners - 1;
        for (int i = 0; i < nCorners; i++)
        {
            if (
                (
                    Corners[i].Y < p.Y && Corners[j].Y >= p.Y
                    || Corners[j].Y < p.Y && Corners[i].Y >= p.Y
                ) && (Corners[i].X <= p.X || Corners[j].X <= p.X)
            )
            {
                if (
                    Corners[i].X
                        + (p.Y - Corners[i].Y)
                            / (Corners[j].Y - Corners[i].Y)
                            * (Corners[j].X - Corners[i].X)
                    < p.X
                )
                {
                    hasPoint = !hasPoint;
                }
            }
            j = i;
        }
        return hasPoint;
    }

    public Object Seal(Canvas c)
    {
        var points = (Point[])Points.Clone();
        var text = new char[Points.Length];
        if (c.At(points[0]).IsArrow())
        {
            points[0] = new Point(points[0].X, points[0].Y, Point.PointHint.START_MARKER);
        }

        if (c.At(points[points.Length - 1]).IsArrow())
        {
            points[points.Length - 1] = new Point(
                points[^1].X,
                points[^1].Y,
                Point.PointHint.END_MARKER
            );
        }

        var cornersAndClosed = pointsToCorners(points);

        int i = 0;
        bool isDashed = false;
        foreach (var p in Points)
        {
            if (!IsText)
            {
                if (c.At(p).IsTick())
                {
                    points[i] = new Point(p.X, p.Y, Point.PointHint.TICK);
                }
                else if (c.At(p).IsDot())
                {
                    points[i] = new Point(p.X, p.Y, Point.PointHint.DOT);
                }

                if (c.At(p).IsDashed())
                {
                    isDashed = true;
                }

                foreach (var corner in cornersAndClosed.Points)
                {
                    if (corner.X == p.X && corner.Y == p.Y && c.At(p).IsRoundedCorner())
                    {
                        points[i] = new Point(p.X, p.Y, Point.PointHint.ROUNDED_CORNER);
                    }
                }
            }
            text[i] = c.At(p).Value;
            i++;
        }

        return new Object(
            points,
            cornersAndClosed.Points,
            IsText,
            IsTagDefinition,
            cornersAndClosed.Closed,
            isDashed,
            text,
            Tag
        );
    }

    public PointState pointsToCorners(Point[] points)
    {
        if (points.Length < 3)
        {
            return new PointState(points, false);
        }

        var res = new Point[] { points[0] };
        var dir = Dir.NONE;
        if (points[0].IsHorizontal(points[1]))
        {
            dir = Dir.H;
        }
        else if (points[0].IsVertical(points[1]))
        {
            dir = Dir.V;
        }
        else if (points[0].IsDiagonalSE(points[1]))
        {
            dir = Dir.SE;
        }
        else if (points[0].IsDiagonalSW(points[1]))
        {
            dir = Dir.SW;
        }
        else if (points[0].IsDiagonalNW(points[1]))
        {
            dir = Dir.NW;
        }
        else if (points[0].IsDiagonalNE(points[1]))
        {
            dir = Dir.NE;
        }
        else
        {
            throw new ArgumentException($"discontiguous points: {Points}");
        }

        for (int i = 2; i < points.Length; i++)
        {
            Action<int, Dir> cornerFunc = (idx, newDir) =>
            {
                if (dir != newDir)
                {
                    res = [.. res, Points[idx - 1]];
                    dir = newDir;
                }
            };
            if (points[i - 1].IsHorizontal(points[i]))
            {
                cornerFunc(i, Dir.H);
            }
            else if (points[i - 1].IsVertical(points[i]))
            {
                cornerFunc(i, Dir.V);
            }
            else if (points[i - 1].IsDiagonalSE(points[i]))
            {
                cornerFunc(i, Dir.SE);
            }
            else if (points[i - 1].IsDiagonalSW(points[i]))
            {
                cornerFunc(i, Dir.SW);
            }
            else if (points[i - 1].IsDiagonalNW(points[i]))
            {
                cornerFunc(i, Dir.NW);
            }
            else if (points[i - 1].IsDiagonalNE(points[i]))
            {
                cornerFunc(i, Dir.NE);
            }
            else
            {
                throw new ArgumentException($"discontiguous points: {Points}");
            }
        }

        var last = points[points.Length - 1];
        var closed = true;
        Action<Dir> closedFunc = newDir =>
        {
            if (dir != newDir)
            {
                closed = false;
                res = [.. res, last];
            }
        };
        if (points[0].IsHorizontal(last))
        {
            closedFunc(Dir.H);
        }
        else if (points[0].IsVertical(last))
        {
            closedFunc(Dir.V);
        }
        else if (last.IsDiagonalNE(points[0]))
        {
            closedFunc(Dir.NE);
        }
        else
        {
            closed = false;
            res = [.. res, last];
        }

        return new PointState(res, closed);
    }

    public int CompareTo(Object? o)
    {
        if (o is null)
        {
            return 1;
        }
        if (IsText != o.IsText)
        {
            return IsText ? 1 : -1;
        }
        var topDiff = Points[0].Y - o.Points[0].Y;
        if (topDiff != 0)
        {
            return topDiff;
        }
        return Points[0].X - o.Points[0].X;
    }

    public record List(Object[] Value) { }

    public enum Dir
    {
        NONE,
        H,
        V,
        SE,
        SW,
        NW,
        NE
    }

    public record PointState(Point[] Points, bool Closed) { }
}
