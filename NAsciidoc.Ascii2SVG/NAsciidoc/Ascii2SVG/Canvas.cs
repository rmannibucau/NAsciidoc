using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace NAsciidoc.Ascii2SVG;

[JsonSourceGenerationOptions()]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonArray))]
[JsonSerializable(typeof(JsonNode))]
internal partial class LightJsonContext : JsonSerializerContext { }

public partial record Canvas(
    char[] Grid,
    int[] Size,
    bool[] Visited,
    Object.List Objects,
    IDictionary<string, object> Options
)
{
    [GeneratedRegex("(\\d+)\\s*,\\s*(\\d+)$")]
    internal static partial Regex ObjTagRe();

    public static Canvas NewInstance(string data, int tabWidth, bool noBlur)
    {
        var options = new Dictionary<string, object>
        {
            {
                "__a2s__closed__options__",
                noBlur
                    ? new Dictionary<string, object> { { "fill", "#fff" } }
                    : new Dictionary<string, object>
                    {
                        { "fill", "#fff" },
                        { "filter", "url(#dsFilter)" }
                    }
            }
        };
        var indent = new string(' ', tabWidth);
        var lines = data.Split('\n').Select(it => it.Replace("\n", indent)).ToArray();
        var size = new int[] { lines.Select(it => it.Length).Max(), lines.Length };
        var grid = new char[size[0] * size[1]];
        var visited = new bool[size[0] * size[1]];
        int y = 0;
        foreach (var line in lines)
        {
            var padding = y * size[0];
            for (int x = 0; x < line.Length; x++)
            {
                grid[padding + x] = line[x];
            }
            if (line.Length < size[1])
            {
                for (int x = line.Length; x < size[1]; x++)
                {
                    grid[padding + x] = ' ';
                }
            }
            y++;
        }

        var from = new Canvas(
            grid,
            size,
            visited,
            new Object.List([]),
            new Dictionary<string, object>(options)
        );
        var found = from.FindObjects();
        return new Canvas(from.Grid, from.Size, from.Visited, new Object.List(found), from.Options);
    }

    public Char At(Point p)
    {
        return At(p.X, p.Y);
    }

    public Char At(int x, int y)
    {
        return new Char(Grid[y * Size[0] + x]);
    }

    private bool IsVisited(int x, int y)
    {
        return Visited[y * Size[0] + x];
    }

    private void Visit(int x, int y)
    {
        Visited[y * Size[0] + x] = true;
    }

    private void Unvisit(int x, int y)
    {
        var idx = y * Size[0] + x;
        if (!Visited[idx])
        {
            throw new InvalidOperationException($"Can't unvisit a cell you didn't visit: #{idx}");
        }
        Visited[idx] = false;
    }

    private bool CanLeft(int x)
    {
        return x > 0;
    }

    private bool CanRight(int x)
    {
        return x < Size[0] - 1;
    }

    private bool CanUp(int y)
    {
        return y > 0;
    }

    private bool CanDown(int y)
    {
        return y < Size[1] - 1;
    }

    private bool CanDiagonal(int x, int y)
    {
        return (CanLeft(x) || CanRight(x)) && (CanUp(y) || CanDown(y));
    }

    private Point[] Next(Point pos)
    {
        if (!IsVisited(pos.X, pos.Y))
        {
            throw new InvalidOperationException($"internal error; revisiting {pos}");
        }

        Point[]? res = null;
        var ch = At(pos);
        if (ch.CanHorizontal())
        {
            Action<Point> nextHorizontal = p =>
            {
                if (!IsVisited(p.X, p.Y) && At(p).CanHorizontal())
                {
                    res = [.. (res ?? []), p];
                }
            };
            if (CanLeft(pos.X))
            {
                nextHorizontal(new Point(pos.X - 1, pos.Y, pos.Hint));
            }
            if (CanRight(pos.X))
            {
                nextHorizontal(new Point(pos.X + 1, pos.Y, pos.Hint));
            }
        }
        if (ch.CanVertical())
        {
            Action<Point> nextVertical = p =>
            {
                if (!IsVisited(p.X, p.Y) && At(p).CanVertical())
                {
                    res = [.. (res ?? []), p];
                }
            };
            if (CanUp(pos.Y))
            {
                nextVertical(new Point(pos.X, pos.Y - 1, pos.Hint));
            }
            if (CanDown(pos.Y))
            {
                nextVertical(new Point(pos.X, pos.Y + 1, pos.Hint));
            }
        }
        if (CanDiagonal(pos.X, pos.Y))
        {
            Action<Point, Point> nextDiagonal = (from, to) =>
            {
                if (!IsVisited(to.X, to.Y) && At(to).CanDiagonalFrom(At(from)))
                {
                    res = [.. (res ?? []), to];
                }
            };
            if (CanUp(pos.Y))
            {
                if (CanLeft(pos.X))
                {
                    nextDiagonal(pos, new Point(pos.X - 1, pos.Y - 1, pos.Hint));
                }
                if (CanRight(pos.X))
                {
                    nextDiagonal(pos, new Point(pos.X + 1, pos.Y - 1, pos.Hint));
                }
            }
            if (CanDown(pos.Y))
            {
                if (CanLeft(pos.X))
                {
                    nextDiagonal(pos, new Point(pos.X - 1, pos.Y + 1, pos.Hint));
                }
                if (CanRight(pos.X))
                {
                    nextDiagonal(pos, new Point(pos.X + 1, pos.Y + 1, pos.Hint));
                }
            }
        }

        return res!;
    }

    private Object.List? ScanPath(Point[] points)
    {
        var cur = points[^1];
        var next = Next(cur);
        if (next == null || next.Length == 0)
        {
            if (points.Length == 1)
            {
                Unvisit(cur.X, cur.Y);
                return null;
            }
            var o = new Object(points, [], false, false, false, false, [], "").Seal(this);
            return new Object.List([o]);
        }

        if (cur.X == points[0].X && cur.Y == points[0].Y + 1)
        {
            var res = new Object[]
            {
                new Object(points, [], false, false, false, false, [], "").Seal(this)
            };
            var list = ScanPath([cur]);
            if (list != null && list.Value.Length > 0)
            {
                foreach (var it in list.Value)
                {
                    res = [.. res, it];
                }
            }
            return new Object.List(res);
        }

        Object[]? objs = null;
        foreach (var n in next)
        {
            if (IsVisited(n.X, n.Y))
            {
                continue;
            }
            Visit(n.X, n.Y);
            Point[] p2 = [.. points, n];
            var toAdd = ScanPath(p2)?.Value ?? [];
            foreach (var it in toAdd)
            {
                objs = [.. (objs ?? []), it];
            }
        }
        return objs == null ? null : new Object.List(objs);
    }

    public Object.List? EnclosingObjects(Object[] objects, Point p)
    {
        var maxTL = new int[] { -1, -1 };

        Object[]? q = null;
        foreach (var o in objects)
        {
            if (!o.IsClosed)
            {
                continue;
            }

            if (o.hasPoint(p) && o.Corners[0].X > maxTL[0] && o.Corners[0].Y > maxTL[1])
            {
                q = [.. (q ?? []), o];
                maxTL[0] = o.Corners[0].X;
                maxTL[1] = o.Corners[0].Y;
            }
        }

        return q == null ? null : new Object.List(q);
    }

    private Object ScanText(Object[] objects, int x, int y)
    {
        var points = new List<Point> { new(x, y, Point.PointHint.NONE) };

        int whiteSpaceStreak = 0;
        int[] cur = [x, y];

        int tagged = 0;
        var tag = Array.Empty<char>();
        var tagDef = Array.Empty<char>();

        while (CanRight(cur[0]))
        {
            if (cur[0] == x && At(cur[0], cur[1]).IsObjectStartTag())
            {
                tagged++;
            }
            else if (cur[0] > x && At(cur[0], cur[1]).IsObjectEndTag())
            {
                tagged++;
            }

            cur[0]++;
            if (IsVisited(cur[0], cur[1]) && (tagDef.Length == 0 || tagDef[^1] == '}'))
            {
                break;
            }
            var ch = At(cur[0], cur[1]);
            if (!ch.IsTextCont())
            {
                break;
            }
            if (tagged == 0 && ch.IsSpace())
            {
                whiteSpaceStreak++;
                if (whiteSpaceStreak > 2)
                {
                    break;
                }
            }
            else
            {
                whiteSpaceStreak = 0;
            }

            switch (tagged)
            {
                case 1:
                    if (!At(cur[0], cur[1]).IsObjectEndTag())
                    {
                        tag = [.. tag, ch.Value];
                    }
                    break;
                case 2:
                    if (At(cur[0], cur[1]).IsTagDefinitionSeparator())
                    {
                        tagged++;
                    }
                    else
                    {
                        tagged = -1;
                    }
                    break;
                case 3:
                    tagDef = [.. tagDef, ch.Value];
                    break;
                default:
                    break;
            }

            points.Add(new Point(cur[0], cur[1], Point.PointHint.NONE));
        }

        // If we found a start and end tag marker, we either need to assign the tag to the object,
        // or we need to assign the specified options to the global Canvas option space.
        if (tagged == 2 || (tagged < 0 && tag.Length > 0))
        {
            var t = new string(tag);
            var container = EnclosingObjects(objects, new Point(x, y, Point.PointHint.NONE));
            if (container != null && container.Value.Length > 0)
            {
                var from = container.Value[0];
                var idx = Array.IndexOf(objects, from);
                objects[idx] = new Object(
                    from.Points,
                    from.Corners,
                    from.IsText,
                    from.IsTagDefinition,
                    from.IsClosed,
                    from.IsDashed,
                    from.Text,
                    t
                );
            }
        }
        else if (tagged == 3)
        {
            var t = new string(tag);

            var matcher = ObjTagRe().Match(t);
            if (matcher.Success)
            {
                var targetX = int.Parse(matcher.Groups[1].Value);
                var targetY = int.Parse(matcher.Groups[2].Value);
                int idx = 0;
                foreach (var o in objects)
                {
                    var corner = o.Corners[0];
                    if (corner.X == targetX && corner.Y == targetY)
                    {
                        objects[idx] = new Object(
                            o.Points,
                            o.Corners,
                            o.IsText,
                            o.IsTagDefinition,
                            o.IsClosed,
                            o.IsDashed,
                            o.Text,
                            new string(tag)
                        );
                        break;
                    }
                    idx++;
                }
            }

            var jsonValue = new string(tagDef).Trim();
            if (!string.IsNullOrWhiteSpace(jsonValue))
            {
                var m = JsonSerializer.Deserialize(jsonValue, LightJsonContext.Default.JsonObject)!;
                tag = t.ToCharArray();
                Options[t] = m;
            }
        }

        while (points.Count > 0 && At(points[^1]).IsSpace())
        {
            points.RemoveAt(points.Count - 1);
        }

        return new Object(
            [.. points],
            [],
            true,
            tagDef.Length > 0,
            false,
            false,
            [],
            new string(tag)
        ).Seal(this);
    }

    private Object[] FindObjects()
    {
        var objects = Objects.Value;
        for (int y = 0; y < Size[1]; y++)
        {
            for (int x = 0; x < Size[0]; x++)
            {
                if (IsVisited(x, y))
                {
                    continue;
                }
                var ch = At(x, y);
                if (ch.IsPathStart())
                {
                    Visit(x, y);
                    var objs = ScanPath([new Point(x, y, Point.PointHint.NONE)]);
                    if (objs != null && objs.Value.Length > 0)
                    {
                        foreach (var o in objs.Value)
                        {
                            foreach (var p in o.Points)
                            {
                                Visit(p.X, p.Y);
                            }
                        }
                        foreach (var o in objs.Value)
                        {
                            objects = [.. objects, o];
                        }
                    }
                }
            }
        }

        for (int y = 0; y < Size[1]; y++)
        {
            for (int x = 0; x < Size[0]; x++)
            {
                if (IsVisited(x, y))
                {
                    continue;
                }
                var ch = At(x, y);
                if (ch.IsTextStart())
                {
                    var obj = ScanText(objects, x, y);
                    if (obj == null)
                    { // unlikely
                        continue;
                    }
                    foreach (var p in obj.Points)
                    {
                        Visit(p.X, p.Y);
                    }
                    objects = [.. objects, obj];
                }
            }
        }

        return [.. objects.Order()];
    }
}
