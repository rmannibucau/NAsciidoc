using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using NAsciidoc.Model;

namespace NAsciidoc.Parser
{
    public partial class Parser(IDictionary<string, string> globalAttributes)
    {
        private static readonly Author NoAuthor = new("", "");
        private static readonly Revision NoRevision = new("", "", "");
        private static readonly Header NoHeader = new(
            "",
            NoAuthor,
            NoRevision,
            ImmutableDictionary<string, string>.Empty
        );

        private static readonly IList<string> LinkPrefixes =
        [
            "http://",
            "https://",
            "ftp://",
            "ftps://",
            "irc://",
            "file://",
            "mailto:",
        ];

        [GeneratedRegex("^:(?<name>[^\\n\\t:]+):( +(?<value>.+))? *$")]
        private static partial Regex AttributeDefinitionRegex();

        [GeneratedRegex("^(?<wildcard>\\*+) .+")]
        private static partial Regex UnOrderedListPrefix();

        [GeneratedRegex("^[0-9]*(?<dots>\\.+) .+")]
        private static partial Regex OrderedListPrefix();

        [GeneratedRegex("^(?<name>(?!::).*)(?<marker>::+)(?<content>.*)")]
        private static partial Regex DescriptionListPrefix();

        [GeneratedRegex("^<(?<number>[\\d+.]+)> (?<description>.+)$")]
        private static partial Regex CallOut();

        [GeneratedRegex("<(?<number>\\d+)>")]
        private static partial Regex CallOutRef();

        public Parser()
            : this(ImmutableDictionary<string, string>.Empty) { }

        public Document Parse(string content, ParserContext? context = null)
        {
            return Parse(content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'), context);
        }

        public Document Parse(IList<string> strings, ParserContext? context = null)
        {
            var reader = new Reader(strings);
            return Parse(reader, context);
        }

        public Document Parse(Reader reader, ParserContext? context = null)
        {
            var header = ParseHeader(reader, context?.Resolver ?? null);
            return new Document(
                header,
                ParseBody(reader, context?.Resolver ?? null, header.Attributes)
            );
        }

        public Body ParseBody(
            Reader reader,
            IContentResolver? resolver = null,
            IDictionary<string, string>? initialAttributes = null
        )
        {
            return new Body(
                DoParse(
                    reader,
                    line => true,
                    resolver,
                    new Dictionary<string, string>(
                        initialAttributes ?? ImmutableDictionary<string, string>.Empty
                    ),
                    true,
                    true
                )
            );
        }

        private IDictionary<string, string> Merge(
            IDictionary<string, string>? options,
            IDictionary<string, string>? next
        )
        {
            if (options is null && next is null)
            {
                return new Dictionary<string, string>();
            }
            if (options is null)
            {
                return new Dictionary<string, string>(next!);
            }
            if (next is null)
            {
                return new Dictionary<string, string>(options);
            }

            IList<IDictionary<string, string>> toMerge = [options, next];
            return toMerge
                .Where(it => it is not null)
                .SelectMany(it => it)
                .ToDictionary(i => i.Key, i => i.Value);
        }

        private void FlushOption(
            string defaultKey,
            StringBuilder key,
            StringBuilder value,
            Dictionary<string, string> collector
        )
        {
            var keyValue = key.ToString();
            if (value.Length == 0)
            {
                if (keyValue.StartsWith('.'))
                {
                    collector.TryAdd("role", keyValue[1..]);
                }
                else if (keyValue.StartsWith('#'))
                {
                    collector.TryAdd("id", keyValue[1..]);
                }
                else if (keyValue.StartsWith('%'))
                {
                    if (collector.TryGetValue("options", out var opts))
                    {
                        collector["options"] = $"{opts} {keyValue[1..]}";
                    }
                    else
                    {
                        collector.Add("options", keyValue[1..]);
                    }
                }
                else
                {
                    collector.TryAdd(defaultKey, keyValue);
                }
            }
            else
            {
                collector.TryAdd(keyValue, value.ToString());
            }
        }

        private IDictionary<string, string> DoParseOptions(
            string options,
            string defaultKey,
            bool nestedOptsSupport,
            params string[] orderedKeyFallbacks
        )
        {
            var map = new Dictionary<string, string>();
            var key = new StringBuilder();
            var value = new StringBuilder();
            bool quoted = false;
            bool inKey = true;
            for (int i = 0; i <= options.Length; i++)
            {
                char c =
                    i == options.Length
                        ? ',' /* force flush */
                        : options[i];
                if (c == '"')
                {
                    quoted = !quoted;
                }
                else if (quoted)
                {
                    (inKey ? key : value).Append(c);
                }
                else if (c == '=')
                {
                    inKey = false;
                }
                else if (c == ',')
                {
                    if (key.Length > 0)
                    {
                        if (
                            // if we have a "value" but no key and a fallback key, force it
                            value.Length == 0
                            && map.Count < orderedKeyFallbacks.Length
                            // ignore the "known" shortcuts
                            && key[0] != '.'
                            && key[0] != '%'
                            && key[0] != '#'
                        )
                        {
                            value.Append(key);
                            key.Length = 0;
                            key.Append(orderedKeyFallbacks[map.Count]);
                        }
                        FlushOption(defaultKey, key, value, map);
                    }
                    key.Length = 0;
                    value.Length = 0;
                    inKey = true;
                }
                else
                {
                    (inKey ? key : value).Append(c);
                }
            }
            if (nestedOptsSupport)
            {
                map.Remove("opts", out var nestedOpts);
                if (nestedOpts is null && map.Remove("options", out var nestedOpts2))
                {
                    nestedOpts = nestedOpts2;
                }
                if (nestedOpts != null)
                {
                    foreach (var opt in DoParseOptions(nestedOpts, "opts", false))
                    {
                        map.Add(opt.Key, opt.Value);
                    }
                }
            }

            // opts will alias options since both are equivalent
            if (map.TryGetValue("opts", out string? opts) && !map.ContainsKey("options"))
            {
                map.Add("options", opts);
            }
            if (map.TryGetValue("options", out string? opts2) && !map.ContainsKey("opts"))
            {
                map.Add("opts", opts2);
            }

            return map;
        }

        private IDictionary<string, string>? MapIf(
            string matcher,
            string? role,
            string defaultKey,
            string options
        )
        {
            if (options == matcher) // fallback, not really supported
            {
                return ImmutableDictionary<string, string>.Empty;
            }
            if (options.StartsWith(matcher + ','))
            {
                return Merge(
                    role == null ? null : new Dictionary<string, string> { { "role", role } },
                    DoParseOptions(options[(matcher.Length + 1)..].Trim(), defaultKey, true)
                );
            }

            return null;
        }

        private IDictionary<string, string> ParseOptions(string options, string? macroType = null)
        {
            var result = ByTypeParseOption(options, macroType);
            if ("link" == macroType && result.TryGetValue("", out var label) && label.EndsWith('^'))
            {
                result[""] = result[""][..^1];
                result.TryAdd("window", "_blank");
            }
            return result;
        }

        private IDictionary<string, string> ByTypeParseOption(string options, string? macroType)
        {
            if (macroType == "image")
            {
                return DoParseOptions(options, "", true, "alt", "width", "height");
            }

            return MapIf("source", null, "language", options)
                ?? MapIf("example", "exampleblock", "", options)
                ?? MapIf("verse", "verseblock", "", options)
                ?? MapIf("quote", "quoteblock", "attribution", options)
                ?? DoParseOptions(options, "", true);
        }

        private string? Subs(
            string? value,
            IDictionary<string, string> opts,
            IDictionary<string, string>? attributes
        )
        {
            opts.TryGetValue("subs", out var subs);
            var result = value ?? "";
            if (subs is null)
            {
                return result;
            }
            if (subs.Contains("attributes") && !subs.Contains("-attributes"))
            {
                result = EarlyAttributeReplacement(
                    result,
                    true,
                    opts,
                    attributes ?? ImmutableDictionary<string, string>.Empty
                );
            }
            return result;
        }

        private int FindSectionLevel(string line)
        {
            int sep = line.IndexOf(' ');
            return sep > 0 && line.Length > sep && Enumerable.Range(0, sep).All(i => line[i] == '=')
                ? sep
                : -1;
        }

        private IList<IElement> DoInclude(
            Macro macro,
            IContentResolver? resolver,
            IDictionary<string, string>? currentAttributes,
            bool parse
        )
        {
            if (resolver is null)
            {
                throw new ArgumentException(
                    "No content resolver so can't handle includes",
                    nameof(resolver)
                );
            }
            var content = resolver
                .Resolve(
                    macro.Label,
                    Encoding.GetEncoding(
                        macro.Options.TryGetValue("encoding", out var env) ? env : "utf-8"
                    )
                )
                ?.ToList();
            if (content is null)
            {
                if (macro.Options.ContainsKey("optional"))
                {
                    return ImmutableList<IElement>.Empty;
                }
                throw new InvalidOperationException($"Missing include: '{macro.Label}'");
            }

            macro.Options.TryGetValue("lines", out var lines);
            if (lines is not null && !string.IsNullOrWhiteSpace(lines))
            {
                // we support - index starts at 1 (line number):
                // * $line
                // * $start..$end
                // * $start..-1 (to the end)
                // * $start;$end (avoids quotes in the options)
                // * $start1..$end1,$start2..$end2
                // * $start1..$end1;$start2..$end2 (avoids quotes in the options)
                var src = content;
                content = lines
                    .Replace(';', ',')
                    .Split(",")
                    .Select(it => it.Trim())
                    .Where(it => !string.IsNullOrWhiteSpace(it))
                    .Select(it =>
                    {
                        int sep = it.IndexOf("..", StringComparison.Ordinal);
                        if (sep > 0)
                        {
                            return new List<int>
                            {
                                int.Parse(it[..sep]),
                                int.Parse(it[(sep + "..".Length)..]),
                            };
                        }
                        return [int.Parse(it)];
                    })
                    .SelectMany(range =>
                        range.Count switch
                        {
                            1 => [src[range[0] - 1]],
                            2 => src[(range[0] - 1)..(range[1] == -1 ? src.Count : range[1])],
                            _ => throw new InvalidOperationException(),
                        }
                    )
                    .ToList();
            }

            var tags = macro.Options.TryGetValue("tag", out var t)
                ? t
                : (macro.Options.TryGetValue("tags", out var t2) ? t2 : null);
            if (tags is not null)
            {
                var src = content;
                content = tags.Split(",")
                    .Select(it => it.Trim())
                    .Where(it => !string.IsNullOrWhiteSpace(it))
                    .SelectMany(tag =>
                    {
                        int from = src.IndexOf("# tag::" + tag + "[]");
                        int to = src.IndexOf("# end::" + tag + "[]");
                        return to > from && from > 0 ? src.GetRange(from + 1, to) : [];
                    })
                    .ToList();
            }

            macro.Options.TryGetValue("leveloffset", out var leveloffset);
            if (leveloffset is not null && leveloffset.Length > 0)
            {
                char first = leveloffset[0];
                int offset =
                    (first == '-' ? -1 : 1)
                    * int.Parse(first == '+' || first == '-' ? leveloffset[1..] : leveloffset);
                if (offset > 0)
                {
                    var prefix = new string('=', offset);
                    content = content
                        .Select(it => FindSectionLevel(it) > 0 ? prefix + it : it)
                        .ToList();
                }
            }

            macro.Options.TryGetValue("indent", out var indent);
            if (indent is not null)
            {
                int value = int.Parse(indent);
                var noIndent = string.Join('\n', content.Select(it => it.TrimStart())); // stripIndent
                content = (value > 0 ? noIndent.PadLeft(value) : noIndent).Split('\n').ToList();
            }

            if (parse)
            {
                return DoParse(
                    new Reader(content),
                    l => true,
                    resolver,
                    currentAttributes ?? ImmutableDictionary<string, string>.Empty,
                    true,
                    true
                );
            }
            return
            [
                new Text(
                    ImmutableList<Text.Styling>.Empty,
                    string.Join('\n', content) + '\n',
                    ImmutableDictionary<string, string>.Empty
                ),
            ];
        }

        private IList<IElement> HandleIncludes(
            string content,
            IContentResolver? resolver,
            IDictionary<string, string>? currentAttributes,
            bool parse
        )
        {
            int start = content.IndexOf("include::");
            if (start < 0)
            {
                return
                [
                    new Text(
                        ImmutableList<Text.Styling>.Empty,
                        content,
                        ImmutableDictionary<string, string>.Empty
                    ),
                ];
            }

            int opts = content.IndexOf('[', start);
            if (opts < 0)
            {
                return
                [
                    new Text(
                        ImmutableList<Text.Styling>.Empty,
                        content,
                        ImmutableDictionary<string, string>.Empty
                    ),
                ];
            }

            int end = content.IndexOf(']', opts);
            if (end < 0)
            {
                return
                [
                    new Text(
                        ImmutableList<Text.Styling>.Empty,
                        content,
                        ImmutableDictionary<string, string>.Empty
                    ),
                ];
            }

            var includeOpts = ParseOptions(content[(opts + 1)..end]);
            var includeValue = content[(start + "include::".Length)..opts];
            var include = DoInclude(
                new Macro(
                    "include",
                    includeOpts.TryGetValue("subs", out var subs) && subs.Contains("-macro")
                        ? includeValue
                        : EarlyAttributeReplacement(
                            includeValue,
                            true,
                            currentAttributes ?? ImmutableDictionary<string, string>.Empty
                        ),
                    includeOpts,
                    false
                ),
                resolver,
                currentAttributes,
                parse
            );
            IList<IList<IElement>> data =
            [
                [
                    new Text(
                        ImmutableList<Text.Styling>.Empty,
                        content[..start],
                        ImmutableDictionary<string, string>.Empty
                    ),
                ],
                include,
            ];
            return data.SelectMany(it => it).ToList();
        }

        private PassthroughBlock ParsePassthrough(
            Reader reader,
            IDictionary<string, string>? documentOptions,
            IDictionary<string, string>? options,
            string marker,
            IContentResolver? resolver
        )
        {
            var content = new StringBuilder();
            string? next;
            while ((next = reader.NextLine()) != null && marker != next.Trim())
            {
                if (content.Length > 0)
                {
                    content.Append('\n');
                }
                content.Append(next);
            }
            if (next != null && !next.StartsWith(marker))
            {
                reader.Rewind();
            }

            var text = content.ToString();
            var actualOpts = options ?? ImmutableDictionary<string, string>.Empty;
            if (!text.Contains("include::"))
            {
                return new PassthroughBlock(
                    Subs(text, actualOpts, documentOptions) ?? "",
                    actualOpts
                );
            }

            var filtered = string.Join(
                '\n',
                text.Split("\n")
                    .Select(it =>
                    {
                        try
                        {
                            return it.StartsWith("include::")
                                ? string.Join(
                                    "",
                                    HandleIncludes(it, resolver, actualOpts, false)
                                        .Select(e => e is Text t ? t.Value : "")
                                )
                                : it;
                        }
                        catch (Exception)
                        {
                            return it;
                        }
                    })
            );
            return new PassthroughBlock(
                Subs(filtered, actualOpts, documentOptions) ?? "",
                actualOpts
            );
        }

        private IElement NewText(
            IList<Text.Styling>? styles,
            string value,
            IDictionary<string, string>? options
        )
        {
            // cheap handling of legacy syntax for anchors - for backward compat but only when at the beginning or end, not yet in the middle
            string? id = null;
            string text = value;
            if (value.StartsWith("[["))
            {
                int end = value.IndexOf("]]");
                if (end > 0)
                {
                    id = value["[[".Length..end];
                    text = text[(end + "]]".Length)..].Trim();
                }
            }
            else if (value.EndsWith("]]"))
            {
                int start = value.LastIndexOf("[[");
                if (start > 0)
                {
                    id = value[(start + "[[".Length)..(value.Length - "]]".Length)];
                    text = text[..start].Trim();
                }
            }

            if (
                id is not null
                && !string.IsNullOrWhiteSpace(id)
                && (options is null || !options.ContainsKey("id"))
            )
            {
                var opts = new Dictionary<string, string>(
                    options ?? ImmutableDictionary<string, string>.Empty
                );
                opts.TryAdd("id", id);
                return new Text(styles ?? ImmutableList<Text.Styling>.Empty, text, opts);
            }
            return new Text(
                styles ?? ImmutableList<Text.Styling>.Empty,
                text,
                options ?? ImmutableDictionary<string, string>.Empty
            ); // todo: check nested links, email - without escaping
        }

        private bool IsLink(string link)
        {
            return LinkPrefixes.Any(link.StartsWith);
        }

        private int FindNextLink(string line, int from)
        {
            return LinkPrefixes
                .Select(p => line.IndexOf(p, from))
                .Where(i => i >= from)
                .DefaultIfEmpty(-1)
                .Min();
        }

        private void FlushText(IList<IElement> elements, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }
            int start = 0;
            while (start < content.Length)
            {
                int next = FindNextLink(content, start);
                if (next < 0)
                {
                    elements.Add(NewText(null, start == 0 ? content : content[start..], null));
                    break;
                }

                int end = new List<string> { " ", "\t" }
                    .Select(s => content.IndexOf(s, next, StringComparison.Ordinal))
                    .Where(i => i > next)
                    .DefaultIfEmpty(content.Length)
                    .Min();

                if (start != next)
                {
                    elements.Add(NewText(null, content[start..next], null));
                }

                var link = content[next..end];
                elements.Add(new Link(link, link, ImmutableDictionary<string, string>.Empty));
                if (end == content.Length)
                {
                    break;
                }
                start = end;
            }
        }

        private IElement UnwrapElementIfPossible(Paragraph element)
        {
            if (element.Children.Count != 1)
            {
                return element;
            }

            var first = element.Children[0];
            if (first is UnOrderedList l)
            {
                return new UnOrderedList(l.Children, Merge(l.Options, element.Options));
            }
            if (first is OrderedList ol)
            {
                return new OrderedList(ol.Children, Merge(ol.Options, element.Options));
            }
            if (first is Section s)
            {
                return new Section(s.Level, s.Title, s.Children, Merge(s.Options, element.Options));
            }
            if (first is Text t)
            {
                return new Text(t.Style, t.Value, Merge(t.Options, element.Options));
            }
            if (first is Code c)
            {
                return new Code(c.Value, c.CallOuts, Merge(c.Options, element.Options), c.Inline);
            }
            if (first is Link lk)
            {
                return new Link(lk.Url, lk.Label, Merge(lk.Options, element.Options));
            }
            if (first is Macro m)
            {
                return new Macro(m.Name, m.Label, Merge(m.Options, element.Options), m.Inline);
            }
            if (first is Quote q)
            {
                return new Quote(q.Children, Merge(q.Options, element.Options));
            }
            if (first is OpenBlock b)
            {
                return new OpenBlock(b.Children, Merge(b.Options, element.Options));
            }
            if (first is PageBreak p)
            {
                return new PageBreak(Merge(p.Options, element.Options));
            }
            if (first is LineBreak lb)
            {
                return lb;
            }
            if (first is DescriptionList d && element.Options.Count == 0)
            {
                return new DescriptionList(d.Children, Merge(d.Options, element.Options));
            }
            if (first is ConditionalBlock cb)
            {
                return new ConditionalBlock(
                    cb.Evaluator,
                    cb.Children,
                    Merge(cb.Options, element.Options)
                );
            }
            if (first is Admonition a && element.Options.Count == 0)
            {
                return a;
            }

            return element;
        }

        private IDictionary<string, string> RemoveEmptyKey(IDictionary<string, string> options)
        {
            return options.Where(it => !string.IsNullOrWhiteSpace(it.Key)).ToImmutableDictionary();
        }

        private IList<IElement> FlattenTexts(IList<IElement> elements)
        {
            if (elements.Count <= 1)
            {
                return elements;
            }

            var result = new List<IElement>(elements.Count + 1);
            var buffer = new List<Text>(2);
            foreach (var elt in elements)
            {
                if (elt is Text t && t.Style.Count == 0 && t.Options.Count == 0)
                {
                    buffer.Add(t);
                }
                else
                {
                    if (buffer.Count > 0)
                    {
                        result.Add(MergeTexts(buffer));
                        buffer.Clear();
                    }
                    result.Add(elt);
                }
            }
            if (buffer.Count > 0)
            {
                result.Add(MergeTexts(buffer));
            }
            return result;
        }

        private IElement MergeTexts(IList<Text> buffer)
        {
            return buffer.Count == 1
                ? buffer[0]
                : NewText(
                    null,
                    string.Join(
                        " ",
                        new List<IEnumerable<string>>
                        {
                            new List<string> { buffer[0].Value.TrimEnd() },
                            buffer.Skip(1).Take(buffer.Count - 2).Select(i => i.Value.Trim()),
                            new List<string> { buffer[buffer.Count - 1].Value.TrimStart() },
                        }.SelectMany(it => it)
                    ),
                    null
                );
        }

        private void AddTextElements(
            string line,
            int i,
            int end,
            List<IElement> collector,
            Text.Styling? style,
            string? options,
            IContentResolver? resolver,
            IDictionary<string, string> currentAttributes
        )
        {
            var content = line[(i + 1)..end];
            var sub = ParseLine(null, content, resolver, currentAttributes, true);
            var opts = options is not null
                ? ParseOptions(options)
                : ImmutableDictionary<string, string>.Empty;
            if (sub.Count == 1 && sub[0] is Text t)
            {
                if (t.Style.Count > 0)
                {
                    var styles = new List<Text.Styling>();
                    if (style is not null)
                    {
                        styles.Add(style.Value);
                    }
                    foreach (var s in t.Style)
                    {
                        styles.Add(s);
                    }
                    collector.Add(NewText(styles, t.Value, opts));
                }
                else
                {
                    collector.Add(
                        NewText(
                            style is null
                                ? ImmutableList<Text.Styling>.Empty
                                : new List<Text.Styling> { style.Value },
                            t.Value,
                            opts
                        )
                    );
                }
            }
            else if (sub.Count == 1 && opts.Count > 0) // quick way to fusion parseLine options and provided options
            {
                collector.Add(UnwrapElementIfPossible(new Paragraph(sub, opts)));
            }
            else
            {
                foreach (
                    var it in sub
                    // todo: for now we loose the style for what is not pure text, should we handle it as an element or role maybe?
                    .Select(it =>
                        it is Text t
                            ? NewText(
                                style == null ? null : new List<Text.Styling> { style.Value },
                                t.Value,
                                opts
                            )
                            : it
                    )
                )
                {
                    collector.Add(it);
                }
            }
        }

        private bool IsInlineOptionContentMarker(char c)
        {
            return c == '#';
        }

        private Admonition? ParseAdmonition(
            Reader reader,
            string line,
            IContentResolver? resolver,
            IDictionary<string, string> currentAttributes
        )
        {
            int firstSemiColon = line.IndexOf(':');
            if (firstSemiColon < 0)
            {
                return null;
            }

            var name = line[..firstSemiColon].Trim();
            Admonition.AdmonitionLevel? level = name switch
            {
                "IMPORTANT" => Admonition.AdmonitionLevel.Important,
                "CAUTION" => Admonition.AdmonitionLevel.Caution,
                "TIP" => Admonition.AdmonitionLevel.Tip,
                "NOTE" => Admonition.AdmonitionLevel.Note,
                "WARNING" => Admonition.AdmonitionLevel.Warning,
                _ => null,
            };
            if (level is null)
            {
                return null;
            }
            var buffer = new List<string>();
            buffer.Add(line[(Enum.GetName(level.Value)!.Length + 1)..].TrimStart());
            string? next;
            while ((next = reader.NextLine()) != null && !string.IsNullOrWhiteSpace(next))
            {
                buffer.Add(next);
            }
            if (next != null)
            {
                reader.Rewind();
            }
            return new Admonition(
                level.Value,
                UnwrapElementIfPossible(
                    ParseParagraph(new Reader(buffer), null, resolver, currentAttributes, true)
                )
            );
        }

        private bool IsBlock(string strippedLine)
        {
            return "----" == strippedLine
                || "```" == strippedLine
                || "--" == strippedLine
                || "++++" == strippedLine;
        }

        private void ReadContinuation(
            Reader reader,
            string prefix,
            Regex regex,
            StringBuilder buffer,
            string nextStripped
        )
        {
            buffer.Append(nextStripped[prefix.Length..].TrimStart());

            string? next;
            string? needed = null;
            while ((next = reader.NextLine()) != null)
            {
                if (needed == next)
                {
                    needed = null;
                }
                else if (IsBlock(next))
                {
                    needed = next;
                }
                else if (needed == null && string.IsNullOrWhiteSpace(next))
                {
                    break;
                }
                else if ("+" == next.Trim())
                { // continuation
                    buffer.Append('\n');
                    continue;
                }
                else if (needed == null && regex.Match(next.Trim()).Success)
                {
                    break;
                }
                buffer.Append('\n').Append(next);
            }
            if (next != null)
            {
                reader.Rewind();
            }
        }

        private void AddCollapsingChildOnParent(IList<IElement> children, IElement elt)
        {
            if (children.Count > 0)
            {
                int lastIdx = children.Count - 1;
                var last = children[lastIdx];
                if (last is Paragraph p)
                {
                    children[lastIdx] = new Paragraph([.. p.Children, elt], p.Options);
                }
                else if (last is Text t)
                {
                    children[lastIdx] = new Paragraph([last, elt], t.Options);
                }
                else
                {
                    children.Add(elt);
                }
            }
            else
            {
                children.Add(elt);
            }
        }

        private T ParseList<T>(
            Reader reader,
            string? options,
            string prefix,
            Regex regex,
            string captureName,
            Func<T, IList<IElement>> childrenAccessor,
            Func<IList<IElement>, IDictionary<string, string>, T> factory,
            IContentResolver? resolver,
            IDictionary<string, string> currentAttributes
        )
            where T : IElement
        {
            var children = new List<IElement>(2);
            string? next;
            string? nextStripped;
            var buffer = new StringBuilder();
            Match matcher;
            int currentLevel =
                prefix.Length
                - 1 /*ending space*/
            ;
            while (
                (next = reader.NextLine()) != null
                && (matcher = regex.Match((nextStripped = next.Trim()))).Success
                && !string.IsNullOrWhiteSpace(next)
            )
            {
                var level = matcher.Groups[captureName].Length;
                if (level < currentLevel)
                { // go back to parent
                    break;
                }
                if (level == currentLevel)
                { // a new item
                    buffer.Clear();
                    ReadContinuation(reader, prefix, regex, buffer, nextStripped);

                    var elements = DoParse(
                        new Reader(buffer.ToString().Split("\n")),
                        l => true,
                        resolver,
                        currentAttributes,
                        true,
                        false
                    );
                    children.Add(
                        elements.Count > 1
                            ? new Paragraph(elements, ImmutableDictionary<string, string>.Empty)
                            : elements[0]
                    );
                }
                else
                { // nested
                    reader.Rewind();
                    var nestedList = ParseList(
                        reader,
                        null,
                        prefix[0] + prefix,
                        regex,
                        captureName,
                        childrenAccessor,
                        factory,
                        resolver,
                        currentAttributes
                    );
                    if (childrenAccessor(nestedList).Count > 0)
                    {
                        AddCollapsingChildOnParent(children, nestedList);
                    }
                }
            }
            if (next != null)
            {
                reader.Rewind();
            }
            return factory(
                children,
                options == null ? ImmutableDictionary<string, string>.Empty : ParseOptions(options)
            );
        }

        private OrderedList ParseOrderedList(
            Reader reader,
            string? options,
            string prefix,
            IContentResolver? resolver,
            IDictionary<string, string> currentAttributes
        )
        {
            return ParseList(
                reader,
                options,
                prefix,
                OrderedListPrefix(),
                "dots",
                it => it.Children,
                (c, o) => new OrderedList(c, o),
                resolver,
                currentAttributes
            );
        }

        private UnOrderedList ParseUnorderedList(
            Reader reader,
            string? options,
            string prefix,
            IContentResolver? resolver,
            IDictionary<string, string> currentAttributes
        )
        {
            return ParseList(
                reader,
                options,
                prefix,
                UnOrderedListPrefix(),
                "wildcard",
                it => it.Children,
                (c, o) => new UnOrderedList(c, o),
                resolver,
                currentAttributes
            );
        }

        private Paragraph ParseParagraph(
            Reader reader,
            IDictionary<string, string>? options,
            IContentResolver? resolver,
            IDictionary<string, string> currentAttributes,
            bool supportComplexStructures /* title case for ex */
        )
        {
            var elements = new List<IElement>();
            string? line;
            while ((line = reader.NextLine()) != null && !string.IsNullOrWhiteSpace(line))
            {
                if (line.StartsWith("=") || (line.StartsWith("[") && line.EndsWith("]")))
                {
                    reader.Rewind();
                    break;
                }
                foreach (
                    var it in ParseLine(
                        reader,
                        EarlyAttributeReplacement(line, false, currentAttributes),
                        resolver,
                        currentAttributes,
                        supportComplexStructures
                    )
                )
                {
                    elements.Add(it);
                }
            }
            if (
                elements.Count == 1
                && elements[0] is Paragraph p
                && (options == null || options.Count == 0)
            )
            {
                return p;
            }
            return new Paragraph(
                FlattenTexts(elements),
                options ?? ImmutableDictionary<string, string>.Empty
            );
        }

        private DescriptionList ParseDescriptionList(
            Reader reader,
            string prefix,
            IContentResolver? resolver,
            IDictionary<string, string> currentAttributes
        )
        {
            var order = new List<IElement>();
            var children = new SortedDictionary<IElement, IElement>(
                Comparer<IElement>.Create((a, b) => order.IndexOf(a) - order.IndexOf(b))
            );
            string? next;
            var buffer = new List<string>();
            Match matcher;
            int currentLevel =
                prefix.Length
                - 1 /*ending space*/
            ;
            IElement? last = null;
            while (
                (next = reader.NextLine()) != null
                && (matcher = DescriptionListPrefix().Match(next)).Success
                && !string.IsNullOrWhiteSpace(next)
            )
            {
                var level = matcher.Groups["marker"].Length;
                if (level < currentLevel)
                { // go back to parent
                    break;
                }
                if (level == currentLevel)
                { // a new item
                    buffer.Clear();
                    var content = matcher.Groups["content"].Value.TrimStart();
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        buffer.Add(content);
                    }
                    string? needed = null;
                    while (
                        (next = reader.NextLine()) != null
                        && (
                            (
                                !DescriptionListPrefix().Match(next).Success
                                && !string.IsNullOrWhiteSpace(next)
                            )
                            || needed != null
                        )
                    )
                    {
                        if (next.Trim() == "+")
                        {
                            buffer.Add("");
                            continue;
                        }

                        buffer.Add(next);
                        if (needed == next)
                        {
                            needed = null;
                        }
                        else if (IsBlock(next))
                        {
                            needed = next;
                        }
                    }
                    if (next != null)
                    {
                        reader.Rewind();
                    }
                    var element = DoParse(
                        new Reader(buffer),
                        s => true,
                        resolver,
                        currentAttributes,
                        true,
                        false
                    );
                    var unwrapped = UnwrapElementIfPossible(
                        element.Count == 1 && element[0] is Paragraph p
                            ? p
                            : new Paragraph(element, ImmutableDictionary<string, string>.Empty)
                    );
                    var key = DoParse(
                        new Reader(new List<string> { matcher.Groups["name"].Value }),
                        l => true,
                        resolver,
                        currentAttributes,
                        false,
                        false
                    );

                    var item =
                        key.Count == 1
                            ? key[0]
                            : new Paragraph(
                                key,
                                new Dictionary<string, string> { { "nowrap", "true" } }
                            );
                    order.Add(item);
                    children.Add(item, unwrapped);
                    last = unwrapped;
                }
                else
                { // nested
                    reader.Rewind();
                    var nestedList = ParseDescriptionList(
                        reader,
                        prefix[0] + prefix,
                        resolver,
                        currentAttributes
                    );
                    if (nestedList.Children.Count > 0 && last != null)
                    {
                        AddCollapsingChildOnParent([last], nestedList);
                    }
                }
            }
            if (next != null)
            {
                reader.Rewind();
            }
            return new DescriptionList(
                children,
                currentAttributes ?? ImmutableDictionary<string, string>.Empty
            );
        }

        private IList<IElement> ParseLine(
            Reader? reader,
            string line,
            IContentResolver? resolver,
            IDictionary<string, string> currentAttributes,
            bool supportComplexStructures
        )
        {
            var elements = new List<IElement>();
            int start = 0;
            bool inMacro = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (supportComplexStructures)
                {
                    if (i == line.Length - 2 && line.EndsWith(" +"))
                    {
                        FlushText(elements, line[start..i]);
                        elements.Add(new LineBreak());
                        start = line.Length;
                        break;
                    }

                    var admonition = reader is null
                        ? null
                        : ParseAdmonition(reader, line, resolver, currentAttributes);
                    if (admonition is not null)
                    {
                        elements.Add(admonition);
                        i = line.Length;
                        start = i;
                        break;
                    }

                    {
                        var matcher = OrderedListPrefix().Match(line);
                        if (matcher.Success && matcher.Groups["dots"].Length == 1)
                        {
                            reader!.Rewind();
                            elements.Add(
                                ParseOrderedList(reader, null, ". ", resolver, currentAttributes)
                            );
                            i = line.Length;
                            start = i;
                            break;
                        }
                    }

                    if (line.StartsWith("*"))
                    {
                        var matcher = UnOrderedListPrefix().Match(line);
                        if (matcher.Success && matcher.Groups["wildcard"].Length == 1)
                        {
                            reader!.Rewind();
                            elements.Add(
                                ParseUnorderedList(reader, null, "* ", resolver, currentAttributes)
                            );
                            i = line.Length;
                            start = i;
                            break;
                        }
                    }

                    if (line.Contains("::"))
                    {
                        var matcher = DescriptionListPrefix().Match(line);
                        if (
                            matcher.Success
                            && matcher.Groups["marker"].Length == 2
                            &&
                            // and is not a macro
                            (
                                line.EndsWith("::")
                                || line[(line.IndexOf("::") + "::".Length)..].StartsWith(" ")
                            )
                        )
                        {
                            reader!.Rewind();
                            elements.Add(
                                ParseDescriptionList(reader, ":: ", resolver, currentAttributes)
                            );
                            i = line.Length;
                            start = i;
                            break;
                        }
                    }
                }

                char c = line[i];
                if (inMacro && c != '[')
                {
                    continue;
                }

                switch (c)
                {
                    case ':':
                        inMacro =
                            line.Length > i + 1
                            && line[i + 1] != ' '
                            && i > 0
                            && line[i - 1] != ' '
                            && line.IndexOf('[', i + 1) > i;
                        break;
                    case '\\':
                    { // escaping
                        if (start != i)
                        {
                            FlushText(elements, line[start..i]);
                        }
                        i++;
                        start = i;
                        break;
                    }
                    case '{':
                    {
                        int end = line.IndexOf('}', i + 1);
                        if (end > 0)
                        {
                            if (start != i)
                            {
                                FlushText(elements, line[start..i]);
                            }
                            var attributeName = line[(i + 1)..end];
                            elements.Add(
                                new Model.Attribute(
                                    attributeName,
                                    value =>
                                        DoParse(
                                            new Reader([value]),
                                            _ => true,
                                            resolver,
                                            new Dictionary<string, string>(currentAttributes),
                                            true,
                                            false
                                        )
                                )
                            );
                            i = end;
                            start = end + 1;
                        }
                        break;
                    }
                    case '*':
                    {
                        int end = line.IndexOf('*', i + 1);
                        if (end > 0)
                        {
                            if (start != i)
                            {
                                FlushText(elements, line[start..i]);
                            }
                            AddTextElements(
                                line,
                                i,
                                end,
                                elements,
                                Text.Styling.Bold,
                                null,
                                resolver,
                                currentAttributes
                            );
                            i = end;
                            start = end + 1;
                        }
                        break;
                    }
                    case '_':
                    {
                        int end = line.IndexOf('_', i + 1);
                        if (end > 0)
                        {
                            if (start != i)
                            {
                                FlushText(elements, line[start..i]);
                            }
                            AddTextElements(
                                line,
                                i,
                                end,
                                elements,
                                Text.Styling.Italic,
                                null,
                                resolver,
                                currentAttributes
                            );
                            i = end;
                            start = end + 1;
                        }
                        break;
                    }
                    case '~':
                    {
                        int end = line.IndexOf('~', i + 1);
                        if (end > 0)
                        {
                            if (start != i)
                            {
                                FlushText(elements, line[start..i]);
                            }
                            AddTextElements(
                                line,
                                i,
                                end,
                                elements,
                                Text.Styling.Sub,
                                null,
                                resolver,
                                currentAttributes
                            );
                            i = end;
                            start = end + 1;
                        }
                        break;
                    }
                    case '^':
                    {
                        int end = line.IndexOf('^', i + 1);
                        if (end > 0)
                        {
                            if (start != i)
                            {
                                FlushText(elements, line[start..i]);
                            }
                            AddTextElements(
                                line,
                                i,
                                end,
                                elements,
                                Text.Styling.Sup,
                                null,
                                resolver,
                                currentAttributes
                            );
                            i = end;
                            start = end + 1;
                        }
                        break;
                    }
                    case '[':
                    {
                        inMacro = false; // we'll parse it so all good, no more need to escape anything
                        var end = line.IndexOf(']', i + 1);
                        while (end > 0)
                        {
                            if (line[end - 1] != '\\')
                            {
                                break;
                            }
                            end = line.IndexOf(']', end + 1);
                        }

                        if (
                            end > 0
                            && (
                                end == (line.Length - 1)
                                || !IsInlineOptionContentMarker(line[end + 1])
                            )
                        )
                        { // check it is maybe a link
                            var subLine = line[start..].Trim();
                            var canBeLink = IsLink(subLine) || subLine.StartsWith("link:");

                            var backward = -1;
                            var previousSemicolon = line.LastIndexOf(':', i);
                            if (previousSemicolon > 0 || canBeLink)
                            {
                                if (
                                    line[
                                        previousSemicolon..Math.Min(
                                            previousSemicolon + "://".Length,
                                            end
                                        )
                                    ] == "://"
                                )
                                {
                                    // likely a link
                                    var previousSpace = line.LastIndexOf(' ', previousSemicolon);
                                    if (previousSpace >= 0)
                                    {
                                        var link = line[(previousSpace + 1)..(end + 1)];
                                        if (IsLink(link) || link.StartsWith("link:"))
                                        {
                                            backward = previousSpace + 1;
                                        }
                                    }
                                }

                                if (backward < 0)
                                {
                                    var antepenultimateSemicolon = line.IndexOf(':', start);
                                    backward =
                                        line.LastIndexOf(
                                            ' ',
                                            antepenultimateSemicolon > 0
                                                ? antepenultimateSemicolon
                                                : previousSemicolon
                                        ) + 1;
                                }
                            }

                            if (backward >= 0 && backward < i)
                            { // start by assuming it a link then fallback on a macro
                                var optionsPrefix = line[backward..i];
                                if (start < backward)
                                {
                                    FlushText(elements, line[start..backward]);
                                }

                                var macroMarker = optionsPrefix.IndexOf(
                                    ':',
                                    StringComparison.Ordinal
                                );
                                if (macroMarker > 0 && !IsLink(optionsPrefix))
                                {
                                    var inlined =
                                        optionsPrefix.Length <= macroMarker + 1
                                        || optionsPrefix[macroMarker + 1] != ':';
                                    var type = optionsPrefix[0..macroMarker];
                                    var options = ParseOptions(line[(i + 1)..end].Trim(), type);
                                    var label =
                                        "stem" == type
                                            ? line[(i + 1)..end]
                                            : optionsPrefix[(macroMarker + (inlined ? 1 : 2))..];

                                    if (
                                        "link" == type
                                        && options.TryGetValue("", out string? linkName)
                                    )
                                    {
                                        int from = linkName.IndexOf('[');
                                        while (from > 0)
                                        { // if label has some opening bracket we must slice what we computed (images in link)
                                            end = end >= line.Length ? -1 : line.IndexOf(']', end + 1);
                                            from =
                                                from >= label.Length
                                                    ? -1
                                                    : label.IndexOf('[', from + 1);
                                            options = ParseOptions(line[(i + 1)..end].Trim());
                                        }
                                    }

                                    var macro = new Macro(
                                        type,
                                        label,
                                        "stem" == type
                                            ? ImmutableDictionary<string, string>.Empty
                                            : options,
                                        inlined
                                    );
                                    switch (macro.Name)
                                    {
                                        case "include":
                                            foreach (
                                                var it in DoInclude(
                                                    macro,
                                                    resolver,
                                                    currentAttributes,
                                                    true
                                                )
                                            )
                                            {
                                                elements.Add(it);
                                            }
                                            break;
                                        case "ifdef":
                                            elements.Add(
                                                new ConditionalBlock(
                                                    new ConditionalBlock.Ifdef(macro.Label).Test,
                                                    DoParse(
                                                        new Reader(ReadIfBlock(reader!)),
                                                        l => true,
                                                        resolver,
                                                        currentAttributes,
                                                        false,
                                                        false
                                                    ),
                                                    macro.Options
                                                )
                                            );
                                            break;
                                        case "ifndef":
                                            elements.Add(
                                                new ConditionalBlock(
                                                    new ConditionalBlock.Ifndef(macro.Label).Test,
                                                    DoParse(
                                                        new Reader(ReadIfBlock(reader!)),
                                                        l => true,
                                                        resolver,
                                                        currentAttributes,
                                                        false,
                                                        false
                                                    ),
                                                    macro.Options
                                                )
                                            );
                                            break;
                                        case "ifeval":
                                            elements.Add(
                                                new ConditionalBlock(
                                                    new ConditionalBlock.Ifeval(
                                                        ParseCondition(
                                                            string.IsNullOrWhiteSpace(macro.Label)
                                                                ? line[(i + 1)..end]
                                                                : macro.Label.Trim(),
                                                            currentAttributes
                                                        )
                                                    ).Test,
                                                    DoParse(
                                                        new Reader(ReadIfBlock(reader!)),
                                                        l => true,
                                                        resolver,
                                                        currentAttributes,
                                                        false,
                                                        false
                                                    ),
                                                    macro.Options
                                                )
                                            );
                                            break;
                                        default:
                                            elements.Add(macro);
                                            break;
                                    }
                                    ;
                                }
                                else
                                {
                                    var options = ParseOptions(line[(i + 1)..end].Trim());
                                    elements.Add(
                                        new Link(
                                            optionsPrefix,
                                            options.TryGetValue("", out var ll)
                                                ? ll
                                                : optionsPrefix,
                                            RemoveEmptyKey(options)
                                        )
                                    );
                                }
                                i = end;
                                start = end + 1;
                                continue;
                            }

                            var contentMarkerStart = end + 1;
                            if (line.Length > contentMarkerStart)
                            {
                                int next = line[contentMarkerStart];
                                if (next == '#')
                                { // inline role
                                    int end2 = line.IndexOf('#', contentMarkerStart + 1);
                                    if (end2 > 0)
                                    {
                                        if (start != i)
                                        {
                                            FlushText(elements, line[start..i]);
                                        }
                                        AddTextElements(
                                            line,
                                            contentMarkerStart,
                                            end2,
                                            elements,
                                            null,
                                            line[(i + 1)..end],
                                            resolver,
                                            currentAttributes
                                        );
                                        i = end2;
                                        start = end2 + 1;
                                    }
                                } // else?
                            }
                        }
                        break;
                    }
                    case '#':
                    {
                        int j;
                        for (j = 1; j < line.Length - i; j++)
                        {
                            if (line[i + j] != '#')
                            {
                                break;
                            }
                        }
                        j--;
                        if (i + j == line.Length)
                        {
                            throw new InvalidOperationException(
                                "You can't do a line of '#': " + line
                            );
                        }

                        var endString =
                            j == 0
                                ? "#"
                                : string.Join("", Enumerable.Range(0, j + 1).Select(idx => "#"));
                        int end = line.IndexOf(endString, i + endString.Length);
                        if (end > 0)
                        {
                            // override options if set inline (todo: do it for all inline markers)
                            string? options = null;
                            if (i > 0 && ']' == line[i - 1])
                            {
                                int optionsStart = line.LastIndexOf('[', i - 1);
                                if (optionsStart >= 0)
                                {
                                    options = line[(optionsStart + 1)..(i - 1)];
                                    // adjust indices to skip options
                                    if (start < optionsStart)
                                    {
                                        FlushText(elements, line[start..optionsStart]);
                                    }
                                }
                                else if (start < i)
                                {
                                    FlushText(elements, line[start..i]);
                                }
                            }
                            else if (start < i)
                            {
                                FlushText(elements, line[start..i]);
                            }

                            AddTextElements(
                                line,
                                i + endString.Length - 1,
                                end,
                                elements,
                                Text.Styling.Mark,
                                options,
                                resolver,
                                currentAttributes
                            );
                            start = end + endString.Length;
                            i = start - 1;
                        }
                        break;
                    }
                    case '`':
                    {
                        int end = line.IndexOf('`', i + 1);
                        if (end > 0)
                        {
                            if (start != i)
                            {
                                FlushText(elements, line[start..i]);
                            }
                            var content = line[(i + 1)..end];
                            if (IsLink(content))
                            { // this looks like a bad practise but can happen
                                var link = UnwrapElementIfPossible(
                                    ParseParagraph(
                                        new Reader(new List<string> { content }),
                                        ImmutableDictionary<string, string>.Empty,
                                        resolver,
                                        ImmutableDictionary<string, string>.Empty,
                                        false
                                    )
                                );
                                if (link is Link l)
                                {
                                    var roleValue = l.Options.TryGetValue("role", out var rl)
                                        ? rl
                                        : "";
                                    elements.Add(
                                        roleValue.Contains("inline-code")
                                            ? l
                                            : new Link(
                                                l.Url,
                                                l.Label,
                                                // inject role inline-code
                                                l.Options.Where(it => "role" != it.Key)
                                                    .Concat(
                                                        [
                                                            new KeyValuePair<string, string>(
                                                                "role",
                                                                $"{roleValue} inline-code".TrimStart()
                                                            ),
                                                        ]
                                                    )
                                                    .ToDictionary()
                                            )
                                    );
                                }
                            }
                            else
                            {
                                elements.Add(
                                    new Code(
                                        content.Replace("\\{", "{"), // quick and dirty unescaping of variables
                                        ImmutableList<CallOut>.Empty,
                                        ImmutableDictionary<string, string>.Empty,
                                        true
                                    )
                                );
                            }
                            i = end;
                            start = end + 1;
                        }
                        break;
                    }
                    case '<':
                    {
                        if (
                            line.Length > i + 4 /*<<x>>*/
                            && line[i + 1] == '<'
                        )
                        {
                            int end = line.IndexOf(">>", i + 1, StringComparison.Ordinal);
                            if (end > 0)
                            {
                                if (start != i)
                                {
                                    FlushText(elements, line[start..i]);
                                }
                                var name = line[(i + 2)..end];
                                int sep = name.IndexOf(',');
                                if (sep > 0)
                                {
                                    elements.Add(new Anchor(name[0..sep], name[(sep + 1)..]));
                                }
                                else
                                {
                                    elements.Add(
                                        new Anchor(
                                            name,
                                            "" /* renderer should resolve the section and use its title */
                                        )
                                    );
                                }
                                i = end;
                                start = end + ">>".Length;
                            }
                        }
                        break;
                    }
                    default:
                        break;
                }
            }
            if (start < line.Length)
            {
                FlushText(elements, line[start..]);
            }
            return FlattenTexts(elements);
        }

        private IElement ParseSection(
            Reader reader,
            IDictionary<string, string> options,
            IContentResolver? resolver,
            IDictionary<string, string> currentAttributes
        )
        {
            var title = reader.SkipCommentsAndEmptyLines();
            int i = 0;
            while (i < title?.Length && title[i] == '=')
            {
                i++;
            }

            currentAttributes.TryGetValue("leveloffset", out var offset);
            if (offset is not null)
            {
                i += int.Parse(offset);
            }

            // implicit attribute
            currentAttributes["sectnumlevels"] = i.ToString();

            var prefix = new string('=', i + 1);
            var lineContent = title![i..].Trim();
            var titleElement = ParseLine(
                new Reader([lineContent]),
                lineContent,
                resolver,
                currentAttributes,
                false
            );
            return new Section(
                i,
                titleElement.Count == 1
                    ? titleElement[0]
                    : new Paragraph(
                        titleElement,
                        new Dictionary<string, string> { { "nowrap", "true" } }
                    ),
                DoParse(
                    reader,
                    line => !line!.StartsWith('=') || line.StartsWith(prefix),
                    resolver,
                    currentAttributes,
                    true,
                    true
                ),
                options == null ? ImmutableDictionary<string, string>.Empty : options
            );
        }

        private ContentWithCalloutIndices ParseWithCallouts(string snippet)
        {
            StringBuilder? result = null;
            HashSet<int>? callOuts = null;
            var lines = snippet.Split("\n");
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var matcher = CallOutRef().Match(line);
                if (matcher.Success)
                {
                    if (result == null)
                    {
                        result = new StringBuilder();
                        callOuts = new HashSet<int>(2);
                        if (i > 0)
                        {
                            for (int j = 0; j < Math.Min(lines.Length, i); j++)
                            {
                                result.Append(lines[j]).Append('\n');
                            }
                        }
                    }
                    try
                    {
                        var number = int.Parse(matcher.Groups["number"].Value);
                        callOuts!.Add(number);
                        result!
                            .Append(line.Replace(matcher.Value, "(" + number + ')'))
                            .Append('\n');
                    }
                    catch (Exception)
                    {
                        throw new InvalidOperationException(
                            "Can't parse a callout on line '" + line + "' in\n" + snippet
                        );
                    }
                }
                else
                {
                    result?.Append(line).Append('\n');
                }
            }
            return result is null
                ? new ContentWithCalloutIndices(snippet.TrimEnd(), ImmutableList<int>.Empty)
                : new ContentWithCalloutIndices(
                    result.ToString().TrimEnd(),
                    callOuts is not null ? callOuts.ToList() : ImmutableList<int>.Empty
                );
        }

        private Code ParseCodeBlock(
            Reader reader,
            IDictionary<string, string>? options,
            IContentResolver? resolver,
            IDictionary<string, string> currentAttributes,
            string marker
        )
        {
            var builder = new StringBuilder();
            string? next;
            while ((next = reader.NextLine()) is not null && marker != next.Trim())
            {
                builder.Append(next).Append('\n');
            }

            // todo: better support of the code features/syntax config
            var content = builder.ToString();
            var snippet = HandleIncludes(content, resolver, currentAttributes, false);
            var code = string.Join(
                "",
                snippet.Where(it => it is Text).Select(it => (it as Text)!.Value)
            );
            var codeOptions = options ?? ImmutableDictionary<string, string>.Empty;

            var contentWithCallouts = ParseWithCallouts(code);
            if (contentWithCallouts.CallOutReferences.Count == 0)
            {
                IDictionary<string, string> opts =
                    codeOptions ?? ImmutableDictionary<string, string>.Empty;
                return new Code(
                    Subs(code, opts, currentAttributes) ?? "",
                    ImmutableList<CallOut>.Empty,
                    opts,
                    false
                );
            }

            var callOuts = new List<CallOut>(contentWithCallouts.CallOutReferences.Count);
            Match matcher;
            while (
                (next = reader.SkipCommentsAndEmptyLines()) != null
                && (matcher = CallOut().Match(next)).Success
            )
            {
                int number;
                try
                {
                    var numberRef = matcher.Groups["number"].Value;
                    number = "." == numberRef ? callOuts.Count + 1 : int.Parse(numberRef);
                }
                catch (Exception)
                {
                    throw new InvalidOperationException("Invalid callout: '" + next + "'");
                }

                var text = matcher.Groups["description"].Value;
                while (
                    (next = reader.NextLine()) != null
                    && !next.StartsWith('<')
                    && !string.IsNullOrWhiteSpace(next)
                )
                {
                    text += '\n' + next;
                }
                if (next != null && !string.IsNullOrWhiteSpace(next) && next.StartsWith('<'))
                {
                    reader.Rewind();
                }

                var elements = DoParse(
                    new Reader(text.Split("\n")),
                    l => true,
                    resolver,
                    currentAttributes,
                    true,
                    false
                );
                callOuts.Add(
                    new CallOut(
                        number,
                        elements.Count == 1
                            ? elements[0]
                            : new Paragraph(elements, ImmutableDictionary<string, string>.Empty)
                    )
                );
            }
            if (next != null && !string.IsNullOrWhiteSpace(next))
            {
                reader.Rewind();
            }

            if (callOuts.Count != contentWithCallouts.CallOutReferences.Count)
            { // todo: enhance
                throw new InvalidOperationException(
                    "Invalid callout references (code markers don't match post-code callouts) in snippet:\n"
                        + snippet
                );
            }

            return new Code(
                Subs(
                    contentWithCallouts.Content,
                    codeOptions ?? ImmutableDictionary<string, string>.Empty,
                    currentAttributes
                ) ?? "",
                callOuts,
                codeOptions ?? ImmutableDictionary<string, string>.Empty,
                false
            );
        }

        private OpenBlock ParseOpenBlock(
            Reader reader,
            IDictionary<string, string>? options,
            IContentResolver? resolver,
            IDictionary<string, string> currentAttributes
        )
        {
            var content = new List<string>();
            string? next;
            while ((next = reader.NextLine()) != null && "--" != next.Trim())
            {
                content.Add(next);
            }
            if (next != null && !next.StartsWith("--"))
            {
                reader.Rewind();
            }
            return new OpenBlock(
                DoParse(new Reader(content), l => true, resolver, currentAttributes, true, false),
                options ?? ImmutableDictionary<string, string>.Empty
            );
        }

        private Quote ParseQuote(
            Reader reader,
            IDictionary<string, string>? options,
            IContentResolver? resolver,
            IDictionary<string, string> currentAttributes
        )
        {
            var content = new List<string>();
            string? next;
            while ((next = reader.NextLine()) != null && next.StartsWith('>'))
            {
                content.Add(next[1..].TrimStart());
            }
            if (next != null && !next.StartsWith("> "))
            {
                reader.Rewind();
            }
            return new Quote(
                DoParse(new Reader(content), l => true, resolver, currentAttributes, true, false),
                options ?? ImmutableDictionary<string, string>.Empty
            );
        }

        // todo: support footer, alignments and spans
        private Table ParseTable(
            Reader reader,
            IDictionary<string, string>? options,
            IContentResolver? resolver,
            IDictionary<string, string> currentAttributes,
            string token
        )
        {
            IList<Func<IList<string>, IElement>> cellParser = (
                options ?? ImmutableDictionary<string, string>.Empty
            ).TryGetValue("cols", out var cols)
                ? cols!
                    .Trim()
                    .Split(',')
                    .Select(it => it.Trim())
                    .Where(it => !string.IsNullOrWhiteSpace(it))
                    .Select(i =>
                    {
                        IDictionary<string, string> options = ImmutableDictionary<
                            string,
                            string
                        >.Empty;
                        if (i.Contains('<'))
                        {
                            options = new Dictionary<string, string>
                            {
                                { "role", "tableblock halign-left valign-top" },
                            };
                        }
                        if (i.Contains('>'))
                        {
                            options = new Dictionary<string, string>
                            {
                                { "role", "tableblock halign-right valign-top" },
                            };
                        }
                        if (i.Contains('^'))
                        {
                            options = new Dictionary<string, string>
                            {
                                { "role", "tableblock halign-center valign-top" },
                            };
                        }
                        return ToTableCellFormatter(i, resolver, currentAttributes, options);
                    })
                    .ToList() ?? []
                : [];

            var rows = new List<IList<IElement>>(4);
            string? next;
            while (token != (next = reader.SkipCommentsAndEmptyLines()) && next is not null)
            {
                next = next.Trim();
                var cells = new List<IElement>();
                if (next.IndexOf('|', 2) > 0) // single line row
                {
                    int cellIdx = 0;
                    int last = 1; // line starts with '|'
                    int nextSep = next.IndexOf('|', last);
                    while (nextSep > 0)
                    {
                        var content = next[last..nextSep];
                        cells.Add(
                            cellParser.Count > cellIdx
                                ? cellParser[cellIdx++]([content])
                                : NewText(null, content.Trim(), null)
                        );
                        last = nextSep + 1;
                        nextSep = next.IndexOf('|', last);
                    }
                    if (last < next.Length)
                    {
                        var end = next[last..];
                        cells.Add(
                            cellParser.Count > cellIdx
                                ? cellParser[cellIdx]([end])
                                : NewText(null, end.Trim(), null)
                        );
                    }
                }
                else // one cell per row
                {
                    int cellIdx = 0;
                    do
                    {
                        var content = new List<string> { next![1..] };
                        while (
                            (next = reader.NextLine()) != null
                            && !next.StartsWith('|')
                            && !string.IsNullOrWhiteSpace(next)
                        )
                        {
                            content.Add(next.Trim());
                        }
                        if (next != null)
                        {
                            reader.Rewind();
                        }

                        cells.Add(
                            cellParser.Count > cellIdx
                                ? cellParser[cellIdx++](content)
                                : NewText(null, string.Join('\n', content).Trim(), null)
                        );
                    } while (
                        (next = reader.NextLine()) != null
                        && !string.IsNullOrWhiteSpace(next)
                        && !next.StartsWith("|===")
                    );
                    if (next != null && next.StartsWith("|"))
                    {
                        reader.Rewind();
                    }
                }
                rows.Add(cells);
            }
            return new Table(rows, options ?? ImmutableDictionary<string, string>.Empty);
        }

        private Func<IList<string>, IElement> ToTableCellFormatter(
            string options,
            IContentResolver? resolver,
            IDictionary<string, string> currentAttributes,
            IDictionary<string, string> style
        )
        {
            if (options.Contains('a'))
            { // asciidoc
                return c =>
                {
                    var content = DoParse(
                        new Reader(c),
                        line => true,
                        resolver,
                        currentAttributes,
                        true,
                        true
                    );
                    if (content.Count == 1)
                    {
                        return content[0];
                    }
                    return new Paragraph(content, style);
                };
            }
            if (options.Contains('e'))
            { // emphasis
                return c => new Text([Text.Styling.Emphasis], string.Join('\n', c), style);
            }
            if (options.Contains('s'))
            { // strong
                return c => new Text([Text.Styling.Bold], string.Join('\n', c), style);
            }
            if (options.Contains('l') || options.Contains('m'))
            { // literal or monospace
                return c =>
                {
                    var elements = string.Join(
                        "",
                        HandleIncludes(string.Join('\n', c), resolver, currentAttributes, true)
                            .Select(e =>
                                e is Text t ? t.Value : e.ToString() /* FIXME */
                            )
                    );
                    return new Code(elements, ImmutableList<CallOut>.Empty, style, true);
                };
            }
            if (options.Contains('h'))
            { // header
                var role = style.TryGetValue("role", out var v) ? v : "";
                return c =>
                    NewText(
                        null,
                        string.Join('\n', c),
                        new Dictionary<string, string> { { "role", $"header {role}".TrimEnd() } }
                    );
            }
            // contains("d") == default
            return c =>
            {
                var content = DoParse(
                    new Reader(c),
                    line => true,
                    resolver,
                    currentAttributes,
                    false,
                    true
                );
                if (content.Count == 1)
                {
                    return content[0];
                }
                return new Paragraph(content, style);
            };
        }

        private IList<IElement> DoParse(
            Reader reader,
            Predicate<string?> continueTest,
            IContentResolver? resolver,
            IDictionary<string, string> attributes,
            bool supportComplexStructures,
            bool canBeTitle
        )
        {
            var elements = new List<IElement>(8);
            string? next;

            int lastOptions = -1;
            IDictionary<string, string>? options = null;
            Match attributeMatcher;
            while ((next = reader.SkipCommentsAndEmptyLines()) != null)
            {
                if (!continueTest(next))
                {
                    reader.Rewind();
                    if (lastOptions == reader.LineNumber)
                    {
                        reader.Rewind();
                    }
                    break;
                }

                var newValue = EarlyAttributeReplacement(next, false, attributes);
                if (newValue != next)
                {
                    reader.SetPreviousValue(newValue);
                }

                var stripped = next.Trim();
                if (stripped.StartsWith('[') && stripped.EndsWith(']'))
                {
                    if ("[abstract]" == stripped)
                    { // not sure this was a great idea, just consider it a role for now
                        options = Merge(
                            options,
                            new Dictionary<string, string> { { "role", "abstract" } }
                        );
                    }
                    else
                    {
                        options = Merge(options, ParseOptions(next[1..^1]));
                        lastOptions = reader.LineNumber;
                    }
                }
                else if ("...." == stripped)
                {
                    elements.Add(
                        new Listing(
                            ParsePassthrough(
                                reader,
                                attributes,
                                options ?? ImmutableDictionary<string, string>.Empty,
                                "....",
                                resolver
                            ).Value,
                            options ?? ImmutableDictionary<string, string>.Empty
                        )
                    );
                    options = null;
                }
                else if (
                    canBeTitle
                    && next.StartsWith('.')
                    && !next.StartsWith("..")
                    && !next.StartsWith(". ")
                )
                {
                    options = Merge(
                        options,
                        new Dictionary<string, string> { { "title", next[1..].Trim() } }
                    );
                }
                else if (next.StartsWith('='))
                {
                    reader.Rewind();
                    elements.Add(
                        ParseSection(
                            reader,
                            options ?? ImmutableDictionary<string, string>.Empty,
                            resolver,
                            attributes
                        )
                    );
                    options = null;
                }
                else if ("----" == stripped)
                {
                    elements.Add(ParseCodeBlock(reader, options, resolver, attributes, "----"));
                    options = null;
                }
                else if ("```" == stripped)
                {
                    elements.Add(ParseCodeBlock(reader, options, resolver, attributes, "```"));
                    options = null;
                }
                else if ("--" == stripped)
                {
                    elements.Add(ParseOpenBlock(reader, options, resolver, attributes));
                    options = null;
                }
                else if (stripped.StartsWith("|==="))
                {
                    elements.Add(ParseTable(reader, options, resolver, attributes, stripped));
                    options = null;
                }
                else if ("++++" == stripped)
                {
                    elements.Add(ParsePassthrough(reader, attributes, options, "++++", resolver));
                    options = null;
                }
                else if ("<<<" == stripped)
                {
                    elements.Add(
                        new PageBreak(options ?? ImmutableDictionary<string, string>.Empty)
                    );
                    options = null;
                }
                else if (stripped.StartsWith("> "))
                {
                    reader.Rewind();
                    elements.Add(ParseQuote(reader, options, resolver, attributes));
                    options = null;
                }
                else if (stripped.StartsWith("____"))
                {
                    var buffer = new List<string>();
                    while ((next = reader.NextLine()) != null && "____" != next.Trim())
                    {
                        buffer.Add(next);
                    }
                    elements.Add(
                        new Quote(
                            DoParse(
                                new Reader(buffer),
                                l => true,
                                resolver,
                                attributes,
                                supportComplexStructures,
                                false
                            ),
                            options ?? ImmutableDictionary<string, string>.Empty
                        )
                    );
                    options = null;
                }
                else if (
                    stripped.StartsWith(':')
                    && (attributeMatcher = AttributeDefinitionRegex().Match(stripped)).Success
                )
                {
                    var value = attributeMatcher.Groups.TryGetValue("value", out var v)
                        ? v.Value ?? ""
                        : "";
                    var name = attributeMatcher.Groups["name"].Value;
                    if (
                        (value.StartsWith("+") || value.StartsWith('-'))
                        && attributes is Dictionary<string, string> d
                        && d.ContainsValue(name)
                    )
                    { // offset
                        try
                        {
                            attributes.Add(
                                name,
                                (int.Parse(attributes[name]) + int.Parse(value)).ToString()
                            );
                        }
                        catch (Exception)
                        { // not a number mainly
                            attributes.Add(name, value);
                        }
                    }
                    else
                    {
                        attributes.Add(name, value);
                    }
                }
                else
                {
                    reader.Rewind();
                    var element = UnwrapElementIfPossible(
                        ParseParagraph(
                            reader,
                            options,
                            resolver,
                            attributes,
                            supportComplexStructures
                        )
                    );
                    if (
                        element is Paragraph { Options.Count: 0 } p
                        && p.Children.Any(it => it.Type() == IElement.ElementType.Section)
                    )
                    {
                        elements.AddRange(p.Children);
                    }
                    else
                    {
                        elements.Add(element);
                    }

                    options = null;
                }
            }
            return elements.Where(it => !(it is Paragraph p && p.Children.Count == 0)).ToList();
        }

        private Header ParseHeader(Reader reader, IContentResolver? resolver)
        {
            var firstLine = reader.SkipCommentsAndEmptyLines();
            if (firstLine is null)
            {
                reader.Reset();
                return NoHeader;
            }

            string title;
            if (firstLine.StartsWith("= ") || firstLine.StartsWith("# "))
            {
                title = firstLine[2..].Trim();
            }
            else
            {
                reader.Reset();
                return NoHeader;
            }

            var author = NoAuthor;
            var revision = NoRevision;

            var authorLine = reader.NextLine();
            if (
                authorLine is not null
                && !string.IsNullOrWhiteSpace(authorLine)
                && !reader.IsComment(authorLine)
                && CanBeHeaderLine(authorLine)
            )
            {
                if (
                    !authorLine.StartsWith("include:")
                    && !AttributeDefinitionRegex().Match(authorLine).Success
                )
                { // author line
                    author = ParseAuthorLine(authorLine);

                    var revisionLine = reader.NextLine();
                    if (
                        revisionLine != null
                        && !string.IsNullOrEmpty(revisionLine)
                        && !reader.IsComment(revisionLine)
                        && CanBeHeaderLine(revisionLine)
                    )
                    {
                        if (
                            !authorLine.StartsWith("include:")
                            && !AttributeDefinitionRegex().Match(revisionLine).Success
                        )
                        { // author line
                            revision = ParseRevisionLine(revisionLine);
                        }
                        else
                        {
                            reader.Rewind();
                        }
                    }
                }
                else
                {
                    reader.Rewind();
                }
            }

            var attributes = ReadAttributes(reader, resolver);
            return new Header(title, author, revision, attributes);
        }

        // name <mail>
        private Author ParseAuthorLine(string authorLine)
        {
            int mailStart = authorLine.LastIndexOf('<');
            if (mailStart > 0)
            {
                int mailEnd = authorLine.IndexOf('>', mailStart);
                if (mailEnd > 0)
                {
                    return new Author(
                        authorLine[..mailStart].Trim(),
                        authorLine[(mailStart + 1)..mailEnd].Trim()
                    );
                }
            }
            return new Author(authorLine.Trim(), "");
        }

        // revision number, revision date: revision revmark
        private Revision ParseRevisionLine(string revisionLine)
        {
            int firstSep = revisionLine.IndexOf(",");
            int secondSep = revisionLine.IndexOf(":");
            if (firstSep < 0 && secondSep < 0)
            {
                return new Revision(revisionLine.Trim(), "", "");
            }
            if (firstSep > 0 && secondSep < 0)
            {
                return new Revision(
                    revisionLine[..firstSep].Trim(),
                    revisionLine[(firstSep + 1)..].Trim(),
                    ""
                );
            }
            return new Revision(
                revisionLine[..firstSep].Trim(),
                revisionLine[(firstSep + 1)..secondSep].Trim(),
                revisionLine[(secondSep + 1)..].Trim()
            );
        }

        private bool CanBeHeaderLine(string line)
        { // ideally shouldn't be needed and an empty line should be required between title and "content"
            return !(
                line.StartsWith("* ")
                || line.StartsWith("=")
                || line.StartsWith("[")
                || line.StartsWith(".")
                || line.StartsWith("<<")
                || line.StartsWith("--")
                || line.StartsWith("``")
                || line.StartsWith("..")
                || line.StartsWith("++")
                || line.StartsWith("|==")
                || line.StartsWith("> ")
                || line.StartsWith("__")
            );
        }

        private IList<string> ReadIfBlock(Reader reader)
        { // todo: support nested
            var buffer = new List<string>();
            string? next;
            int remaining = 1;
            while (
                (next = reader.NextLine()) != null
                && ("endif::[]" != next.Trim() || --remaining > 0)
            )
            {
                buffer.Add(next);
                if (
                    next.StartsWith("ifndef::")
                    || next.StartsWith("ifdef::")
                    || next.StartsWith("ifeval::")
                )
                {
                    remaining++;
                }
            }
            return buffer;
        }

        private IDictionary<string, string> ReadAttributes(
            Reader reader,
            IContentResolver? resolver
        )
        {
            var attributes = new SortedDictionary<string, string>();
            string? line;
            while ((line = reader.NextLine()) is not null && !string.IsNullOrWhiteSpace(line))
            {
                var matcher = AttributeDefinitionRegex().Match(line);
                if (matcher.Success)
                {
                    var value = matcher.Groups.TryGetValue("value", out var v)
                        ? v.Value.Trim()
                        : "";
                    while (value.EndsWith('\\'))
                    {
                        value = value[0..^1].Trim();
                        var next = reader.NextLine();
                        if (next is not null)
                        {
                            value = value + ' ' + next.Trim();
                        }
                    }
                    attributes.Add(matcher.Groups["name"].Value, value);
                }
                else if (line.StartsWith("include::") && line.TrimEnd().EndsWith(']'))
                {
                    var optsStart = line.LastIndexOf('[');
                    var text = DoInclude(
                        new Macro(
                            "include",
                            EarlyAttributeReplacement(
                                line["include::".Length..optsStart],
                                true,
                                ImmutableDictionary<string, string>.Empty
                            ),
                            ParseOptions(line[(optsStart + 1)..line.LastIndexOf(']')]),
                            false
                        ),
                        resolver,
                        attributes,
                        false
                    );
                    if (text.Count == 1 && text[0] is Text t)
                    {
                        foreach (
                            var it in ReadAttributes(new Reader(t.Value.Split('\n')), resolver)
                        )
                        {
                            attributes[it.Key] = it.Value;
                        }
                    }
                }
                else
                {
                    // simplistic macro handling, mainly for conditional blocks since we still are in headers
                    var stripped = line.Trim();
                    int options = stripped.IndexOf("[]");
                    if (stripped.Length - "[]".Length == options)
                    { // endsWith
                        int sep = stripped.IndexOf("::");
                        if (sep > 0)
                        {
                            var macro = new Macro(
                                stripped[..sep],
                                stripped[(sep + "::".Length)..options],
                                ImmutableDictionary<string, string>.Empty,
                                false
                            );
                            if (
                                "ifdef" == macro.Name
                                || "ifndef" == macro.Name
                                || "ifeval" == macro.Name
                            )
                            {
                                var block = ReadIfBlock(reader);

                                var ctx = new DictionariesContext(attributes, globalAttributes);
                                if (
                                    macro.Name switch
                                    {
                                        "ifdef" => new ConditionalBlock.Ifdef(macro.Label).Test(
                                            ctx
                                        ),
                                        "ifndef" => new ConditionalBlock.Ifndef(macro.Label).Test(
                                            ctx
                                        ),
                                        "ifeval" => new ConditionalBlock.Ifeval(
                                            ParseCondition(macro.Label.Trim(), attributes)
                                        ).Test(ctx),
                                        _ => false, // not possible
                                    }
                                )
                                {
                                    reader.Insert(block);
                                }
                                continue;
                            }
                        }
                    }

                    if (attributes.Count == 0) // direct content (weird but accepted)
                    {
                        reader.Rewind();
                        break;
                    }

                    // missing empty line separator
                    throw new InvalidOperationException($"Unknown line: '{line}'");
                }
            }
            return attributes;
        }

        private Predicate<ConditionalBlock.IContext> ParseCondition(
            string condition,
            IDictionary<string, string> attributeAtParsingTime
        )
        {
            int sep1 = condition.IndexOf(' ');
            if (sep1 < 0)
            {
                throw new InvalidOperationException("Unknown expression: '" + condition + "'");
            }
            int sep2 = condition.LastIndexOf(' ');
            if (sep2 < 0 || sep2 == sep1)
            {
                throw new InvalidOperationException("Unknown expression: '" + condition + "'");
            }

            var leftOperand = StripQuotes(condition[..sep1].Trim());
            var operatorType = condition[(sep1 + 1)..sep2].Trim();
            var rightOperand = StripQuotes(condition[sep2..].Trim());
            IDictionary<string, string> parsingAttributes =
                attributeAtParsingTime.Count > 0
                    ? new Dictionary<string, string>(attributeAtParsingTime)
                    : ImmutableDictionary<string, string>.Empty;
            Func<ConditionalBlock.IContext, Func<string, string?>> attributeAccessor = ctx =>
                // ensure levels and implicit attributes are well evaluated
                key => parsingAttributes.TryGetValue(key, out var v) ? v : ctx.Attribute(key);
            return operatorType switch
            {
                "==" => context =>
                    Eval(
                        leftOperand,
                        rightOperand,
                        attributeAccessor(context),
                        it => it.Item1 == it.Item2
                    ),
                "!=" => context =>
                    !Eval(
                        leftOperand,
                        rightOperand,
                        attributeAccessor(context),
                        it => it.Item1 == it.Item2
                    ),
                "<" => context =>
                    EvalNumbers(leftOperand, rightOperand, context, it => it.Item1 < it.Item2),
                "<=" => context =>
                {
                    var attributes = attributeAccessor(context);
                    return double.Parse(EarlyAttributeReplacement(leftOperand, attributes, true))
                        <= double.Parse(EarlyAttributeReplacement(rightOperand, attributes, true));
                },
                ">" => context =>
                {
                    var attributes = attributeAccessor(context);
                    return double.Parse(EarlyAttributeReplacement(leftOperand, attributes, true))
                        > double.Parse(EarlyAttributeReplacement(rightOperand, attributes, true));
                },
                ">=" => context =>
                {
                    var attributes = attributeAccessor(context);
                    return double.Parse(EarlyAttributeReplacement(leftOperand, attributes, true))
                        >= double.Parse(EarlyAttributeReplacement(rightOperand, attributes, true));
                },
                _ => throw new InvalidOperationException($"Unknown operator '{operatorType}'"),
            };
        }

        private string StripQuotes(string strip)
        {
            return strip.StartsWith('\"') && strip.EndsWith('\"') && strip.Length > 1
                ? strip[1..^1]
                : strip;
        }

        private bool Eval(
            string leftOperand,
            string rightOperand,
            Func<string, string?> context,
            Predicate<(string, string)> test
        )
        {
            return test(
                (
                    EarlyAttributeReplacement(leftOperand, context, true),
                    EarlyAttributeReplacement(rightOperand, context, true)
                )
            );
        }

        private bool EvalNumbers(
            string leftOperand,
            string rightOperand,
            ConditionalBlock.IContext context,
            Predicate<(double, double)> test
        )
        {
            return test(
                (
                    double.Parse(EarlyAttributeReplacement(leftOperand, context.Attribute, true)),
                    double.Parse(EarlyAttributeReplacement(rightOperand, context.Attribute, true))
                )
            );
        }

        private string EarlyAttributeReplacement(
            string value,
            bool unescape,
            params IDictionary<string, string>[] attributes
        )
        {
            return EarlyAttributeReplacement(
                value,
                k =>
                    attributes
                        .Where(it => it.ContainsKey(k))
                        .Select(it => it.TryGetValue(k, out var v) ? v : null)
                        .FirstOrDefault(),
                unescape
            );
        }

        private string EarlyAttributeReplacement(
            string value,
            Func<string, string?> attributes,
            bool unescape
        )
        {
            if (!value.Contains('{'))
            {
                return value;
            }

            HashSet<string>? keys = null;
            var from = 0;
            do
            {
                from = value.IndexOf('{', from);
                if (from < 0)
                {
                    break;
                }

                var to = from + 1;
                while (to < value.Length)
                {
                    if (value[to] == '}')
                    {
                        var name = value[(from + 1)..to];
                        if (attributes(name) != null || globalAttributes.ContainsKey(name))
                        {
                            keys ??= new HashSet<string>(1);
                            keys.Add(name);
                        }
                    }

                    if (value[to] != '}' && value[to] != ' ')
                    {
                        to++;
                        continue;
                    }

                    from = to;
                    break;
                }
            } while (from > 0);

            if (keys is null)
            {
                return value;
            }

            var result = value;
            foreach (var key in keys)
            {
                var placeholder = '{' + key + '}';
                var replacement =
                    attributes(key)
                    ?? (globalAttributes.TryGetValue(key, out var gav) ? gav : placeholder);

                var start = 0;
                while (start < result.Length)
                {
                    var next = result.IndexOf(placeholder, start, StringComparison.Ordinal);
                    if (next < 0)
                    {
                        break;
                    }

                    if (next > 0 && result[next - 1] == '\\')
                    {
                        if (unescape)
                        {
                            result = result[..(next - 1)] + result[next..]; // drop escaping
                        }
                        start = next + 1;
                        continue;
                    }

                    result = result[..next] + replacement + result[(next + placeholder.Length)..];
                    start = next + placeholder.Length;
                }
            }
            return result;
        }
    }

    public record ParserContext(IContentResolver Resolver) { }

    public class CustomContentResolver(Func<string, Encoding?, IEnumerable<string>?> resolve)
        : IContentResolver
    {
        public IEnumerable<string>? Resolve(string reference, Encoding? encoding)
        {
            return resolve(reference, encoding);
        }
    }

    internal class DictionariesContext(
        IDictionary<string, string> first,
        IDictionary<string, string> second
    ) : ConditionalBlock.IContext
    {
        public string? Attribute(string key)
        {
            return first.TryGetValue(key, out var v1)
                ? v1
                : (second.TryGetValue(key, out var v2) ? v2 : null);
        }
    };

    internal record ContentWithCalloutIndices(string Content, IList<int> CallOutReferences) { }
}
