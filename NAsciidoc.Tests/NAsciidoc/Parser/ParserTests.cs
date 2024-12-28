using System.Collections.Immutable;
using NAsciidoc.Model;

namespace NAsciidoc.Parser;

public class ParserTests
{
    [Fact]
    public void ParseHeader()
    {
        var header = new Parser()
            .Parse(new Reader(["= Title", ":attr-1: v1", ":attr-2: v2", "", "content"]), null)
            .Header;
        Assert.Equal("Title", header.Title);
        Assert.Equal(
            new Dictionary<string, string> { { "attr-1", "v1" }, { "attr-2", "v2" } },
            header.Attributes
        );
    }
    
    [Fact] // check for https://github.com/yupiik/tools-maven-plugin/issues/21
    public void ParseIndentedCode()
    {
        var body = new Parser().ParseBody(
            new Reader(
                // intentation is intended
                """
                        [source,xml]
                        ----
                        <dependency>
                            <groupId>io.quarkiverse.qute.web</groupId>
                            <artifactId>quarkus-qute-web</artifactId>
                        </dependency>
                        ----
                    """.Split('\n')
            )
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Code(
                    "    <dependency>\n        <groupId>io.quarkiverse.qute.web</groupId>\n        <artifactId>quarkus-qute-web</artifactId>\n    </dependency>\n",
                    ImmutableList<CallOut>.Empty,
                    ImmutableDictionary<string, string>.Empty,
                    false
                ),
            },
            body.Children
        );
    }

    // ensure we can use custom extension blocks implementable in a custom visitor decorator
    // here we use mermaid which enables to use mermaid in js mode to render the block content
    // TIP: it can be done at build time if you accept to bring back node stack
    [Fact]
    public void CustomBlockType()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                    [mermaid]
                    ....
                    foo
                    bar
                    ....
                    """.Replace("\r\n", "\n").Split('\n')
            )
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Listing(
                    "foo\nbar",
                    new Dictionary<string, string> { { "", "mermaid" } }
                ),
            },
            body.Children
        );
    }

    [Fact]
    public void ParseHeaderWithConditionalBlocks()
    {
        var content = """
            = Title
            :idprefix:
            :idseparator: -
            ifndef::env-github[]
            :toc: left
            :icons: font
            endif::[]
            ifdef::env-github[]
            :toc: macro
            :caution-caption: :fire:
            :important-caption: :exclamation:
            :note-caption: :paperclip:
            :tip-caption: :bulb:
            :warning-caption: :warning:
            endif::[]
            """.Replace("\r\n", "\n");
        {
            var header = new Parser().Parse(content, null).Header;
            Assert.Equal("Title", header.Title);
            Assert.Equal(
                new Dictionary<string, string>
                {
                    { "idprefix", "" },
                    { "idseparator", "-" },
                    { "toc", "left" },
                    { "icons", "font" }
                },
                header.Attributes
            );
        }
        {
            var header = new Parser(new Dictionary<string, string> { { "env-github", "true" } })
                .Parse(content, null)
                .Header;
            Assert.Equal("Title", header.Title);
            Assert.Equal(
                new Dictionary<string, string>
                {
                    { "idprefix", "" },
                    { "idseparator", "-" },
                    { "toc", "macro" },
                    { "caution-caption", ":fire:" },
                    { "important-caption", ":exclamation:" },
                    { "note-caption", ":paperclip:" },
                    { "tip-caption", ":bulb:" },
                    { "warning-caption", ":warning:" }
                },
                header.Attributes
            );
        }
    }

    [Fact]
    public void ParseHeaderWhenMissing()
    {
        var header = new Parser().Parse("paragraph").Header;
        Assert.Equal("", header.Title);
        Assert.Equal(ImmutableDictionary<string, string>.Empty, header.Attributes);
    }

    [Fact]
    public void ParseMultiLineAttributesHeader()
    {
        var header = new Parser()
            .Parse("= Title\n:attr-1: v1\n:attr-2: v2\\\n  and it continues\n\ncontent")
            .Header;
        Assert.Equal("Title", header.Title);
        Assert.Equal(
            new Dictionary<string, string>
            {
                { "attr-1", "v1" },
                { "attr-2", "v2 and it continues" }
            },
            header.Attributes
        );
    }

    [Fact]
    public void ParseAuthorLine()
    {
        var header = new Parser()
            .Parse(
                [
                    "= Title",
                    "firstname middlename lastname <email>",
                    "revision number, revision date: revision revmark",
                    ":attr: value"
                ]
            )
            .Header;
        Assert.Equal("Title", header.Title);
        Assert.Equal(new Author("firstname middlename lastname", "email"), header.Author);
        Assert.Equal(
            new Revision("revision number", "revision date", "revision revmark"),
            header.Revision
        );
        Assert.Equal(new Dictionary<string, string> { { "attr", "value" } }, header.Attributes);
    }

    [Fact]
    public void DefinitionList()
    {
        var body = new Parser()
            .Parse(
                new Reader(
                    """
                    generate-frisby-skeleton.output (env: `GENERATE_FRISBY_SKELETON_OUTPUT`)::
                    Where to generate the skeleton. Default: `hcms-frisby`.
                    hcms.database-init.enabled (env: `HCMS_DATABASE_INIT_ENABLED`)::
                    Should database be initialized at startup. Default: `true`.
                    +
                    multiline
                    """.Replace("\r\n", "\n").Split("\n")
                )
            )
            .Body;
        Assert.Collection(
            body.Children,
            e =>
                Assert.Multiple(() =>
                {
                    var expected = new DescriptionList(
                        // todo: here we dont check the order is ok
                        new Dictionary<IElement, IElement>
                        {
                            {
                                new Paragraph(
                                    [
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            "generate-frisby-skeleton.output (env: ",
                                            ImmutableDictionary<string, string>.Empty
                                        ),
                                        new Code(
                                            "GENERATE_FRISBY_SKELETON_OUTPUT",
                                            ImmutableList<CallOut>.Empty,
                                            ImmutableDictionary<string, string>.Empty,
                                            true
                                        ),
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            ")",
                                            ImmutableDictionary<string, string>.Empty
                                        )
                                    ],
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new Paragraph(
                                    [
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            "Where to generate the skeleton. Default: ",
                                            ImmutableDictionary<string, string>.Empty
                                        ),
                                        new Code(
                                            "hcms-frisby",
                                            ImmutableList<CallOut>.Empty,
                                            ImmutableDictionary<string, string>.Empty,
                                            true
                                        ),
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            ".",
                                            ImmutableDictionary<string, string>.Empty
                                        )
                                    ],
                                    ImmutableDictionary<string, string>.Empty
                                )
                            },
                            {
                                new Paragraph(
                                    [
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            "hcms.database-init.enabled (env: ",
                                            ImmutableDictionary<string, string>.Empty
                                        ),
                                        new Code(
                                            "HCMS_DATABASE_INIT_ENABLED",
                                            ImmutableList<CallOut>.Empty,
                                            ImmutableDictionary<string, string>.Empty,
                                            true
                                        ),
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            ")",
                                            ImmutableDictionary<string, string>.Empty
                                        )
                                    ],
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new Paragraph(
                                    [
                                        new Paragraph(
                                            [
                                                new Text(
                                                    ImmutableList<Text.Styling>.Empty,
                                                    "Should database be initialized at startup. Default: ",
                                                    ImmutableDictionary<string, string>.Empty
                                                ),
                                                new Code(
                                                    "true",
                                                    ImmutableList<CallOut>.Empty,
                                                    ImmutableDictionary<string, string>.Empty,
                                                    true
                                                ),
                                                new Text(
                                                    ImmutableList<Text.Styling>.Empty,
                                                    ".",
                                                    ImmutableDictionary<string, string>.Empty
                                                )
                                            ],
                                            ImmutableDictionary<string, string>.Empty
                                        ),
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            "multiline",
                                            ImmutableDictionary<string, string>.Empty
                                        )
                                    ],
                                    ImmutableDictionary<string, string>.Empty
                                )
                            }
                        },
                        ImmutableDictionary<string, string>.Empty
                    );

                    Assert.IsType<DescriptionList>(e);
                    Assert.Equivalent(expected, e);
                })
        );
    }

    [Fact]
    public void ParseHeaderAndContent()
    {
        var doc = new Parser().Parse(["= Title", "", "++++", "pass", "++++"], null);
        Assert.Multiple(() =>
        {
            Assert.Equal("Title", doc.Header.Title);
            Assert.Equal(ImmutableDictionary<string, string>.Empty, doc.Header.Attributes);
            Assert.Equivalent(
                new List<IElement>
                {
                    new PassthroughBlock("pass", ImmutableDictionary<string, string>.Empty)
                },
                doc.Body.Children
            );
        });
    }

    [Fact]
    public void ParseParagraph()
    {
        var body = new Parser()
            .Parse(
                new Reader(
                    """
                    Mark my words, #automation is essential#.
                                    
                    ##Mark##up refers to value that contains formatting ##mark##s.
                                    
                    Where did all the [.underline]#cores# go?
                                    
                    We need [.line-through]#ten# twenty VMs.
                                    
                    A [.myrole]#custom role# must be fulfilled by the theme.
                    """.Replace("\r\n", "\n").Split("\n")
                ),
                null
            )
            .Body;
        Assert.Equivalent(
            new List<IElement>
            {
                new Paragraph(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "Mark my words, ",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            [Text.Styling.Mark],
                            "automation is essential",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            ".",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                ),
                new Paragraph(
                    [
                        new Text(
                            [Text.Styling.Mark],
                            "Mark",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "up refers to value that contains formatting ",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            [Text.Styling.Mark],
                            "mark",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "s.",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                ),
                new Paragraph(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "Where did all the ",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            [Text.Styling.Mark],
                            "cores",
                            new Dictionary<string, string> { { "role", "underline" } }
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            " go?",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                ),
                new Paragraph(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "We need ",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            [Text.Styling.Mark],
                            "ten",
                            new Dictionary<string, string> { { "role", "line-through" } }
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            " twenty VMs.",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                ),
                new Paragraph(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "A ",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            [Text.Styling.Mark],
                            "custom role",
                            new Dictionary<string, string> { { "role", "myrole" } }
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            " must be fulfilled by the theme.",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void ParseParagraphMultiline()
    {
        var body = new Parser()
            .Parse(
                new Reader(
                    """
                    Mark my words, #automation is essential#.
                                    
                    ##Mark##up refers to value that contains formatting ##mark##s.
                    Where did all the [.underline]#cores# go?
                                    
                    end.
                    """.Replace("\r\n", "\n").Split('\n')
                ),
                null
            )
            .Body;
        Assert.Equivalent(
            new List<IElement>
            {
                new Paragraph(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "Mark my words, ",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            [Text.Styling.Mark],
                            "automation is essential",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            ".",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                ),
                new Paragraph(
                    [
                        new Text(
                            [Text.Styling.Mark],
                            "Mark",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "up refers to value that contains formatting ",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            [Text.Styling.Mark],
                            "mark",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "s. Where did all the ",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            [Text.Styling.Mark],
                            "cores",
                            new Dictionary<string, string> { { "role", "underline" } }
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            " go?",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                ),
                new Text(
                    ImmutableList<Text.Styling>.Empty,
                    "end.",
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void Links()
    {
        var body = new Parser()
            .Parse(
                new Reader(
                    """
                    https://yupiik.io[Yupiik OSS,role=external,window=_blank]
                                    
                    This can be in a sentence about https://yupiik.io[Yupiik OSS].
                    """.Replace("\r\n", "\n").Split('\n')
                ),
                null
            )
            .Body;
        Assert.Equivalent(
            new List<IElement>
            {
                new Link(
                    "https://yupiik.io",
                    "Yupiik OSS",
                    new Dictionary<string, string>
                    {
                        { "role", "external" },
                        { "window", "_blank" }
                    }
                ),
                new Paragraph(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "This can be in a sentence about ",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Link(
                            "https://yupiik.io",
                            "Yupiik OSS",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            ".",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void LinkNoOpt()
    {
        Assert.Equivalent(
            new List<IElement>
            {
                new Link(
                    "https://yupiik.io",
                    "https://yupiik.io",
                    ImmutableDictionary<string, string>.Empty
                )
            },
            new Parser().Parse(new Reader(["https://yupiik.io"]), null).Body.Children
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Paragraph(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "in a sentence ",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Link(
                            "https://yupiik.io",
                            "https://yupiik.io",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            " and multiple ",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Link(
                            "https://www.yupiik.io",
                            "https://www.yupiik.io",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            " links.",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            new Parser()
                .Parse(
                    new Reader(
                        [
                            "in a sentence https://yupiik.io and multiple https://www.yupiik.io links."
                        ]
                    ),
                    null
                )
                .Body.Children
        );
    }

    [Fact]
    public void LinkInCode()
    {
        var body = new Parser().Parse(new Reader(["`https://yupiik.io[Yupiik OSS]`"]), null).Body;
        Assert.Equivalent(
            new List<IElement>
            {
                new Link(
                    "https://yupiik.io",
                    "Yupiik OSS",
                    new Dictionary<string, string> { { "role", "inline-code" } }
                )
            },
            body.Children
        );
    }

    [Fact]
    public void LinkMacroWithRole()
    {
        Assert.Equivalent(
            new List<IElement>
            {
                new Macro(
                    "link",
                    "foo",
                    new Dictionary<string, string> { { "role", "test" } },
                    true
                )
            },
            new Parser().Parse(new Reader(["link:foo[role=\"test\"]"]), null).Body.Children
        );
    }

    [Fact]
    public void LinksAttribute()
    {
        var body = new Parser()
            .Parse(new Reader([":url: https://yupiik.io", "", "{url}[Yupiik OSS]"]), null)
            .Body;
        Assert.Equivalent(
            new List<IElement>
            {
                new Link(
                    "https://yupiik.io",
                    "Yupiik OSS",
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void ParseParagraphAndSections()
    {
        var body = new Parser()
            .Parse(
                new Reader(
                    """
                    == Section #1
                                    
                    ##Mark##up refers to value that contains formatting ##mark##s.
                    Where did all the [.underline]#cores# go?
                                    
                    == Section #2
                                    
                    Something key.
                    """.Replace("\r\n", "\n").Split('\n')
                ),
                null
            )
            .Body;
        Assert.Equivalent(
            new List<IElement>
            {
                new Section(
                    2,
                    new Text(
                        ImmutableList<Text.Styling>.Empty,
                        "Section #1",
                        ImmutableDictionary<string, string>.Empty
                    ),
                    [
                        new Paragraph(
                            [
                                new Text(
                                    [Text.Styling.Mark],
                                    "Mark",
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "up refers to value that contains formatting ",
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new Text(
                                    [Text.Styling.Mark],
                                    "mark",
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "s. Where did all the ",
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new Text(
                                    [Text.Styling.Mark],
                                    "cores",
                                    new Dictionary<string, string> { { "role", "underline" } }
                                ),
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    " go?",
                                    ImmutableDictionary<string, string>.Empty
                                )
                            ],
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                ),
                new Section(
                    2,
                    new Text(
                        ImmutableList<Text.Styling>.Empty,
                        "Section #2",
                        ImmutableDictionary<string, string>.Empty
                    ),
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "Something key.",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void Options()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                [.first]
                = Section #1
                                
                [.second]
                == Section #2
                                
                [.center]
                Something key.
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Section(
                    1,
                    new Text(
                        ImmutableList<Text.Styling>.Empty,
                        "Section #1",
                        ImmutableDictionary<string, string>.Empty
                    ),
                    [
                        new Section(
                            2,
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Section #2",
                                ImmutableDictionary<string, string>.Empty
                            ),
                            [
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "Something key.",
                                    new Dictionary<string, string> { { "role", "center" } }
                                )
                            ],
                            new Dictionary<string, string> { { "role", "second" } }
                        )
                    ],
                    new Dictionary<string, string> { { "role", "first" } }
                )
            },
            body.Children
        );
    }

    [Fact]
    public void ColonInTitle()
    {
        var body = new Parser().ParseBody(new Reader(["== foo :: bar"]), null);
        Assert.Equivalent(
            new List<IElement>
            {
                new Section(
                    2,
                    new Text(
                        ImmutableList<Text.Styling>.Empty,
                        "foo :: bar",
                        ImmutableDictionary<string, string>.Empty
                    ),
                    ImmutableList<IElement>.Empty,
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void PlusInList()
    {
        var body = new Parser().ParseBody(new Reader(["* foo++"]), null);
        Assert.Equivalent(
            new List<IElement>
            {
                new UnOrderedList(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "foo++",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void DotInList()
    {
        var body = new Parser().ParseBody(new Reader(["* .NET is a framework"]), null);
        Assert.Equivalent(
            new List<IElement>
            {
                new UnOrderedList(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            ".NET is a framework",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void LeadingDots()
    {
        var body = new Parser().ParseBody(new Reader(["... foobar"]), null);
        Assert.Equivalent(
            new List<IElement>
            {
                new Text(
                    ImmutableList<Text.Styling>.Empty,
                    "... foobar",
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void DataAttributes()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                [.step,data-foo=bar,data-dummy="true"]
                == Section #1
                                
                first
                                
                [.step,data-foo=bar2,data-dummy="true"]
                == Section #2
                                
                === Nested section
                                
                Something key.
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Section(
                    2,
                    new Text(
                        ImmutableList<Text.Styling>.Empty,
                        "Section #1",
                        ImmutableDictionary<string, string>.Empty
                    ),
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "first",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    new Dictionary<string, string>
                    {
                        { "data-dummy", "true" },
                        { "data-foo", "bar" },
                        { "role", "step" }
                    }
                ),
                new Section(
                    2,
                    new Text(
                        ImmutableList<Text.Styling>.Empty,
                        "Section #2",
                        ImmutableDictionary<string, string>.Empty
                    ),
                    [
                        new Section(
                            3,
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Nested section",
                                ImmutableDictionary<string, string>.Empty
                            ),
                            [
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "Something key.",
                                    ImmutableDictionary<string, string>.Empty
                                )
                            ],
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    new Dictionary<string, string>
                    {
                        { "role", "step" },
                        { "data-dummy", "true" },
                        { "data-foo", "bar2" }
                    }
                )
            },
            body.Children
        );
    }

    [Fact]
    public void ParseParagraphAndSectionsAndSubsections()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                == Section #1
                                
                first
                                
                == Section #2
                                
                === Nested section
                                
                Something key.
                                
                ==== And it can
                                
                go far
                                
                === Another nested section
                                
                === Even without content
                                
                yes
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Section(
                    2,
                    new Text(
                        ImmutableList<Text.Styling>.Empty,
                        "Section #1",
                        ImmutableDictionary<string, string>.Empty
                    ),
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "first",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                ),
                new Section(
                    2,
                    new Text(
                        ImmutableList<Text.Styling>.Empty,
                        "Section #2",
                        ImmutableDictionary<string, string>.Empty
                    ),
                    [
                        new Section(
                            3,
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Nested section",
                                ImmutableDictionary<string, string>.Empty
                            ),
                            [
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "Something key.",
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new Section(
                                    4,
                                    new Text(
                                        ImmutableList<Text.Styling>.Empty,
                                        "And it can",
                                        ImmutableDictionary<string, string>.Empty
                                    ),
                                    [
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            "go far",
                                            ImmutableDictionary<string, string>.Empty
                                        )
                                    ],
                                    ImmutableDictionary<string, string>.Empty
                                )
                            ],
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Section(
                            3,
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Another nested section",
                                ImmutableDictionary<string, string>.Empty
                            ),
                            ImmutableList<IElement>.Empty,
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Section(
                            3,
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Even without content",
                                ImmutableDictionary<string, string>.Empty
                            ),
                            [
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "yes",
                                    ImmutableDictionary<string, string>.Empty
                                )
                            ],
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void Code()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                [source,java,.hljs]
                ----
                public record Foo() {
                }
                ----
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Code(
                    "public record Foo() {\n}\n",
                    ImmutableList<CallOut>.Empty,
                    new Dictionary<string, string> { { "language", "java" }, { "role", "hljs" } },
                    false
                )
            },
            body.Children
        );
    }

    [Fact]
    public void PassthroughAttributeSubs()
    {
        var body = new Parser(new Dictionary<string, string> { { "foo-version", "1" } }).ParseBody(
            new Reader(
                """
                [subs=attributes]
                ++++
                <script defer src="/js/test.js?v={foo-version}"></script>
                ++++
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new PassthroughBlock(
                    "<script defer src=\"/js/test.js?v=1\"></script>",
                    new Dictionary<string, string> { { "subs", "attributes" } }
                )
            },
            body.Children
        );
    }

    [Fact]
    public void CodeAttributeSubs()
    {
        var body = new Parser(new Dictionary<string, string> { { "foo-version", "1" } }).ParseBody(
            new Reader(
                """
                [subs=attributes]
                ----
                <script defer src="/js/test.js?v={foo-version}"></script>
                ----
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Code(
                    "<script defer src=\"/js/test.js?v=1\"></script>\n",
                    ImmutableList<CallOut>.Empty,
                    new Dictionary<string, string> { { "subs", "attributes" } },
                    false
                )
            },
            body.Children
        );
    }

    [Fact]
    public void CodeAfterListContinuation()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                * foo
                +
                [source,java,.hljs]
                ----
                public record Foo() {

                }
                ----
                +
                * bar
                +
                ----
                public record Bar() {

                }
                ----
                +
                * end
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new UnOrderedList(
                    [
                        new Paragraph(
                            [
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "foo",
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new Code(
                                    "public record Foo() {\n\n}\n",
                                    ImmutableList<CallOut>.Empty,
                                    new Dictionary<string, string>
                                    {
                                        { "language", "java" },
                                        { "role", "hljs" }
                                    },
                                    false
                                )
                            ],
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Paragraph(
                            [
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "bar",
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new Code(
                                    "public record Bar() {\n\n}\n",
                                    ImmutableList<CallOut>.Empty,
                                    ImmutableDictionary<string, string>.Empty,
                                    false
                                )
                            ],
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "end",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void CodeWithCallout()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                [source,java,.hljs]
                ----
                import anything;
                public record Foo( <1>
                  String name <2>
                ) {
                }
                ----
                                
                <1> Defines a record,
                <.> Defines an attribute of the record.
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Code(
                    """
                    import anything;
                    public record Foo( (1)
                      String name (2)
                    ) {
                    }
                    """.Replace("\r\n", "\n"),
                    [
                        new CallOut(
                            1,
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Defines a record,",
                                ImmutableDictionary<string, string>.Empty
                            )
                        ),
                        new CallOut(
                            2,
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Defines an attribute of the record.",
                                ImmutableDictionary<string, string>.Empty
                            )
                        )
                    ],
                    new Dictionary<string, string> { { "language", "java" }, { "role", "hljs" } },
                    false
                )
            },
            body.Children
        );
    }

    [Fact]
    public void UnorderedList()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                * item 1
                * item 2
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new UnOrderedList(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "item 1",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "item 2",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void UnorderedListUnCommonFormatting()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                * something:
                  Some description.
                ** Parameters:
                  *** --resolve-provider: ...
                  *** --resolve-relaxed: ...
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new UnOrderedList(
                    [
                        new Paragraph(
                            [
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "something: Some description.",
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new UnOrderedList(
                                    [
                                        new Paragraph(
                                            [
                                                new Text(
                                                    ImmutableList<Text.Styling>.Empty,
                                                    "Parameters:",
                                                    ImmutableDictionary<string, string>.Empty
                                                ),
                                                new UnOrderedList(
                                                    [
                                                        new Text(
                                                            ImmutableList<Text.Styling>.Empty,
                                                            "--resolve-provider: ...",
                                                            ImmutableDictionary<
                                                                string,
                                                                string
                                                            >.Empty
                                                        ),
                                                        new Text(
                                                            ImmutableList<Text.Styling>.Empty,
                                                            "--resolve-relaxed: ...",
                                                            ImmutableDictionary<
                                                                string,
                                                                string
                                                            >.Empty
                                                        )
                                                    ],
                                                    ImmutableDictionary<string, string>.Empty
                                                )
                                            ],
                                            ImmutableDictionary<string, string>.Empty
                                        )
                                    ],
                                    ImmutableDictionary<string, string>.Empty
                                )
                            ],
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void OrderedList()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                . item 1
                2. item 2
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new OrderedList(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "item 1",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "item 2",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void OrderedListWithCode()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                . item 1
                +
                [source,java]
                ----
                record Foo() {}
                ----
                +
                2. item 2
                +
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new OrderedList(
                    [
                        new Paragraph(
                            [
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "item 1",
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new Code(
                                    "record Foo() {}\n",
                                    ImmutableList<CallOut>.Empty,
                                    new Dictionary<string, string> { { "language", "java" } },
                                    false
                                )
                            ],
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "item 2",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void OrderedListNested()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                . item 1
                .. item 1 1
                .. item 1 2
                2. item 2
                .. item 2 1
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new OrderedList(
                    [
                        new Paragraph(
                            [
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "item 1",
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new OrderedList(
                                    [
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            "item 1 1",
                                            ImmutableDictionary<string, string>.Empty
                                        ),
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            "item 1 2",
                                            ImmutableDictionary<string, string>.Empty
                                        )
                                    ],
                                    ImmutableDictionary<string, string>.Empty
                                )
                            ],
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Paragraph(
                            [
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "item 2",
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new OrderedList(
                                    [
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            "item 2 1",
                                            ImmutableDictionary<string, string>.Empty
                                        )
                                    ],
                                    ImmutableDictionary<string, string>.Empty
                                )
                            ],
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void UnOrderedListNested()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                [.iconed]
                * item 1
                ** item 1 1
                ** item 1 2
                * item 2
                ** item 2 1
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new UnOrderedList(
                    [
                        new Paragraph(
                            [
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "item 1",
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new UnOrderedList(
                                    [
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            "item 1 1",
                                            ImmutableDictionary<string, string>.Empty
                                        ),
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            "item 1 2",
                                            ImmutableDictionary<string, string>.Empty
                                        )
                                    ],
                                    ImmutableDictionary<string, string>.Empty
                                )
                            ],
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Paragraph(
                            [
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "item 2",
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new UnOrderedList(
                                    [
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            "item 2 1",
                                            ImmutableDictionary<string, string>.Empty
                                        )
                                    ],
                                    ImmutableDictionary<string, string>.Empty
                                )
                            ],
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    new Dictionary<string, string> { { "role", "iconed" } }
                )
            },
            body.Children
        );
    }

    [Fact]
    public void OrderedListMultiLine()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                . item 1
                with continuation
                2. item 2
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new OrderedList(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "item 1 with continuation",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "item 2",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void UnorderedListWithTitle()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                .Foo
                * item 1
                * item 2
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new UnOrderedList(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "item 1",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "item 2",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    new Dictionary<string, string> { { "title", "Foo" } }
                )
            },
            body.Children
        );
    }

    [Fact]
    public void DescriptionList()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                CPU:: The brain of the computer.
                Hard drive:: Permanent storage for operating system and/or user files.
                RAM:: Temporarily stores information the CPU uses during operation.
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new DescriptionList(
                    new Dictionary<string, string>
                    {
                        { "CPU", "The brain of the computer." },
                        {
                            "Hard drive",
                            "Permanent storage for operating system and/or user files."
                        },
                        { "RAM", "Temporarily stores information the CPU uses during operation." }
                    }
                        .Select(it => new KeyValuePair<IElement, IElement>(
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                it.Key,
                                ImmutableDictionary<string, string>.Empty
                            ),
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                it.Value,
                                ImmutableDictionary<string, string>.Empty
                            )
                        ))
                        .ToDictionary(),
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void DescriptionListWithList()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                Dairy::
                * Milk
                * Eggs
                Bakery::
                * Bread
                Produce::
                * Bananas
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new DescriptionList(
                    new Dictionary<string, IElement>
                    {
                        {
                            "Dairy",
                            new UnOrderedList(
                                [
                                    new Text(
                                        ImmutableList<Text.Styling>.Empty,
                                        "Milk",
                                        ImmutableDictionary<string, string>.Empty
                                    ),
                                    new Text(
                                        ImmutableList<Text.Styling>.Empty,
                                        "Eggs",
                                        ImmutableDictionary<string, string>.Empty
                                    )
                                ],
                                ImmutableDictionary<string, string>.Empty
                            )
                        },
                        {
                            "Bakery",
                            new UnOrderedList(
                                [
                                    new Text(
                                        ImmutableList<Text.Styling>.Empty,
                                        "Bread",
                                        ImmutableDictionary<string, string>.Empty
                                    )
                                ],
                                ImmutableDictionary<string, string>.Empty
                            )
                        },
                        {
                            "Produce",
                            new UnOrderedList(
                                [
                                    new Text(
                                        ImmutableList<Text.Styling>.Empty,
                                        "Bananas",
                                        ImmutableDictionary<string, string>.Empty
                                    )
                                ],
                                ImmutableDictionary<string, string>.Empty
                            )
                        }
                    }
                        .Select(it => new KeyValuePair<IElement, IElement>(
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                it.Key,
                                ImmutableDictionary<string, string>.Empty
                            ),
                            it.Value
                        ))
                        .ToDictionary(),
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void Image()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                image:test.png[Test]
                                
                It is inline like image:foo.svg[Bar] or
                                
                image::as-a-block.jpg[Foo,width="100%"]
                                
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Macro(
                    "image",
                    "test.png",
                    new Dictionary<string, string> { { "alt", "Test" } },
                    true
                ),
                new Paragraph(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "It is inline like ",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Macro(
                            "image",
                            "foo.svg",
                            new Dictionary<string, string> { { "alt", "Bar" } },
                            true
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            " or",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                ),
                new Macro(
                    "image",
                    "as-a-block.jpg",
                    new Dictionary<string, string> { { "alt", "Foo" }, { "width", "100%" } },
                    false
                )
            },
            body.Children
        );
    }

    [Fact]
    public void AdmonitionParsing()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                WARNING: Wolpertingers are known to nest in server racks.
                Enter at your own risk.
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Admonition(
                    Admonition.AdmonitionLevel.Warning,
                    new Text(
                        ImmutableList<Text.Styling>.Empty,
                        "Wolpertingers are known to nest in server racks. Enter at your own risk.",
                        ImmutableDictionary<string, string>.Empty
                    )
                )
            },
            body.Children
        );
    }

    [Fact]
    public void Anchor()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                The section <<anchors>> describes how automatic anchors work.
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Paragraph(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "The section ",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Anchor("anchors", ""),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            " describes how automatic anchors work.",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void TitleId()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                == Create a configuration model [[configuration_model]]
                                
                A configuration model is a record marked with RootConfiguration.
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Section(
                    2,
                    new Text(
                        ImmutableList<Text.Styling>.Empty,
                        "Create a configuration model",
                        new Dictionary<string, string> { { "id", "configuration_model" } }
                    ),
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "A configuration model is a record marked with RootConfiguration.",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void Include()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                == My title
                                
                include::foo.adoc[]
                                
                include::bar.adoc[lines=2..3]
                                
                """.Replace("\r\n", "\n").Split('\n')
            ),
            new CustomContentResolver(
                (reference, encoding) =>
                    reference switch
                    {
                        "foo.adoc" => ["This is foo."],
                        "bar.adoc"
                            =>
                            [
                                "This is ignored.",
                                "First included line.",
                                "Last included line.",
                                "Ignored again."
                            ],
                        _ => null
                    }
            )
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Section(
                    2,
                    new Text(
                        ImmutableList<Text.Styling>.Empty,
                        "My title",
                        ImmutableDictionary<string, string>.Empty
                    ),
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "This is foo.",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "First included line. Last included line.",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void IncludeAttributes()
    {
        var doc = new Parser().Parse(
            new Reader(
                """
                = My title
                include::attributes.adoc[]
                                
                {url}[Yupiik]
                """.Replace("\r\n", "\n").Split('\n')
            ),
            new ParserContext(
                new CustomContentResolver(
                    (reference, encoding) =>
                        reference switch
                        {
                            "attributes.adoc" => [":url: https://yupiik.io"],
                            _ => null
                        }
                )
            )
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Link("https://yupiik.io", "Yupiik", ImmutableDictionary<string, string>.Empty)
            },
            doc.Body.Children
        );
    }

    [Fact]
    public void IncludeAttributesAndInlineAttributes()
    {
        var doc = new Parser().Parse(
            new Reader(
                """
                = My title
                :pre: yes
                include::attributes.adoc[]
                :post: true
                                
                {url}[github]
                """.Replace("\r\n", "\n").Split('\n')
            ),
            new ParserContext(
                new CustomContentResolver(
                    (reference, encoding) =>
                        reference switch
                        {
                            "attributes.adoc" => [":url: https://rmannibucau.github.io"],
                            _ => null
                        }
                )
            )
        );
        Assert.Equivalent(
            new Dictionary<string, string>
            {
                { "pre", "yes" },
                { "url", "https://rmannibucau.github.io" },
                { "post", "true" }
            },
            doc.Header.Attributes
        );
    }

    [Fact]
    public void Table()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                [cols="1,1"]
                |===
                |Cell in column 1, row 1
                |Cell in column 2, row 1

                |Cell in column 1, row 2
                |Cell in column 2, row 2

                |Cell in column 1, row 3
                |Cell in column 2, row 3
                |===                    
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Table(
                    [
                        [
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Cell in column 1, row 1",
                                ImmutableDictionary<string, string>.Empty
                            ),
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Cell in column 2, row 1",
                                ImmutableDictionary<string, string>.Empty
                            )
                        ],
                        [
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Cell in column 1, row 2",
                                ImmutableDictionary<string, string>.Empty
                            ),
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Cell in column 2, row 2",
                                ImmutableDictionary<string, string>.Empty
                            )
                        ],
                        [
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Cell in column 1, row 3",
                                ImmutableDictionary<string, string>.Empty
                            ),
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Cell in column 2, row 3",
                                ImmutableDictionary<string, string>.Empty
                            )
                        ]
                    ],
                    new Dictionary<string, string> { { "cols", "1,1" } }
                )
            },
            body.Children
        );
    }

    [Fact]
    public void TableOpts()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                [opts="header"]
                |===
                |c1
                |===
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Table(
                    [
                        [
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "c1",
                                ImmutableDictionary<string, string>.Empty
                            )
                        ]
                    ],
                    new Dictionary<string, string> { { "opts", "header" } }
                )
            },
            body.Children
        );
    }

    [Fact]
    public void TableMultiple()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                [cols="1a,1"]
                |===
                |Cell in column 1, row 1
                [source,java]
                ----
                public class Foo {
                }
                ----
                |Cell in column 2, row 1

                |Cell in column 1, row 2
                |Cell in column 2, row 2
                |===                    
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Table(
                    [
                        [
                            new Paragraph(
                                [
                                    new Text(
                                        ImmutableList<Text.Styling>.Empty,
                                        "Cell in column 1, row 1",
                                        ImmutableDictionary<string, string>.Empty
                                    ),
                                    new Code(
                                        "public class Foo {\n}\n",
                                        ImmutableList<CallOut>.Empty,
                                        new Dictionary<string, string> { { "language", "java" } },
                                        false
                                    )
                                ],
                                ImmutableDictionary<string, string>.Empty
                            ),
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Cell in column 2, row 1",
                                ImmutableDictionary<string, string>.Empty
                            )
                        ],
                        [
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Cell in column 1, row 2",
                                ImmutableDictionary<string, string>.Empty
                            ),
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Cell in column 2, row 2",
                                ImmutableDictionary<string, string>.Empty
                            )
                        ]
                    ],
                    new Dictionary<string, string> { { "cols", "1a,1" } }
                )
            },
            body.Children
        );
    }

    [Fact]
    public void TableRowsInline()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                [cols="1,1"]
                |===
                |Cell in column 1, row 1|Cell in column 2, row 1
                |Cell in column 1, row 2|Cell in column 2, row 2
                |Cell in column 1, row 3|Cell in column 2, row 3
                |===
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Table(
                    [
                        [
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Cell in column 1, row 1",
                                ImmutableDictionary<string, string>.Empty
                            ),
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Cell in column 2, row 1",
                                ImmutableDictionary<string, string>.Empty
                            )
                        ],
                        [
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Cell in column 1, row 2",
                                ImmutableDictionary<string, string>.Empty
                            ),
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Cell in column 2, row 2",
                                ImmutableDictionary<string, string>.Empty
                            )
                        ],
                        [
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Cell in column 1, row 3",
                                ImmutableDictionary<string, string>.Empty
                            ),
                            new Text(
                                ImmutableList<Text.Styling>.Empty,
                                "Cell in column 2, row 3",
                                ImmutableDictionary<string, string>.Empty
                            )
                        ]
                    ],
                    new Dictionary<string, string> { { "cols", "1,1" } }
                )
            },
            body.Children
        );
    }

    [Fact]
    public void SimpleQuote()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                > Somebody said it.
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Quote(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "Somebody said it.",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void SimpleQuoteBlock()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                [quote,Monty Python and the Holy Grail]
                ____
                Dennis: Come and see the violence inherent in the system. Help! Help! I'm being repressed!
                                        
                King Arthur: Bloody peasant!
                                        
                Dennis: Oh, what a giveaway! Did you hear that? Did you hear that, eh? That's what I'm on about! Did you see him repressing me? You saw him, Didn't you?
                ____
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Quote(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "Dennis: Come and see the violence inherent in the system. Help! Help! I'm being repressed!",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "King Arthur: Bloody peasant!",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "Dennis: Oh, what a giveaway! Did you hear that? Did you hear that, eh? That's what I'm on about! Did you see him repressing me? You saw him, Didn't you?",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    new Dictionary<string, string>
                    {
                        { "role", "quoteblock" },
                        { "attribution", "Monty Python and the Holy Grail" }
                    }
                )
            },
            body.Children
        );
    }

    [Fact]
    public void Quote()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                > > What's new?
                >
                > I've got Markdown in my AsciiDoc!
                >
                > > Like what?
                >
                > * Blockquotes
                > * Headings
                > * Fenced code blocks
                >
                > > Is there more?
                >
                > Yep. AsciiDoc and Markdown share a lot of common syntax already.
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new Quote(
                    [
                        new Quote(
                            [
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "What's new?",
                                    ImmutableDictionary<string, string>.Empty
                                )
                            ],
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "I've got Markdown in my AsciiDoc!",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Quote(
                            [
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "Like what?",
                                    ImmutableDictionary<string, string>.Empty
                                )
                            ],
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new UnOrderedList(
                            [
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "Blockquotes",
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "Headings",
                                    ImmutableDictionary<string, string>.Empty
                                ),
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "Fenced code blocks",
                                    ImmutableDictionary<string, string>.Empty
                                )
                            ],
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Quote(
                            [
                                new Text(
                                    ImmutableList<Text.Styling>.Empty,
                                    "Is there more?",
                                    ImmutableDictionary<string, string>.Empty
                                )
                            ],
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "Yep. AsciiDoc and Markdown share a lot of common syntax already.",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void OpenBlock()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                [sidebar]
                .Related information
                --
                This is aside value.
                                        
                It is used to present information related to the main content.
                --
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new OpenBlock(
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "This is aside value.",
                            ImmutableDictionary<string, string>.Empty
                        ),
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "It is used to present information related to the main content.",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    new Dictionary<string, string>
                    {
                        { "", "sidebar" },
                        { "title", "Related information" }
                    }
                )
            },
            body.Children
        );
    }

    [Fact]
    public void Ifdef()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                ifdef::foo[]
                This is value.
                endif::[]
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new ConditionalBlock(
                    new ConditionalBlock.Ifdef("foo").Test,
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "This is value.",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void Ifndef()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                ifndef::foo[]
                This is value.
                endif::[]
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new ConditionalBlock(
                    new ConditionalBlock.Ifndef("foo").Test,
                    [
                        new Text(
                            ImmutableList<Text.Styling>.Empty,
                            "This is value.",
                            ImmutableDictionary<string, string>.Empty
                        )
                    ],
                    ImmutableDictionary<string, string>.Empty
                )
            },
            body.Children
        );
    }

    [Fact]
    public void Passthrough()
    {
        var body = new Parser().ParseBody(
            new Reader(
                """
                ++++
                This is value.
                ++++
                """.Replace("\r\n", "\n").Split('\n')
            ),
            null
        );
        Assert.Equivalent(
            new List<IElement>
            {
                new PassthroughBlock("This is value.", ImmutableDictionary<string, string>.Empty)
            },
            body.Children
        );
    }

    [Fact]
    public void Attributes()
    {
        var body = new Parser().ParseBody(
            new Reader(["This is {replaced} and not this \\{value}."]),
            null
        );
        var children = body.Children;
        Assert.Single(children);
        Assert.Equal(IElement.ElementType.Paragraph, children[0].Type());
        Assert.IsType<Paragraph>(children[0]);
        if (children[0] is Paragraph p)
        {
            Assert.Equivalent(
                new List<IElement.ElementType>
                {
                    IElement.ElementType.Text,
                    IElement.ElementType.Attribute,
                    IElement.ElementType.Text
                },
                p.Children.Select(it => it.Type())
            );
            Assert.Equivalent("replaced", (p.Children[1] as Model.Attribute)!.Name);
            Assert.Equivalent(" and not this {value}.", (p.Children[2] as Text)!.Value);
        }
    }

    [Fact]
    public void Icon()
    {
        // more "complex" since it has a space in the label
        Assert.Equivalent(
            new List<IElement>
            {
                new Macro(
                    "icon",
                    "fas fa-foo",
                    new Dictionary<string, string> { { "size", "2x" } },
                    true
                )
            },
            new Parser().ParseBody(new Reader(["icon:fas fa-foo[size=2x]"])).Children
        );

        // no space
        Assert.Equivalent(
            new List<IElement>
            {
                new Macro(
                    "icon",
                    "heart",
                    new Dictionary<string, string> { { "size", "2x" } },
                    true
                )
            },
            new Parser().ParseBody(new Reader(["icon:heart[size=2x]"]), null).Children
        );
    }

    [Fact]
    public void CodeInclude()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            var code = "test = value\nmultiline = true\n";
            File.WriteAllText(tmpFile, code);

            var work = Directory.GetParent(tmpFile)!.FullName;
            var body = new Parser(
                new Dictionary<string, string> { { "partialsdir", work } }
            ).ParseBody(
                new Reader(
                    """
                    [source,properties,.hljs]
                    ----
                    include::{partialsdir}/content.properties[]
                    ----
                    """.Replace("\r\n", "\n").Replace("content.properties", Path.GetFileName(tmpFile)).Split('\n')
                ),
                new LocalContentResolver(work)
            );
            Assert.Equivalent(
                new List<IElement>
                {
                    new Code(
                        code,
                        ImmutableList<CallOut>.Empty,
                        new Dictionary<string, string>
                        {
                            { "language", "properties" },
                            { "role", "hljs" }
                        },
                        false
                    )
                },
                body.Children
            );
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public void CodeIncludeNested()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            var code =
                "foo::\nbar\ndummy::\nsomething\n[source]\n----\ntest\n\n----\n\nother::\nend";
            File.WriteAllText(tmpFile, code);

            var work = Directory.GetParent(tmpFile)!.FullName;
            var body = new Parser(
                new Dictionary<string, string> { { "partialsdir", work } }
            ).ParseBody(
                new Reader(
                    """
                    include::{partialsdir}/content.properties[]
                    """.Replace("content.properties", Path.GetFileName(tmpFile)).Split('\n')
                ),
                new LocalContentResolver(work)
            );
            Assert.Equivalent(
                new List<IElement>
                {
                    new Paragraph(
                        [
                            new DescriptionList(
                                new Dictionary<IElement, IElement>
                                {
                                    {
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            "foo",
                                            ImmutableDictionary<string, string>.Empty
                                        ),
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            "bar",
                                            ImmutableDictionary<string, string>.Empty
                                        )
                                    },
                                    {
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            "dummy",
                                            ImmutableDictionary<string, string>.Empty
                                        ),
                                        new Paragraph(
                                            [
                                                new Text(
                                                    ImmutableList<Text.Styling>.Empty,
                                                    "something",
                                                    ImmutableDictionary<string, string>.Empty
                                                ),
                                                new Code(
                                                    "test\n\n",
                                                    ImmutableList<CallOut>.Empty,
                                                    ImmutableDictionary<string, string>.Empty,
                                                    false
                                                )
                                            ],
                                            ImmutableDictionary<string, string>.Empty
                                        )
                                    }
                                },
                                ImmutableDictionary<string, string>.Empty
                            ),
                            new DescriptionList(
                                new Dictionary<IElement, IElement>
                                {
                                    {
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            "other",
                                            ImmutableDictionary<string, string>.Empty
                                        ),
                                        new Text(
                                            ImmutableList<Text.Styling>.Empty,
                                            "end",
                                            ImmutableDictionary<string, string>.Empty
                                        )
                                    }
                                },
                                ImmutableDictionary<string, string>.Empty
                            )
                        ],
                        ImmutableDictionary<string, string>.Empty
                    )
                },
                body.Children
            );
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }
}
