using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NAsciidoc.Ascii2SVG;

public class Svg
{
    public string Convert(
        string text,
        int tabWidth = 8,
        bool noBlur = true,
        string font = "monospace",
        int scaleX = 9,
        int scaleY = 16
    )
    {
        return Convert(Canvas.NewInstance(text, tabWidth, noBlur), noBlur, font, scaleX, scaleY);
    }

    public string Convert(Canvas c, bool noBlur, string font, int scaleX, int scaleY)
    {
        var result = new StringBuilder()
            .Append(
                "<!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.1//EN\" \"http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd\">\n"
            )
            .Append("<svg width=\"")
            .Append((c.Size[0] + 1) * scaleX)
            .Append("px\" height=\"")
            .Append((c.Size[1] + 1) * scaleY)
            .Append("px\" ")
            .Append(
                "version=\"1.1\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">\n"
            )
            .Append(
                "  <defs>\n"
                    + "    <filter id=\"dsFilter\" width=\"150%\" height=\"150%\">\n"
                    + "      <feOffset result=\"offOut\" in=\"SourceGraphic\" dx=\"2\" dy=\"2\"/>\n"
                    + "      <feColorMatrix result=\"matrixOut\" in=\"offOut\" type=\"matrix\" values=\"0.2 0 0 0 0 0 0.2 0 0 0 0 0 0.2 0 0 0 0 0 1 0\"/>\n"
                    + "      <feGaussianBlur result=\"blurOut\" in=\"matrixOut\" stdDeviation=\"3\"/>\n"
                    + "      <feBlend in=\"SourceGraphic\" in2=\"blurOut\" mode=\"normal\"/>\n"
                    + "    </filter>\n"
                    + "    <marker id=\"iPointer\"\n"
                    + "      viewBox=\"0 0 10 10\" refX=\"5\" refY=\"5\"\n"
                    + "      markerUnits=\"strokeWidth\"\n"
                    + "      markerWidth=\""
            )
            .Append(scaleX - 1)
            .Append("\" markerHeight=\"")
            .Append(scaleY - 1)
            .Append("\"\n")
            .Append("      orient=\"auto\">\n")
            .Append("      <path d=\"M 10 0 L 10 10 L 0 5 z\" />\n")
            .Append("    </marker>\n")
            .Append("    <marker id=\"Pointer\"\n")
            .Append("      viewBox=\"0 0 10 10\" refX=\"5\" refY=\"5\"\n")
            .Append("      markerUnits=\"strokeWidth\"\n")
            .Append("      markerWidth=\"")
            .Append(scaleX - 1)
            .Append("\" markerHeight=\"")
            .Append(scaleY - 1)
            .Append("\"\n")
            .Append("      orient=\"auto\">\n")
            .Append("      <path d=\"M 0 0 L 10 5 L 0 10 z\" />\n")
            .Append("    </marker>\n")
            .Append("  </defs>\n");

        string getOpts(string tag)
        {
            c.Options.TryGetValue(tag, out var value);
            if (value is null)
            {
                return "";
            }
            if (value is IDictionary<string, object> d)
            {
                return string.Join(
                    ' ',
                    d.Where(it => !it.Key.StartsWith("a2s:"))
                        .Select(it => $"{it.Key}=\"{it.Value}\"")
                );
            }
            if (value is JsonObject json)
            {
                return string.Join(
                    ' ',
                    json.AsEnumerable()
                        .Where(it => !it.Key.StartsWith("a2s:"))
                        .Select(it => $"{it.Key}=\"{it.Value}\"")
                );
            }
            return value.ToString()!;
        }

        if (noBlur)
        {
            result.Append("  <g id=\"closed\" stroke=\"#000\" stroke-width=\"2\" fill=\"none\">\n");
        }
        else
        {
            result.Append(
                "  <g id=\"closed\" filter=\"url(#dsFilter)\" stroke=\"#000\" stroke-width=\"2\" fill=\"none\">\n"
            );
        }

        int i = 0;
        foreach (var obj in c.Objects.Value)
        {
            if (obj.IsClosed && !obj.IsText)
            {
                var opts = "";
                if (obj.IsDashed)
                {
                    opts = "stroke-dasharray=\"5 5\" ";
                }

                var tag = obj.Tag;
                if (string.IsNullOrEmpty(tag))
                {
                    tag = "__a2s__closed__options__";
                }
                opts += getOpts(tag);

                var startLink = "";
                var endLink = "";
                if (c.Options.TryGetValue("a2s:link", out var link))
                {
                    var s = link.ToString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        startLink = "<a href=\"" + s + "\">";
                        endLink = "</a>";
                    }
                }

                result
                    .Append("    ")
                    .Append(startLink)
                    .Append("<path id=\"closed")
                    .Append(i)
                    .Append("\" ")
                    .Append(opts.Length > 0 ? opts + ' ' : "")
                    .Append("d=\"")
                    .Append(Flatten(obj.Points, scaleX, scaleY))
                    .Append("Z\" />")
                    .Append(endLink)
                    .Append('\n');
            }
            i++;
        }
        result.Append("  </g>\n");
        result.Append("  <g id=\"lines\" stroke=\"#000\" stroke-width=\"2\" fill=\"none\">\n");

        i = 0;
        foreach (var obj in c.Objects.Value)
        {
            try
            {
                if (!obj.IsClosed && !obj.IsText)
                {
                    var points = obj.Points;

                    var opts = "";
                    if (obj.IsDashed)
                    {
                        opts += "stroke-dasharray=\"5 5\" ";
                    }
                    if (points[0].Hint == Point.PointHint.START_MARKER)
                    {
                        opts += "marker-start=\"url(#iPointer)\" ";
                    }
                    if (points[^1].Hint == Point.PointHint.END_MARKER)
                    {
                        opts += "marker-end=\"url(#Pointer)\" ";
                    }

                    foreach (var p in points)
                    {
                        if (p.Hint is Point.PointHint.NONE)
                        {
                            continue;
                        }
                        switch (p.Hint)
                        {
                            case Point.PointHint.DOT:
                            {
                                var sp = scale(p, scaleX, scaleY);
                                result
                                    .Append("    <circle cx=\"")
                                    .Append(sp[0].ToString(CultureInfo.InvariantCulture))
                                    .Append("\" cy=\"")
                                    .Append(sp[1].ToString(CultureInfo.InvariantCulture))
                                    .Append("\" r=\"3\" fill=\"#000\" />\n");
                                break;
                            }
                            case Point.PointHint.TICK:
                            {
                                var sp = scale(p, scaleX, scaleY);
                                result
                                    .Append("    <line x1=\"")
                                    .Append((sp[0] - 4).ToString(CultureInfo.InvariantCulture))
                                    .Append("\" y1=\"")
                                    .Append((sp[1] - 4).ToString(CultureInfo.InvariantCulture))
                                    .Append("\" x2=\"")
                                    .Append((sp[0] + 4).ToString(CultureInfo.InvariantCulture))
                                    .Append("\" y2=\"")
                                    .Append((sp[1] + 4).ToString(CultureInfo.InvariantCulture))
                                    .Append("\" stroke-width=\"1\" />\n")
                                    .Append("    <line x1=\"")
                                    .Append((sp[0] + 4).ToString(CultureInfo.InvariantCulture))
                                    .Append("\" y1=\"")
                                    .Append((sp[1] - 4).ToString(CultureInfo.InvariantCulture))
                                    .Append("\" x2=\"")
                                    .Append((sp[0] - 4).ToString(CultureInfo.InvariantCulture))
                                    .Append("\" y2=\"")
                                    .Append((sp[1] + 4).ToString(CultureInfo.InvariantCulture))
                                    .Append("\" stroke-width=\"1\" />\n");
                                break;
                            }
                            default:
                                break;
                        }
                    }

                    opts += getOpts(obj.Tag);

                    var startLink = "";
                    var endLink = "";
                    if (c.Options.TryGetValue("a2s:link", out var link))
                    {
                        var s = link.ToString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            startLink = "<a href=\"" + s + "\">";
                            endLink = "</a>";
                        }
                    }

                    result
                        .Append("    ")
                        .Append(startLink)
                        .Append("<path id=\"open")
                        .Append(i)
                        .Append("\" ")
                        .Append(opts.Length > 0 ? opts + ' ' : "")
                        .Append("d=\"")
                        .Append(Flatten(obj.Points, scaleX, scaleY))
                        .Append("\" />")
                        .Append(endLink)
                        .Append('\n');
                }
            }
            finally
            {
                i++;
            }
        }
        result.Append("  </g>\n");
        result
            .Append("  <g id=\"text\" stroke=\"none\" style=\"font-family:")
            .Append(font)
            .Append(";font-size:15.2px\" >\n");

        string findTextColor(Object o)
        {
            var matcher = Canvas.ObjTagRe().Match(o.Tag);
            if (matcher.Success)
            {
                if (c.Options.TryGetValue(o.Tag, out var value))
                {
                    var s = value.ToString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        return s;
                    }
                }
            }

            var containers = c.EnclosingObjects(c.Objects.Value, o.Points[0]);
            if (containers != null && containers.Value != null)
            {
                foreach (var container in containers.Value)
                {
                    if (c.Options.TryGetValue(container.Tag, out var value))
                    {
                        var v = value switch
                        {
                            IDictionary<string, string> d
                                => d.TryGetValue("fill", out var fill) ? fill : "none",
                            JsonObject jd
                                => jd.TryGetPropertyValue("fill", out var jsonFill)
                                && jsonFill?.GetValueKind() == JsonValueKind.String
                                    ? jsonFill.ToString()
                                    : "none",
                            _ => value.ToString()
                        };
                        if ("none" != v && !string.IsNullOrWhiteSpace(v))
                        {
                            return new Color(v).TextColor();
                        }
                    }
                }
            }
            return "#000";
        }

        i = 0;
        foreach (var obj in c.Objects.Value)
        {
            try
            {
                if (obj.IsText && !obj.IsTagDefinition)
                {
                    var color = findTextColor(obj);
                    var startLink = "";
                    var endLink = "";
                    var text = new string(obj.Text);
                    var tag = obj.Tag;
                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        if (c.Options.TryGetValue("a2s:label", out var label))
                        {
                            text = label.ToString();
                        }

                        if (obj.Corners[0].X == 0)
                        {
                            if (c.Options.TryGetValue("a2s:delref", out var opt))
                            {
                                continue;
                            }
                        }

                        if (c.Options.TryGetValue("a2s:link", out var link))
                        {
                            startLink = "<a href=\"" + link + "\">";
                            endLink = "</a>";
                        }
                    }
                    var sp = scale(obj.Points[0], scaleX, scaleY);
                    // todo: escape text
                    result
                        .Append("    ")
                        .Append(startLink)
                        .Append("<text id=\"obj")
                        .Append(i)
                        .Append("\" x=\"")
                        .Append(sp[0].ToString(CultureInfo.InvariantCulture))
                        .Append("\" y=\"")
                        .Append(sp[1].ToString(CultureInfo.InvariantCulture))
                        .Append("\" fill=\"")
                        .Append(color)
                        .Append("\">")
                        .Append(text!.Replace("\"", "&#34;"))
                        .Append("</text>")
                        .Append(endLink)
                        .Append('\n');
                }
            }
            finally
            {
                i++;
            }
        }
        result.Append("  </g>\n");
        result.Append("</svg>\n");
        return result.ToString();
    }

    private float[] scale(Point p, int scaleX, int scaleY)
    {
        return [scaleX * (.5f + p.X), scaleY * (.5f + p.Y)];
    }

    private StringBuilder Flatten(Point[] points, int scaleX, int scaleY)
    {
        var res = new StringBuilder();
        var sp = scale(points[0], scaleX, scaleY);
        var pp = sp;

        int i = 0;
        foreach (var cp in points)
        {
            try
            {
                var p = scale(cp, scaleX, scaleY);
                if (i == 0)
                {
                    if (cp.Hint == Point.PointHint.ROUNDED_CORNER)
                    {
                        res.Append("M ")
                            .Append(p[0].ToString(CultureInfo.InvariantCulture))
                            .Append(' ')
                            .Append((p[1] + 10).ToString(CultureInfo.InvariantCulture))
                            .Append(" Q ")
                            .Append(p[0].ToString(CultureInfo.InvariantCulture))
                            .Append(' ')
                            .Append(p[1].ToString(CultureInfo.InvariantCulture))
                            .Append(' ')
                            .Append((p[0] + 10).ToString(CultureInfo.InvariantCulture))
                            .Append(' ')
                            .Append(p[1].ToString(CultureInfo.InvariantCulture));
                        continue;
                    }
                    res.Append("M ")
                        .Append(p[0].ToString(CultureInfo.InvariantCulture))
                        .Append(' ')
                        .Append(p[1].ToString(CultureInfo.InvariantCulture));
                    continue;
                }

                if (cp.Hint == Point.PointHint.ROUNDED_CORNER)
                {
                    float cx = p[0];
                    float cy = p[1];
                    float sx = 0;
                    float sy = 0;
                    float ex = 0;
                    float ey = 0;
                    var np = i == points.Length - 1 ? sp : scale(points[i + 1], scaleX, scaleY);
                    if (pp[0] == p[0])
                    {
                        sx = p[0];
                        if (pp[1] < p[1])
                        {
                            sy = p[1] - 10;
                        }
                        else
                        {
                            sy = p[1] + 10;
                        }

                        ey = p[1];
                        if (np[0] < p[0])
                        {
                            ex = p[0] - 10;
                        }
                        else
                        {
                            ex = p[0] + 10;
                        }
                    }
                    else if (pp[1] == p[1])
                    {
                        sy = p[1];
                        if (pp[0] < p[0])
                        {
                            sx = p[0] - 10;
                        }
                        else
                        {
                            sx = p[0] + 10;
                        }
                        ex = p[0];
                        if (np[1] <= p[1])
                        {
                            ey = p[1] - 10;
                        }
                        else
                        {
                            ey = p[1] + 10;
                        }
                    }

                    res.Append(" L ")
                        .Append(sx.ToString(CultureInfo.InvariantCulture))
                        .Append(' ')
                        .Append(sy.ToString(CultureInfo.InvariantCulture))
                        .Append(" Q ")
                        .Append(cx.ToString(CultureInfo.InvariantCulture))
                        .Append(' ')
                        .Append(cy.ToString(CultureInfo.InvariantCulture))
                        .Append(' ')
                        .Append(ex.ToString(CultureInfo.InvariantCulture))
                        .Append(' ')
                        .Append(ey.ToString(CultureInfo.InvariantCulture))
                        .Append(' ');
                }
                else
                {
                    res.Append(" L ")
                        .Append(p[0].ToString(CultureInfo.InvariantCulture))
                        .Append(' ')
                        .Append(p[1].ToString(CultureInfo.InvariantCulture))
                        .Append(' ');
                }

                pp = p;
            }
            finally
            {
                i++;
            }
        }

        return res;
    }
}
