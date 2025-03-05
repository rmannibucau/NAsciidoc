using System.Collections.Immutable;
using System.Text;
using NAsciidoc.Ascii2SVG;
using NAsciidoc.Model;
using NAsciidoc.Parser;

namespace NAsciidoc.Renderer;

public class AsciidoctorLikeHtmlRenderer : Visitor<string>
{
    protected readonly StringBuilder builder = new StringBuilder();
    protected readonly Configuration configuration;
    protected readonly bool dataUri;
    protected readonly DataResolver? resolver;
    protected readonly State state = new State(); // this is why we are not thread safe

    public AsciidoctorLikeHtmlRenderer()
        : this(new Configuration()) { }

    public AsciidoctorLikeHtmlRenderer(Configuration configuration)
    {
        this.configuration = configuration;

        var dataUriValue = configuration.Attributes.TryGetValue("data-uri", out var du)
            ? du
            : "false";
        dataUri = string.IsNullOrWhiteSpace(dataUriValue) || bool.Parse(dataUriValue);
        resolver = dataUri
            ? (
                configuration.Resolver is null
                    ? new DataResolver
                    {
                        Base = AssetsDir(configuration, "imagesdir"),
                        EnableRemoting =
                            configuration.Attributes.TryGetValue("data-enable-remoting", out var er)
                            && bool.Parse(er),
                    }
                    : configuration.Resolver
            )
            : null;
    }

    private string AssetsDir(Configuration configuration, string attribute)
    {
        var assetsBase = configuration.AssetsBase;
        if (
            configuration.Attributes.TryGetValue(attribute, out var attrValue)
            && attrValue is not null
            && !string.IsNullOrWhiteSpace(attrValue)
        )
        {
            return Path.Combine(assetsBase ?? "", attrValue);
        }
        return assetsBase ?? "";
    }

    protected string Escape(string name)
    {
        return HtmlEscaping.Instance.Apply(name);
    }

    protected string? Attr(
        string key,
        string defaultKey,
        string? defaultValue,
        IDictionary<string, string> mainMap
    )
    {
        return mainMap.TryGetValue(key, out var v) ? v
            : configuration.Attributes.TryGetValue(defaultKey, out var o) ? o
            : defaultValue;
    }

    protected string? Attr(string key, IDictionary<string, string> defaultMap)
    {
        return Attr(key, key, null, defaultMap);
    }

    public override void VisitBody(Body body)
    {
        if ("none" != Attr("toc", "toc", "none", state.Document.Header.Attributes))
        {
            VisitToc(body);
        }

        state.StackChain(body.Children, () => base.VisitBody(body));
    }

    public override void VisitConditionalBlock(ConditionalBlock element)
    {
        state.StackChain(element.Children, () => base.VisitConditionalBlock(element));
    }

    public override void VisitElement(IElement element)
    {
        state.lastElement.Add(element);
        if (
            state is { SawPreamble: false, lastElement.Count: >= 2 }
            && element.Type() != IElement.ElementType.Text
            && element.Type() != IElement.ElementType.Paragraph
        )
        {
            state.SawPreamble = true;
        }
        try
        {
            base.VisitElement(element);
        }
        finally
        {
            state.lastElement.RemoveAt(state.lastElement.Count - 1);
        }
    }

    public override void Visit(Document document)
    {
        state.Document = document;
        bool contentOnly =
            configuration.Attributes.TryGetValue("noheader", out var noheader)
            && bool.Parse(noheader);
        if (!contentOnly)
        {
            var attributes = document.Header.Attributes;

            builder.Append("<!DOCTYPE html>\n");
            builder.Append("<html");
            if (Attr("nolang", attributes) is null)
            {
                var lang = Attr("lang", attributes);
                builder.Append(" lang=\"").Append(lang ?? "en").Append('"');
            }
            builder.Append(">\n");
            builder.Append("<head>\n");

            var encoding = Attr("encoding", attributes);
            builder.Append(" <meta charset=\"").Append(encoding ?? "UTF-8").Append("\">\n");

            builder.Append(" <meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">\n");

            var appName = Attr("app-name", attributes);
            if (appName != null)
            {
                builder
                    .Append(" <meta name=\"application-name\" content=\"")
                    .Append(appName)
                    .Append("\">\n");
            }
            var description = Attr("description", attributes);
            if (description != null)
            {
                builder
                    .Append(" <meta name=\"description\" content=\"")
                    .Append(description)
                    .Append("\">\n");
            }
            var keywords = Attr("keywords", attributes);
            if (keywords != null)
            {
                builder
                    .Append(" <meta name=\"keywords\" content=\"")
                    .Append(keywords)
                    .Append("\">\n");
            }
            var author = Attr("author", attributes);
            if (author != null)
            {
                builder.Append(" <meta name=\"author\" content=\"").Append(author).Append("\">\n");
            }
            var copyright = Attr("copyright", attributes);
            if (copyright != null)
            {
                builder
                    .Append(" <meta name=\"copyright\" content=\"")
                    .Append(copyright)
                    .Append("\">\n");
            }

            if (Attr("asciidoctor-css", "asciidoctor-css", null, attributes) != null)
            {
                builder.Append(
                    " <link rel=\"stylesheet\" href=\"https://cdnjs.cloudflare.com/ajax/libs/asciidoctor.js/1.5.9/css/asciidoctor.min.css\" integrity=\"sha512-lb4ZuGfCVoGO2zu/TMakNlBgRA6mPXZ0RamTYgluFxULAwOoNnBIZaNjsdfhnlKlIbENaQbEAYEWxtzjkB8wsQ==\" crossorigin=\"anonymous\" referrerpolicy=\"no-referrer\" />\n"
                );
            }
            builder.Append(Attr("custom-css", "custom-css", "", attributes));

            // todo: favicon, highlighter, etc...
            BeforeHeadEnd();
            builder.Append("</head>\n");

            builder.Append("<body");
            var bodyClasses = Attr("body-classes", "body-classes", null, attributes);
            if (bodyClasses is not null)
            {
                builder.Append(" class=\"").Append(bodyClasses).Append('"');
            }
            builder.Append(">\n");
            builder.Append(Attr("header-html", "header-html", "", document.Header.Attributes));
            AfterBodyStart();

            if (!configuration.SkipGlobalContentWrapper)
            {
                builder.Append(" <div id=\"content\">\n");
            }
        }
        base.Visit(document);
        if (!contentOnly)
        {
            if (!configuration.SkipGlobalContentWrapper)
            {
                builder.Append(" </div>\n");
            }

            if (state.HasStem && Attr("skip-stem-js", document.Header.Attributes) is null)
            {
                builder.Append(
                    """
                    <script type="text/x-mathjax-config">
                    MathJax.Hub.Config({
                      messageStyle: "none",
                      tex2jax: { inlineMath: [["\\\\(", "\\\\)"]], displayMath: [["\\\\[", "\\\\]"]], ignoreClass: "nostem|nolatexmath" },
                      asciimath2jax: { delimiters: [["\\\\$", "\\\\$"]], ignoreClass: "nostem|noasciimath" },
                      TeX: { equationNumbers: { autoNumber: "none" } }
                    })
                    MathJax.Hub.Register.StartupHook("AsciiMath Jax Ready", function () {
                      MathJax.InputJax.AsciiMath.postfilterHooks.Add(function (data, node) {
                        if ((node = data.script.parentNode) && (node = node.parentNode) && node.classList.contains("stemblock")) {
                          data.math.root.display = "block"
                        }
                        return data
                      })
                    })
                    </script>
                    <script src="//cdnjs.cloudflare.com/ajax/libs/mathjax/2.7.9/MathJax.js?config=TeX-MML-AM_HTMLorMML"></script>
                    """.Replace("\r\n", "\n")
                );
            }

            foreach (
                var it in Attr("custom-js", "custom-js", "", document.Header.Attributes)!
                    .Split(',')
                    .Select(it => it.Trim())
                    .Where(it => !string.IsNullOrWhiteSpace(it))
                    .Select(i => " " + i + '\n')
            )
            {
                builder.Append(it);
            }
            BeforeBodyEnd();
            builder.Append("</body>\n");
            builder.Append("</html>\n");
        }
    }

    public override void VisitAdmonition(Admonition element)
    {
        // todo: here we need to impl icons to render it more elegantly
        var name = Enum.GetName(element.Level)!;
        builder
            .Append(" <div class=\"admonitionblock ")
            .Append(name.ToLowerInvariant())
            .Append("\">\n");
        builder
            .Append(
                """
                  <table>
                    <tbody>
                     <tr>
                      <td class="icon">

                """.Replace("\r\n", "\n")
            )
            .Append("     <div class=\"title\">")
            .Append(name.ToUpperInvariant())
            .Append("</div>\n")
            .Append("       </td>\n")
            .Append("      <td class=\"content\">\n");
        VisitElement(element.Content);
        builder
            .Append("    </td>\n")
            .Append("   </tr>\n")
            .Append("      </tbody>\n")
            .Append("  </table>\n")
            .Append(" </div>\n");
    }

    public override void VisitParagraph(Paragraph element)
    {
        state.StackChain(
            element.Children,
            () =>
            {
                HandlePreamble(
                    true,
                    element,
                    () =>
                    {
                        if (element.Options.ContainsKey("__internal-container__"))
                        {
                            base.VisitParagraph(element);
                            return;
                        }

                        if (!state.Nowrap)
                        {
                            builder.Append(" <div");
                            WriteCommonAttributes(
                                element.Options,
                                c => "paragraph" + (c != null ? ' ' + c : "")
                            );
                            builder.Append(">\n");
                        }

                        bool addP =
                            state is { Nowrap: false, SawPreamble: true }
                            && element.Children.All(e =>
                                e.Type() == IElement.ElementType.Text
                                || e.Type() == IElement.ElementType.Attribute
                                || e.Type() == IElement.ElementType.Link
                                || e.Type() == IElement.ElementType.Anchor
                                || e is Macro { Inline: true }
                                || e is Code { Inline: true }
                            );
                        if (addP)
                        {
                            builder.Append(" <p>");
                        }
                        base.VisitParagraph(element);
                        if (addP)
                        {
                            builder.Append("</p>\n");
                        }

                        if (!state.Nowrap)
                        {
                            builder.Append(" </div>\n");
                        }
                    }
                );
            }
        );
    }

    public override void VisitHeader(Header header)
    {
        if (
            !header.Attributes.ContainsKey("notitle")
            && !(
                configuration.Attributes.TryGetValue("noheader", out var noheader)
                && bool.Parse(noheader)
            )
            && !string.IsNullOrWhiteSpace(header.Title)
        )
        {
            builder.Append(" <h1>").Append(Escape(header.Title)).Append("</h1>\n");
        }

        var details = new StringBuilder();
        if (header.Author is not null)
        {
            int authorIdx = 1;
            var mails = header.Author.Name.Split(",");
            foreach (var name in header.Author.Name.Split(","))
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }
                details
                    .Append("<span class=\"author author-")
                    .Append(authorIdx)
                    .Append("\">")
                    .Append(Escape(name))
                    .Append("</span>\n");

                var mail = mails.Length > (authorIdx - 1) ? mails[authorIdx - 1] : null;
                if (mail != null)
                {
                    details
                        .Append("<span class=\"email email-")
                        .Append(authorIdx++)
                        .Append("\">")
                        .Append(Escape(mail))
                        .Append("</span>\n");
                }
                authorIdx++;
            }
        }
        if (header.Revision is not null)
        {
            if (!string.IsNullOrWhiteSpace(header.Revision.Number))
            {
                details
                    .Append("<span id=\"revnumber\">")
                    .Append(Escape(header.Revision.Number))
                    .Append("</span>\n");
            }
            if (!string.IsNullOrWhiteSpace(header.Revision.Date))
            {
                details
                    .Append("<span id=\"revdate\">")
                    .Append(Escape(header.Revision.Date))
                    .Append("</span>\n");
            }
            if (!string.IsNullOrWhiteSpace(header.Revision.RevMark))
            {
                details
                    .Append("<span id=\"revremark\">")
                    .Append(Escape(header.Revision.RevMark))
                    .Append("</span>\n");
            }
        }
        if (details.Length > 0)
        {
            builder
                .Append("  <div class=\"details\">\n")
                .Append("   ")
                .Append(details.ToString().Replace("\n", "\n   "))
                .Append("  </div>\n");
        }
    }

    public override void VisitSection(Section element)
    {
        state.StackChain(
            element.Children,
            () =>
            {
                var titleRenderer = new AsciidoctorLikeHtmlRenderer(configuration);
                titleRenderer.state.SawPreamble = true;
                titleRenderer.state.Nowrap = true;
                titleRenderer.VisitElement(element.Title);
                var title = titleRenderer.Result()!;

                builder.Append(" <").Append(configuration.SectionTag);
                WriteCommonAttributes(
                    element.Options,
                    c => "sect" + (element.Level - 1) + (c == null ? "" : (' ' + c))
                );
                if (!element.Options.ContainsKey("id"))
                {
                    builder.Append(" id=\"").Append(IdGenerator.ForTitle(title)).Append('"');
                }
                builder.Append(">\n");
                builder.Append("  <h").Append(element.Level).Append(">");
                builder.Append(title);
                builder.Append("</h").Append(element.Level).Append(">\n");
                if (!configuration.SkipSectionBody)
                {
                    builder.Append(" <div class=\"sectionbody\">\n");
                }
                base.VisitSection(element);
                if (!configuration.SkipSectionBody)
                {
                    builder.Append(" </div>\n");
                }
                builder.Append(" </").Append(configuration.SectionTag).Append(">\n");
            }
        );
    }

    public override void VisitLineBreak(LineBreak element)
    {
        builder.Append(" <br>\n");
    }

    public override void VisitPageBreak(PageBreak element)
    {
        builder.Append(" <div class=\"page-break\"></div>\n");
    }

    public override void VisitLink(Link element)
    {
        bool parentNeedsP =
            state.lastElement.Count > 1
            && IsList(state.lastElement[state.lastElement.Count - 2].Type());
        if (parentNeedsP)
        { // really to mimic asciidoctor
            builder.Append(" <p>");
        }

        bool code =
            element.Options.TryGetValue("role", out var role) && role.Contains("inline-code");
        if (code)
        {
            builder.Append("<code>");
        }

        builder.Append(" <a href=\"").Append(element.Url).Append("\"");
        WriteCommonAttributes(element.Options, null);

        element.Options.TryGetValue("window", out var window);
        if (window != null)
        {
            builder.Append(" target=\"").Append(window).Append("\"");
        }

        bool noopener = "_blank" == window || element.Options.TryGetValue("noopener", out var _);
        if (element.Options.TryGetValue("nofollow", out var nofollow))
        {
            builder.Append(" rel=\"nofollow");
            if (noopener)
            {
                builder.Append(" noopener");
            }
            builder.Append("\"");
        }
        else if (noopener)
        {
            builder.Append(" rel=\"noopener\"");
        }

        builder.Append('>');
        if (element.Options.ContainsKey("unsafeHtml"))
        {
            builder.Append(element.Label);
        }
        else
        {
            var label = element.Label;
            if (Attr("hide-uri-scheme", element.Options) is not null)
            {
                if (label.Contains("://"))
                {
                    label = label[(label.IndexOf("://") + "://".Length)..];
                }
                else if (label.Contains(':')) // mailto for ex
                {
                    label = label[(label.IndexOf(':') + 1)..];
                }
            }
            builder.Append(Escape(label));
        }
        builder.Append("</a>\n");

        if (code)
        {
            builder.Append("</code>");
        }
        if (parentNeedsP)
        {
            builder.Append("</p>");
        }
    }

    public override void VisitDescriptionList(DescriptionList element)
    {
        if (element.Children.Count == 0)
        {
            return;
        }
        state.StackChain(
            new List<IElement>(element.Children.Values),
            () =>
            {
                builder.Append(" <dl");
                WriteCommonAttributes(element.Options, null);
                builder.Append(">\n");
                foreach (var elt in element.Children)
                {
                    builder.Append("  <dt>");
                    VisitElement(elt.Key);
                    builder.Append("</dt>\n");
                    builder.Append("  <dd>\n");
                    VisitElement(elt.Value);
                    builder.Append("</dd>\n");
                }
                builder.Append(" </dl>\n");
            }
        );
    }

    public override void VisitUnOrderedList(UnOrderedList element)
    {
        if (element.Children.Count == 0)
        {
            return;
        }
        state.StackChain(
            element.Children,
            () =>
            {
                builder.Append(" <div");
                WriteCommonAttributes(element.Options, c => "ulist" + (c != null ? ' ' + c : ""));
                builder.Append(">\n");
                builder.Append(" <ul>\n");
                VisitListElements(element.Children);
                builder.Append(" </ul>\n");
                builder.Append(" </div>\n");
            }
        );
    }

    public override void VisitOrderedList(OrderedList element)
    {
        if (element.Children.Count == 0)
        {
            return;
        }
        state.StackChain(
            element.Children,
            () =>
            {
                builder.Append(" <ol>\n");
                VisitListElements(element.Children);
                builder.Append(" </ol>\n");
            }
        );
    }

    public override void VisitText(Text element)
    {
        var useWrappers =
            !element.Options.TryGetValue("nowrap", out var nowrap)
            || (nowrap.Length > 0 && !bool.Parse(nowrap));

        bool preambleSaw = state.SawPreamble;
        HandlePreamble(
            useWrappers,
            element,
            () =>
            {
                if (element.Options.TryGetValue("role", out var role) && role == "abstract") // we unwrapped the paragraph in some cases so add it back
                {
                    if (preambleSaw)
                    {
                        builder.Append(" <div class=\"sect1\">\n");
                    }
                    // not 100% sure of why asciidoctor does it sometimes (open blocks) but trying to behave the same to keep existing theme
                    VisitQuote(
                        new Quote(
                            [
                                new Text(
                                    element.Style,
                                    element.Value,
                                    ImmutableDictionary<string, string>.Empty
                                ),
                            ],
                            new Dictionary<string, string> { { "role", "quoteblock abstract" } }
                        )
                    );
                    if (preambleSaw)
                    {
                        builder.Append(" </div>\n");
                    }
                    return;
                }

                bool isParagraph =
                    state is { Nowrap: false, InCallOut: false }
                    && useWrappers
                    && (
                        state.lastElement.Count <= 1
                        || state.lastElement[^2].Type() == IElement.ElementType.Section
                    );
                if (isParagraph)
                {
                    // not writeCommonAttributes to not add twice the id for ex
                    element.Options.TryGetValue("role", out var customRole);
                    builder.Append(" <div class=\"paragraph");
                    if (!string.IsNullOrWhiteSpace(customRole))
                    {
                        builder.Append(' ').Append(customRole);
                    }
                    builder.Append("\">\n");
                }

                bool parentNeedsP =
                    state.lastElement.Count > 1
                    && (
                        IsList(state.lastElement[^2].Type())
                        || ( // if parent is a paragraph and it has homogeneous chidlren, ensure texts are wrapped in <p>
                            state.lastElement[^2] is Paragraph { Children.Count: > 1 } p
                            && p.Children.Any(it => it.Type() == IElement.ElementType.Paragraph)
                            && p.Children.Any(it =>
                                it.Type() == IElement.ElementType.Text
                                || it.Type() == IElement.ElementType.Link
                                || it is Code { Inline: true }
                                || (it is Macro m && m.Inline)
                            )
                        )
                    );
                bool wrap =
                    useWrappers
                    && (
                        parentNeedsP
                        || (
                            element.Style.Count != 1
                            && (isParagraph || state.InCallOut || element.Options.Count > 0)
                        )
                    );
                bool useP = parentNeedsP || isParagraph || !state.InCallOut;
                if (wrap)
                {
                    builder.Append(" <").Append(useP ? "p" : "span");
                    WriteCommonAttributes(element.Options, null);
                    builder.Append(">\n");
                }
                var styleTags = element
                    .Style.Select(s =>
                        s switch
                        {
                            Text.Styling.Bold => "b",
                            Text.Styling.Italic => "i",
                            Text.Styling.Emphasis => "em",
                            Text.Styling.Sub => "sub",
                            Text.Styling.Sup => "sup",
                            _ => "span",
                        }
                    )
                    .ToList();
                if (styleTags.Count > 0)
                {
                    builder.Append('<').Append(styleTags[0]);
                    if (!wrap)
                    {
                        WriteCommonAttributes(element.Options, null);
                    }
                    builder.Append('>');
                    if (styleTags.Count > 1)
                    {
                        builder.Append(
                            string.Join("", styleTags.Skip(1).Select(s => '<' + s + '>'))
                        );
                    }
                }
                builder.Append(Escape(element.Value));

                styleTags.Reverse();
                builder.Append(string.Join("", styleTags.Select(s => "</" + s + '>')));
                if (wrap)
                {
                    builder.Append("\n </").Append(useP ? "p" : "span").Append(">\n");
                }

                if (isParagraph)
                {
                    builder.Append(" </div>\n");
                }
            }
        );
    }

    public override void VisitQuote(Quote element)
    {
        builder.Append(" <div");
        WriteCommonAttributes(element.Options, null);
        builder.Append(">\n");

        WriteBlockTitle(element.Options);

        builder.Append("  <blockquote>\n");
        base.VisitQuote(element);
        builder.Append("  </blockquote>\n");

        var attribution = element.Options.TryGetValue("attribution", out var att)
            ? att
            : (element.Options.TryGetValue("citetitle", out var citetitle) ? citetitle : null);
        if (attribution is not null)
        {
            builder
                .Append("  <div class=\"attribution\">\n")
                .Append(Escape(attribution))
                .Append("\n  </div>\n");
        }

        builder.Append(" </div>");
    }

    public override void VisitCode(Code element)
    {
        if (element.Inline)
        {
            builder.Append("<code>").Append(Escape(element.Value.Trim())).Append("</code>");
            return;
        }

        var lang = element.Options.TryGetValue("lang", out var lang1)
            ? lang1
            : (element.Options.TryGetValue("language", out var lg) ? lg : null);

        builder.Append(" <div class=\"listingblock\">\n <div class=\"content\">\n");
        builder.Append(" <pre class=\"highlightjs highlight\">");
        builder.Append("<code");
        WriteCommonAttributes(
            element.Options,
            c => (lang != null ? "language-" + lang + (c != null ? ' ' + c : "") : c) + " hljs"
        );
        if (lang != null)
        {
            builder.Append(" data-lang=\"").Append(lang).Append("\"");
        }
        builder.Append(">");

        var html = Escape(element.Value).Trim();
        builder.Append(
            element.Options.TryGetValue("hightlight-callouts", out var hc) && hc == "false"
                ? html
                : HighlightCallOuts(element.CallOuts, html)
        );
        builder.Append("</code></pre>\n </div>\n </div>\n");

        if (element.CallOuts.Count > 0)
        {
            builder.Append(" <div class=\"colist arabic\">\n");
            builder.Append("  <ol>\n");
            foreach (var c in element.CallOuts)
            {
                bool nowrap = state.Nowrap;
                builder.Append("   <li>\n");
                state.InCallOut = true;
                state.Nowrap = true;
                if (c.Text is Paragraph p && p.Options.Count == 0)
                {
                    foreach (var it in p.Children)
                    {
                        VisitElement(it);
                    }
                }
                else
                {
                    VisitElement(c.Text);
                }
                state.InCallOut = false;
                state.Nowrap = nowrap;
                builder.Append("   </li>\n");
            }
            builder.Append("  </ol>\n");
            builder.Append(" </div>\n");
        }
    }

    public override void VisitTable(Table element)
    {
        var autowidth = element.Options.ContainsKey("autowidth");
        var stripes = Attr("stripes", "table-stripes", null, element.Options);
        var classes =
            "tableblock"
            + " frame-"
            + Attr("frame", "table-frame", "all", element.Options)
            + " grid-"
            + Attr("grid", "table-grid", "all", element.Options)
            + (stripes is not null ? $" stripes-{stripes}" : "")
            + (autowidth && !element.Options.ContainsKey("width") ? " fit-content" : "")
            + (
                element.Options.TryGetValue("tablepcwidth", out var w) && w != "100"
                    ? $" width=\"{w}\""
                    : " stretch"
            )
            + (element.Options.ContainsKey("float") ? " float" : "");

        builder.Append(" <table");
        WriteCommonAttributes(element.Options, c => classes + (c == null ? "" : (' ' + c)));
        builder.Append(">\n");

        element.Options.TryGetValue("title", out var title);
        if (title != null)
        {
            builder
                .Append("  <caption class=\"title\">")
                .Append(Escape(title))
                .Append("</caption>\n");
        }

        if (element.Elements.Count > 0)
        {
            var firstRow = element.Elements[0];
            var cols = element.Options.TryGetValue("cols", out var colsValue)
                ? colsValue.Split(',').SelectMany(ExtractNumbers).ToImmutableList()
                : [];

            builder.Append("  <colgroup>\n");
            if (autowidth)
            {
                for (int i = 0; i < firstRow.Count; i++)
                {
                    builder.Append("   <col>\n");
                }
            }
            else
            {
                int totalWeight = cols.Sum();
                int pc = (int)(100.0 / Math.Max(1, totalWeight));
                foreach (var c in cols)
                {
                    builder.Append("   <col width=\"").Append(c * pc).Append("%\">\n");
                }
            }
            builder.Append("  </colgroup>\n");

            var hasHeader =
                element.Options.TryGetValue("options", out var options)
                && options.Contains("header");
            if (hasHeader) // todo: handle headers+classes without assuming first row is headers - update parser - an options would be better pby?
            {
                builder.Append("  <thead>\n");
                builder.Append("   <tr>\n");
                foreach (var it in firstRow)
                {
                    builder.Append("    <th>\n");
                    VisitElement(it is Code c ? new Text([], c.Value, c.Options) : it);
                    builder.Append("    </th>\n");
                }
                builder.Append("   </tr>\n");
                builder.Append("  </thead>\n");
            }

            if (!hasHeader || element.Elements.Count > 1)
            {
                builder.Append("  <tbody>\n");
                foreach (var row in hasHeader ? element.Elements.Skip(1) : element.Elements)
                {
                    builder.Append("   <tr>\n");
                    foreach (var col in row)
                    {
                        builder.Append("    <td>\n"); // todo: tableblock halign-left valign-top and friends
                        VisitElement(col);
                        builder.Append("    </td>\n");
                    }
                    builder.Append("   </tr>\n");
                }
                builder.Append("  </tbody>\n");
            }
        }

        builder.Append(" </table>\n");
    }

    public override void VisitAnchor(Anchor element)
    {
        VisitLink(
            new Link(
                "#" + element.Value,
                element.Label == null || string.IsNullOrWhiteSpace(element.Label)
                    ? element.Value
                    : element.Label,
                ImmutableDictionary<string, string>.Empty
            )
        );
    }

    public override void VisitPassthroughBlock(PassthroughBlock element)
    {
        switch (element.Options.TryGetValue("", out var type) ? type : "")
        {
            case "stem":
                VisitStem(new Macro("stem", element.Value, element.Options, false));
                break;
            default:
                builder.Append('\n').Append(element.Value).Append('\n');
                break;
        }
    }

    public override void VisitOpenBlock(OpenBlock element)
    {
        state.StackChain(
            element.Children,
            () =>
            {
                bool skipDiv = false;
                if (element.Options.ContainsKey("abstract"))
                {
                    builder.Append(" <div");
                    WriteCommonAttributes(
                        element.Options,
                        c => "abstract quoteblock" + (c == null ? "" : (' ' + c))
                    );
                    builder.Append(">\n");
                }
                else if (element.Options.ContainsKey("partintro"))
                {
                    builder.Append(" <div");
                    WriteCommonAttributes(
                        element.Options,
                        c => "openblock " + (c == null ? "" : (' ' + c))
                    );
                    builder.Append(">\n");
                }
                else
                {
                    skipDiv = true;
                }
                WriteBlockTitle(element.Options);
                builder.Append("  <div");
                if (skipDiv)
                {
                    WriteCommonAttributes(
                        element.Options,
                        c => "content" + (c == null ? "" : (' ' + c))
                    );
                }
                builder.Append(">\n");
                base.VisitOpenBlock(element);
                builder.Append("  </div>\n");
                if (!skipDiv)
                {
                    builder.Append(" </div>\n");
                }
            }
        );
    }

    public override void VisitListing(Listing element)
    {
        switch (element.Options.TryGetValue("", out var type) ? type : "")
        {
            case "a2s":
                var svg = new Svg();
                var tabWidth = element.Options.TryGetValue("tabWidth", out var tw)
                    ? int.Parse(tw)
                    : 8;
                var scaleX = element.Options.TryGetValue("scaleX", out var sx) ? int.Parse(sx) : 9;
                var scaleY = element.Options.TryGetValue("scaleY", out var sy) ? int.Parse(sy) : 16;
                var blur = element.Options.TryGetValue("blur", out var b) && bool.Parse(b);
                var font = element.Options.TryGetValue("font", out var f)
                    ? f
                    : "Consolas,Monaco,Anonymous Pro,Anonymous,Bitstream Sans Mono,monospace";
                if (configuration.DataUriForAscii2Svg)
                {
                    VisitImage(
                        new Macro(
                            "image",
                            new DataUri(
                                () =>
                                    new MemoryStream(
                                        Encoding.UTF8.GetBytes(
                                            svg.Convert(
                                                element.Value,
                                                tabWidth,
                                                !blur,
                                                font,
                                                scaleX,
                                                scaleY
                                            )
                                        )
                                    ),
                                "image/svg+xml"
                            ).Base64,
                            element.Options,
                            false
                        )
                    );
                }
                else
                {
                    if (element.Options.TryGetValue("role", out var clazz))
                    {
                        builder
                            .Append(" <div class=\"")
                            .Append(clazz.Replace('.', ' ').Trim())
                            .Append("\">\n");
                    }
                    VisitPassthroughBlock(
                        new PassthroughBlock(
                            svg.Convert(element.Value, tabWidth, !blur, font, scaleX, scaleY),
                            ImmutableDictionary<string, string>.Empty
                        )
                    );
                    if (clazz != null)
                    {
                        builder.Append(" </div>\n");
                    }
                }
                break;
            default:
                VisitCode(new Code(element.Value, [], element.Options, false));
                break;
        }
    }

    protected void VisitXref(Macro element)
    {
        var target = element.Label;
        int anchor = target.LastIndexOf('#');
        if (anchor > 0)
        {
            var page = target[..anchor];
            if (page.EndsWith(".adoc"))
            {
                target = page[..(page.Length - ".adoc".Length)] + ".html" + target[anchor..];
            }
        }
        else if (target.EndsWith(".adoc"))
        {
            target = target[..(target.Length - ".adoc".Length)] + ".html";
        }
        element.Options.TryGetValue("", out var label);
        builder
            .Append(" <a href=\"")
            .Append(target)
            .Append("\">")
            .Append(label ?? element.Label)
            .Append("</a>\n");
    }

    protected void VisitImage(Macro element)
    {
        if (
            dataUri
            && !element.Label.StartsWith("data:")
            && !element.Options.ContainsKey("skip-data-uri")
        )
        {
            VisitImage(
                new Macro(
                    element.Name,
                    resolver!.Apply(element.Label).Base64,
                    !element.Options.ContainsKey("alt")
                        ? element
                            .Options.Concat(
                                new Dictionary<string, string> { { "alt", element.Label } }
                            )
                            .ToImmutableDictionary()
                        : element.Options,
                    element.Inline
                )
            );
            return;
        }

        builder
            .Append(" <img src=\"")
            .Append(element.Label)
            .Append("\" alt=\"")
            .Append(
                element.Options.TryGetValue("alt", out var alt)
                    ? alt
                    : (element.Options.TryGetValue("", out var f) ? f : element.Label)
            )
            .Append('"');
        if (element.Options.TryGetValue("width", out var w))
        {
            builder.Append(" width=\"").Append(w).Append('"');
        }
        if (element.Options.TryGetValue("height", out var h))
        {
            builder.Append(" height=\"").Append(h).Append('"');
        }
        WriteCommonAttributes(element.Options, null);
        builder.Append(">\n");
    }

    protected void VisitAudio(Macro element)
    {
        builder.Append(" <div");
        WriteCommonAttributes(element.Options, c => "audioblock" + (c == null ? "" : (' ' + c)));
        builder.Append(">\n");
        WriteBlockTitle(element.Options);
        builder
            .Append("  <audio src=\"")
            .Append(element.Label)
            .Append('"')
            .Append(element.Options.ContainsKey("autoplay") ? " autoplay" : "")
            .Append(element.Options.ContainsKey("nocontrols") ? " nocontrols" : "")
            .Append(element.Options.ContainsKey("loop") ? " loop" : "")
            .Append(">\n");
        builder.Append("  Your browser does not support the audio tag.\n");
        builder.Append("  </audio>\n");
        builder.Append(" </div>\n");
    }

    protected void VisitVideo(Macro element)
    {
        builder.Append(" <div");
        WriteCommonAttributes(element.Options, c => "videoblock" + (c == null ? "" : (' ' + c)));
        builder.Append(">\n");
        WriteBlockTitle(element.Options);
        builder
            .Append("  <video src=\"")
            .Append(element.Label)
            .Append("\"")
            .Append(element.Options.ContainsKey("autoplay") ? " autoplay" : "")
            .Append(element.Options.ContainsKey("nocontrols") ? " nocontrols" : "")
            .Append(element.Options.ContainsKey("loop") ? " loop" : "")
            .Append(">\n");
        builder.Append("  Your browser does not support the video tag.\n");
        builder.Append("  </video>\n");
        builder.Append(" </div>\n");
    }

    protected void VisitPassthroughInline(Macro element)
    {
        builder.Append(element.Label);
    }

    protected void VisitBtn(Macro element)
    {
        builder.Append(" <b class=\"button\">").Append(Escape(element.Label)).Append("</b>\n");
    }

    protected void VisitKbd(Macro element)
    {
        builder.Append(" <kbd>").Append(Escape(element.Label)).Append("</kbd>\n");
    }

    protected void VisitIcon(Macro element)
    {
        if (!element.Inline)
        {
            builder.Append(' ');
        }
        var hasRole = element.Options.ContainsKey("role");
        if (hasRole)
        {
            builder.Append("<span");
            WriteCommonAttributes(element.Options, null);
            builder.Append('>');
        }
        builder.Append("<span class=\"icon\"><i class=\"");
        builder
            .Append(element.Label.StartsWith("fa") && !element.Label.Contains(' ') ? "fa " : "")
            .Append(element.Label)
            .Append(
                element.Options.TryGetValue("", out var t) ? $" fa-{t}"
                : element.Options.TryGetValue("size", out var size) ? $" fa-{size}"
                : ""
            );
        builder.Append("\"></i></span>");
        if (hasRole)
        {
            builder.Append("</span>");
        }
        if (!element.Inline)
        {
            builder.Append('\n');
        }
    }

    public override void VisitMacro(Macro element)
    {
        if (!element.Inline)
        {
            builder
                .Append(" <div class=\"")
                .Append(element.Name)
                .Append("block\">\n <div class=\"content\">\n");
        }
        switch (element.Name)
        {
            case "kbd":
                VisitKbd(element);
                break;
            case "btn":
                VisitBtn(element);
                break;
            case "stem":
                VisitStem(element);
                break;
            case "pass":
                VisitPassthroughInline(element);
                break;
            case "icon":
                VisitIcon(element);
                break;
            case "image":
                VisitImage(element);
                break;
            case "audio":
                VisitAudio(element);
                break;
            case "video":
                VisitVideo(element);
                break;
            case "xref":
                VisitXref(element);
                break;
            case "link":
                var label = element.Options.TryGetValue("", out var l) ? l : element.Label;
                if (label.Contains("image:")) // FIXME: ...we don't want options to be parsed but this looks required
                {
                    try
                    {
                        var parser = new Parser.Parser(
                            configuration.Attributes ?? ImmutableDictionary<string, string>.Empty
                        );
                        var body = parser.ParseBody(
                            new Reader([label]),
                            new LocalContentResolver(configuration.AssetsBase ?? "")
                        );
                        if (
                            body.Children.Count == 1
                            && body.Children[0] is Text t
                            && t.Style.Count == 0
                        )
                        {
                            VisitLink(new Link(element.Label, t.Value, element.Options));
                        }
                        else
                        {
                            var nested = new AsciidoctorLikeHtmlRenderer(configuration);
                            nested.state.SawPreamble = true;
                            foreach (
                                var it in (
                                    body.Children.Count == 1 && body.Children[0] is Paragraph p
                                        ? p.Children
                                        : body.Children
                                ).Select(e =>
                                    e is Text t
                                        ? new Text(
                                            t.Style,
                                            t.Value,
                                            t.Options.Concat(
                                                    new Dictionary<string, string>
                                                    {
                                                        { "nowrap", "true" },
                                                    }
                                                )
                                                .ToDictionary()
                                        )
                                        : e
                                )
                            )
                            {
                                nested.VisitElement(it);
                            }

                            var html = nested.Result();
                            VisitLink(
                                new Link(
                                    element.Label,
                                    html,
                                    element
                                        .Options.Concat(
                                            new Dictionary<string, string>
                                            {
                                                { "unsafeHtml", "true" },
                                            }
                                        )
                                        .ToDictionary()
                                )
                            );
                        }
                    }
                    catch (Exception)
                    {
                        VisitLink(new Link(element.Label, label, element.Options));
                    }
                }
                else
                {
                    VisitLink(new Link(element.Label, label, element.Options));
                }
                break;
            // todo: menu, doublefootnote, footnote
            // for future extension point
            default:
                OnMissingMacro(element);
                break;
        }
        if (!element.Inline)
        {
            builder.Append(" </div>\n </div>\n");
        }
    }

    public override string Result()
    {
        Release();
        return builder.ToString();
    }

    public ConditionalBlock.IContext Context()
    {
        return new ConditionalBlock.DictionaryContext(configuration.Attributes);
    }

    protected void OnMissingMacro(Macro element)
    {
        base.VisitMacro(element);
    }

    private void Release()
    {
        state.Dispose();
        resolver?.Dispose();
    }

    protected void VisitStem(Macro element)
    {
        state.HasStem = true;
        if (!element.Inline)
        {
            builder.Append(" <div");
            WriteCommonAttributes(element.Options, c => "stemblock" + (c == null ? "" : (' ' + c)));
            builder.Append(">\n");
            WriteBlockTitle(element.Options);
            builder.Append("  <div class=\"content\">\n");
        }

        bool latex =
            "latexmath"
            == Attr(
                "stem",
                state.Document == null
                    ? ImmutableDictionary<string, string>.Empty
                    : state.Document.Header.Attributes
            );
        if (latex)
        {
            if (element.Inline)
            {
                builder.Append(" \\(").Append(element.Label).Append("\\) ");
            }
            else
            {
                builder.Append(" \\[").Append(element.Label).Append("\\] ");
            }
        }
        else
        {
            builder.Append(" \\$").Append(element.Label).Append("\\$ ");
        }

        if (!element.Inline)
        {
            builder.Append("  </div>\n");
            builder.Append(" </div>\n");
        }
    }

    private int[] ExtractNumbers(string col)
    {
        var i = 0;
        while (col.Length > i && char.IsDigit(col[i]))
        {
            i++;
        }
        try
        {
            if (i == 0)
            {
                return [1];
            }

            var value = int.Parse(col[..i]);
            return col.Length > i && col[i] == '*'
                ? [.. Enumerable.Range(0, value).Select(_ => 1)]
                : [value];
        }
        catch (FormatException)
        {
            return [1];
        }
    }

    protected string HighlightCallOuts(IList<CallOut> callOuts, string value)
    {
        if (callOuts.Count == 0)
        {
            return value;
        }
        var res = value;
        for (int i = 1; i <= callOuts.Count; i++)
        {
            res = res.Replace(" (" + i + ")", " <b class=\"conum\">(" + i + ")</b>");
        }
        return res;
    }

    protected void WriteBlockTitle(IDictionary<string, string> options)
    {
        if (options.TryGetValue("title", out var title))
        {
            builder.Append("  <div class=\"title\">").Append(Escape(title)).Append("</div>\n");
        }
    }

    private void VisitListElements(IList<IElement> element)
    {
        foreach (var elt in element)
        {
            builder.Append("  <li>\n");
            VisitElement(elt);
            builder.Append("  </li>\n");
        }
    }

    protected bool IsList(IElement.ElementType type)
    {
        return type == IElement.ElementType.UnorderedList
            || type == IElement.ElementType.OrderedList;
    }

    protected void AfterBodyStart()
    {
        // no-op
    }

    protected void BeforeBodyEnd()
    {
        // no-op
    }

    protected void BeforeHeadEnd()
    {
        // no-op
    }

    protected void VisitToc(Body body)
    {
        int toclevels = int.Parse(
            Attr("toclevels", "toclevels", "2", state.Document.Header.Attributes)!
        );
        if (toclevels < 1)
        {
            return;
        }

        builder
            .Append(" <div id=\"toc\" class=\"")
            .Append(Attr("toc-class", "toc-class", "toc", state.Document.Header.Attributes))
            .Append("\">\n");
        if (!string.IsNullOrWhiteSpace(state.Document.Header.Title))
        {
            builder
                .Append("  <div id=\"toctitle\">")
                .Append(state.Document.Header.Title)
                .Append("</div>\n");
        }

        var tocVisitor = new TocVisitor(toclevels, 2);
        tocVisitor.VisitBody(body);
        builder.Append(tocVisitor.Result());
        builder.Append(" </div>\n");
    }

    protected void WriteCommonAttributes(
        IDictionary<string, string> options,
        Func<string?, string>? classProcessor
    )
    {
        options.TryGetValue("role", out var classes);
        if (classes is not null)
        {
            classes = classes.Replace('.', ' ');
        }
        if (classProcessor is not null)
        {
            classes = classProcessor(classes);
        }
        if (!string.IsNullOrWhiteSpace(classes))
        {
            builder.Append(" class=\"").Append(classes).Append('"');
        }

        options.TryGetValue("id", out var id);
        if (id is not null && !string.IsNullOrWhiteSpace(id))
        {
            builder.Append(" id=\"").Append(id).Append('"');
        }

        if (configuration.SupportDataAttributes)
        {
            var data = options
                .Where(e => e.Key.StartsWith("data-") && e.Value is not null)
                .Select(e => e.Key + "=\"" + e.Value + "\"")
                .ToList();
            if (data.Count > 0)
            {
                builder.Append(' ').Append(string.Join(' ', data));
            }
        }
    }

    protected void HandlePreamble(bool enableWrappers, IElement next, Action child)
    {
        if (state.SawPreamble)
        {
            child();
            return;
        }

        bool isPreamble = enableWrappers && state.lastElement.Count == 1;
        if (!isPreamble)
        {
            child();
            return;
        }

        bool hasSingleSection =
            state.Document is not null
            && !state.Document.Body.Children.Any(it => it is Section s && s.Level > 0);
        if (hasSingleSection)
        {
            state.SawPreamble = true; // there will be no preamble
            child();
            return;
        }

        state.SawPreamble = true;
        builder.Append(" <div id=\"preamble\">\n <div class=\"sectionbody\">\n");

        bool needsP = next == null || next.Type() != IElement.ElementType.Paragraph;
        if (needsP)
        {
            builder.Append(" <p>");
        }
        child();
        if (needsP)
        {
            builder.Append("</p>\n");
        }
        builder.Append(" </div>\n </div>\n");
    }

    protected class State : IDisposable
    {
        protected static readonly Document EmptyDoc = new(
            new Header("", null, null, ImmutableDictionary<string, string>.Empty),
            new Body(ImmutableList<IElement>.Empty)
        );

        public Document Document { get; set; } = EmptyDoc;
        public IList<IElement>? CurrentChain { get; set; } = null;
        public bool HasStem { get; set; } = false;
        public bool Nowrap { get; set; } = false;
        public bool SawPreamble { get; set; } = false;
        public bool InCallOut { get; set; } = false;
        public readonly List<IElement> lastElement = new(4);

        public void StackChain(IList<IElement> next, Action run)
        {
            var current = CurrentChain;
            try
            {
                CurrentChain = next;
                run();
            }
            finally
            {
                CurrentChain = current;
            }
        }

        public void Dispose()
        {
            Document = EmptyDoc;
            CurrentChain = null;
            SawPreamble = false;
            InCallOut = false;
            lastElement.Clear();
        }
    }

    public class Configuration
    {
        public string SectionTag { get; init; } = "div";
        public bool SkipSectionBody { get; init; } = false;
        public bool SkipGlobalContentWrapper { get; init; } = false;
        public bool SupportDataAttributes { get; init; } = true;
        public DataResolver? Resolver { get; init; } = null;
        public string? AssetsBase { get; init; } = null;
        public IDictionary<string, string> Attributes { get; init; } =
            ImmutableDictionary<string, string>.Empty;

        public bool DataUriForAscii2Svg { get; init; } = true;
    }
}
