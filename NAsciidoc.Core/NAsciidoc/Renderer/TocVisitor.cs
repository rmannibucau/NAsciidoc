using System.Text;
using NAsciidoc.Model;

namespace NAsciidoc.Renderer;

public class TocVisitor(int MaxLevel, int CurrentLevel) : Visitor<StringBuilder>
{
    private readonly IList<Section> sections = [];

    public override void VisitSection(Section element)
    {
        if (element.Level == CurrentLevel)
        {
            sections.Add(element);
        }
    }

    public override StringBuilder Result()
    {
        var builder = new StringBuilder();
        if (sections.Count == 0)
        {
            return builder;
        }

        builder.Append(" <ul class=\"sectlevel").Append(CurrentLevel).Append("\">\n");
        if (CurrentLevel == MaxLevel)
        {
            builder.Append(
                string.Join(
                    '\n',
                    sections.Select(it =>
                    {
                        var title = Title(it.Title);
                        return " <li><a href=\"#" + Id(it, title) + "\">" + title + "</a></li>";
                    })
                ) + '\n'
            );
        }
        else
        {
            builder.Append(
                string.Join(
                    '\n',
                    sections.Select(it =>
                    {
                        var tocVisitor = new TocVisitor(MaxLevel, CurrentLevel + 1);
                        tocVisitor.VisitBody(new Body(it.Children));
                        string children = tocVisitor.Result().ToString() ?? "";
                        string title = Title(it.Title);

                        return " <li><a href=\"#"
                            + Id(it, title)
                            + "\">"
                            + title
                            + "</a>\n"
                            + children
                            + " </li>";
                    })
                ) + '\n'
            );
        }
        builder.Append(" </ul>\n");
        return builder;
    }

    private string Id(Section section, string title)
    {
        return section.Options.TryGetValue("id", out var id) ? id : IdGenerator.ForTitle(title);
    }

    private string Title(IElement title)
    {
        var titleRenderer = new AsciidoctorLikeHtmlRenderer(
            new AsciidoctorLikeHtmlRenderer.Configuration()
        );

        var elt =
            title is Text t && t.Options.Count == 0 && t.Style.Count == 0
                ? new Text(t.Style, t.Value, new Dictionary<string, string> { { "nowrap", "" } })
                : title;
        titleRenderer.VisitElement(elt);

        return titleRenderer.Result() ?? "";
    }
}
