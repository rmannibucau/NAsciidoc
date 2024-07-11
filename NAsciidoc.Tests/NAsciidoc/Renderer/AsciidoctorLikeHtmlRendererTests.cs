using NAsciidoc.Parser;

namespace NAsciidoc.Renderer;

public class AsciidoctorLikeHtmlRendererTests
{
    [Fact]
    public void InlineCodeInDefinitionLst()
    {
        AssertRenderingContent(
            """
            `scheduler.tasks` (env: `SCHEDULER_TASKS`)::
            Default tasks to run. Key is the command and value the frequency. `once_a_day` is supported today and means at 11:00AM, `at_startup` means at startup of the server whenever it is. Default: `check-zulu-releases = once_a_day`.
            """,
            """
             <dl>
              <dt> <div class="paragraph">
            <code>scheduler.tasks</code> (env: <code>SCHEDULER_TASKS</code>) </div>
            </dt>
              <dd>
             <div class="paragraph">
             <p>Default tasks to run. Key is the command and value the frequency. <code>once_a_day</code> is supported today and means at 11:00AM, <code>at_startup</code> means at startup of the server whenever it is. Default: <code>check-zulu-releases = once_a_day</code>.</p>
             </div>
            </dd>
             </dl>
            """
        );
    }

    [Fact]
    public void PassthroughIncludeJson()
    {
        var json = "{\n  \"openapi\":\"3.0.1\"\n}\n";
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, json);
        try
        {
            AssertRenderingContent(
                """
                ++++
                SwaggerUIBundle({
                    spec:
                include::openapi.json[]
                ,
                    dom_id: '#swagger-ui',
                    deepLinking: true,
                    presets: [
                      SwaggerUIBundle.presets.apis,
                      SwaggerUIBundle.SwaggerUIStandalonePreset,
                    ],
                    plugins: [
                      SwaggerUIBundle.plugins.DownloadUrl,
                    ],
                });
                ++++
                """.Replace("openapi.json", Path.GetFileName(tmp)),
                """
                                
                SwaggerUIBundle({
                    spec:
                {
                  "openapi":"3.0.1"
                }

                ,
                    dom_id: '#swagger-ui',
                    deepLinking: true,
                    presets: [
                      SwaggerUIBundle.presets.apis,
                      SwaggerUIBundle.SwaggerUIStandalonePreset,
                    ],
                    plugins: [
                      SwaggerUIBundle.plugins.DownloadUrl,
                    ],
                });
                """,
                Directory.GetParent(tmp)!.FullName
            );
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void MetaInPreamble()
    {
        // this is not a strict preamble as of today but this concept should likely be revisited since
        // it fakes rendering too much in style and enforces an undesired id in several cases
        AssertRendering(
            """
            = Yupiik Fusion 1.0.8 Released
                            
            [.metadata]
            [.mr-2.category-release]#icon:fas fa-gift[]#, [.metadata-authors]#link:http://localhost:4200/blog/author/francois-papon/page-1.html[Francois Papon]#, [.metadata-published]#2023-09-26#, [.metadata-readingtime]#49 sec read#
                            
            [abstract]
            Blabla.
                            
            == What's new?
                            
            * [dependencies] Upgrade to Apache Tomcat 10.1.13.
            """,
            """
            <!DOCTYPE html>
            <html lang="en">
            <head>
             <meta charset="UTF-8">
             <meta http-equiv="X-UA-Compatible" content="IE=edge">
            </head>
            <body>
             <div id="content">
             <h1>Yupiik Fusion 1.0.8 Released</h1>
             <div id="preamble">
             <div class="sectionbody">
             <div class="paragraph metadata">
             <p><span class="mr-2 category-release"><span class="icon"><i class="fas fa-gift"></i></span></span>,  <a href="http://localhost:4200/blog/author/francois-papon/page-1.html" class="metadata-authors">Francois Papon</a>
            , <span class="metadata-published">2023-09-26</span>, <span class="metadata-readingtime">49 sec read</span></p>
             </div>
             </div>
             </div>
             <div class="sect1">
             <div class="quoteblock abstract">
              <blockquote>
            Blabla.  </blockquote>
             </div> </div>
             <div class="sect1" id="_whats_new">
              <h2>What's new?</h2>
             <div class="sectionbody">
             <div class="ulist">
             <ul>
              <li>
             <p>
            [dependencies] Upgrade to Apache Tomcat 10.1.13.
             </p>
              </li>
             </ul>
             </div>
             </div>
             </div>
             </div>
            </body>
            </html>

            """
        );
    }

    [Fact]
    public void RenderHtml()
    {
        AssertRendering(
            """
            = Main title
                            
            Some text.
                            
            == Second part
                            
            This is a snippet:
                            
            [source,java]
            ----
            public record Foo() {}
            ----
            """,
            """
            <!DOCTYPE html>
            <html lang="en">
            <head>
             <meta charset="UTF-8">
             <meta http-equiv="X-UA-Compatible" content="IE=edge">
            </head>
            <body>
             <div id="content">
             <h1>Main title</h1>
             <div id="preamble">
             <div class="sectionbody">
             <p> <div class="paragraph">
             <p>
            Some text.
             </p>
             </div>
            </p>
             </div>
             </div>
             <div class="sect1" id="_second_part">
              <h2>Second part</h2>
             <div class="sectionbody">
             <div class="paragraph">
             <p>
            This is a snippet:
             </p>
             </div>
             <div class="listingblock">
             <div class="content">
             <pre class="highlightjs highlight"><code class="language-java hljs" data-lang="java">public record Foo() {}</code></pre>
             </div>
             </div>
             </div>
             </div>
             </div>
            </body>
            </html>
            """
        );
    }

    [Fact]
    public void Callout()
    {
        AssertRenderingContent(
            """
            == Enums
                                                        
            Enumerations (de)serialization behavior can be customized by using some specific methods:
                       
            [source,java]
            ----
            public enum MyEnum {
                A, B;
                
                public String toJsonString() { <1>
                    return this == A ? "first" : "second";
                }
                
                public static MyEnum fromJsonString(final String v) { <2>
                    return switch (v) {
                        case "first" -> MyEnum.A;
                        case "second" -> MyEnum.B;
                        default -> throw new IllegalArgumentException("Unsupported '" + v + "'");
                    };
                }
            }
            ----
            <.> `toJsonString` is an instance method with no parameter used to replace `.name()` call during serialization,
            <.> `fromJsonString` is a static method with a `String` parameter used to replace `.valueOf(String)` call during deserialization.
            """,
            """
             <div class="sect1" id="_enums">
              <h2>Enums</h2>
             <div class="sectionbody">
             <div class="paragraph">
             <p>
            Enumerations (de)serialization behavior can be customized by using some specific methods:
             </p>
             </div>
             <div class="listingblock">
             <div class="content">
             <pre class="highlightjs highlight"><code class="language-java hljs" data-lang="java">public enum MyEnum {
                A, B;
                
                public String toJsonString() { <b class="conum">(1)</b>
                    return this == A ? &quot;first&quot; : &quot;second&quot;;
                }
                
                public static MyEnum fromJsonString(final String v) { <b class="conum">(2)</b>
                    return switch (v) {
                        case &quot;first&quot; -&gt; MyEnum.A;
                        case &quot;second&quot; -&gt; MyEnum.B;
                        default -&gt; throw new IllegalArgumentException(&quot;Unsupported '&quot; + v + &quot;'&quot;);
                    };
                }
            }</code></pre>
             </div>
             </div>
             <div class="colist arabic">
              <ol>
               <li>
            <code>toJsonString</code> <span>
             is an instance method with no parameter used to replace 
             </span>
            <code>.name()</code> <span>
             call during serialization,
             </span>
               </li>
               <li>
            <code>fromJsonString</code> <span>
             is a static method with a 
             </span>
            <code>String</code> <span>
             parameter used to replace 
             </span>
            <code>.valueOf(String)</code> <span>
             call during deserialization.
             </span>
               </li>
              </ol>
             </div>
             </div>
             </div>
            """
        );
    }

    [Fact]
    public void Ol()
    {
        AssertRenderingContent(
            """
            . first
            . second
            . third
            """,
            """
             <ol>
              <li>
             <p>
            first
             </p>
              </li>
              <li>
             <p>
            second
             </p>
              </li>
              <li>
             <p>
            third
             </p>
              </li>
             </ol>
            """
        );
    }

    [Fact]
    public void Il()
    {
        AssertRenderingContent(
            """
            * first
            * second
            * third
            """,
            """
             <div class="ulist">
             <ul>
              <li>
             <p>
            first
             </p>
              </li>
              <li>
             <p>
            second
             </p>
              </li>
              <li>
             <p>
            third
             </p>
              </li>
             </ul>
             </div>
            """
        );
    }

    [Fact]
    public void UlWithOptions()
    {
        AssertRenderingContent(
            """
            [role="blog-links blog-links-page"]
            * link:http://localhost:4200/blog/index.html[All posts,role="blog-link-all"]
            * link:http://localhost:4200/blog/page-2.html[Next,role="blog-link-next"]
            """,
            """
             <div class="ulist blog-links blog-links-page">
             <ul>
              <li>
             <p> <a href="http://localhost:4200/blog/index.html" class="blog-link-all">All posts</a>
            </p>  </li>
              <li>
             <p> <a href="http://localhost:4200/blog/page-2.html" class="blog-link-next">Next</a>
            </p>  </li>
             </ul>
             </div>
            """
        );
    }

    [Fact]
    public void Dl()
    {
        AssertRenderingContent(
            """
            first:: one
            second:: two
            """,
            """
             <dl>
              <dt>first</dt>
              <dd>
            one</dd>
              <dt>second</dt>
              <dd>
            two</dd>
             </dl>
            """
        );
    }

    [Fact]
    public void Admonition()
    {
        AssertRenderingContent(
            "NOTE: this is an important note.",
            """
             <div class="admonitionblock note">
              <table>
                <tbody>
                 <tr>
                  <td class="icon">
                 <div class="title">NOTE</div>
                   </td>
                  <td class="content">
            this is an important note.    </td>
               </tr>
                  </tbody>
              </table>
             </div>
            """
        );
    }

    [Fact]
    public void Table()
    {
        AssertRenderingContent(
            """
            [cols="1,1",opts="header"]
            |===
            |Cell in column 1, header row |Cell in column 2, header row 
                            
            |Cell in column 1, row 2
            |Cell in column 2, row 2
                            
            |Cell in column 1, row 3
            |Cell in column 2, row 3
                            
            |Cell in column 1, row 4
            |Cell in column 2, row 4
            |===
            """,
            """
             <table class="tableblock frame-all grid-all stretch">
              <colgroup>
               <col width="50%">
               <col width="50%">
              </colgroup>
              <thead>
               <tr>
                <th>
            Cell in column 1, header row     </th>
                <th>
            Cell in column 2, header row    </th>
               </tr>
              </thead>
              <tbody>
               <tr>
                <td>
            Cell in column 1, row 2    </td>
                <td>
            Cell in column 2, row 2    </td>
               </tr>
               <tr>
                <td>
            Cell in column 1, row 3    </td>
                <td>
            Cell in column 2, row 3    </td>
               </tr>
               <tr>
                <td>
            Cell in column 1, row 4    </td>
                <td>
            Cell in column 2, row 4    </td>
               </tr>
              </tbody>
             </table>
            """
        );
    }

    [Fact]
    public void TableWithInlineAdoc()
    {
        AssertRenderingContent(
            """
            [cols="1,1"]
            |===
            |Cell in column 1, header row |Cell in column 2, header row 
                            
            |Cell in column 1, `row 1`
            |===
            """,
            """
            <table class="tableblock frame-all grid-all stretch">
              <colgroup>
               <col width="50%">
               <col width="50%">
              </colgroup>
              <tbody>
               <tr>
                <td>
            Cell in column 1, header row     </td>
                <td>
            Cell in column 2, header row    </td>
               </tr>
               <tr>
                <td>
             <div class="paragraph">
            Cell in column 1, <code>row 1</code> </div>
                </td>
               </tr>
              </tbody>
             </table>
            """
        );
    }

    [Fact]
    public void Quote()
    {
        AssertRenderingContent(
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
            """,
            """
             <div>
              <blockquote>
             <div>
              <blockquote>
            What's new?  </blockquote>
             </div>I've got Markdown in my AsciiDoc! <div>
              <blockquote>
            Like what?  </blockquote>
             </div> <div class="ulist">
             <ul>
              <li>
             <p>
            Blockquotes
             </p>
              </li>
              <li>
             <p>
            Headings
             </p>
              </li>
              <li>
             <p>
            Fenced code blocks
             </p>
              </li>
             </ul>
             </div>
             <div>
              <blockquote>
            Is there more?  </blockquote>
             </div>Yep. AsciiDoc and Markdown share a lot of common syntax already.  </blockquote>
             </div>
            """
        );
    }

    [Fact]
    public void Passthrough()
    {
        AssertRenderingContent(
            """
            ++++
            <div id="test">Content</div>
            ++++
            """,
            """
                            
            <div id="test">Content</div>
            """
        );
    }

    [Fact]
    public void Stem()
    {
        AssertRenderingContent(
            """
            = Some formulas

            [stem]
            ++++
            sqrt(4) = 2
            ++++
                            
            And inline stem:[[[a,b\],[c,d\]\]((n),(k))] too.
            """,
            """
             <div class="sect0" id="_some_formulas">
              <h1>Some formulas</h1>
             <div class="sectionbody">
             <div class="stemblock">
              <div class="content">
             \$sqrt(4) = 2\$   </div>
             </div>
             <div class="paragraph">
             <p>And inline  \$[[a,b\],[c,d\]\]((n),(k))\$  too.</p>
             </div>
             </div>
             </div>
            """
        );
    }

    [Fact]
    public void StemWithSemicolon()
    {
        AssertRenderingContent(
            """        
            And inline stem:[a:b/2].
            """,
            """
             <div class="paragraph">
             <p>And inline  \$a:b/2\$ .</p>
             </div>
            """
        );
    }

    [Fact]
    public void XRef()
    {
        AssertRenderingContent("xref:foo.adoc[Bar]", " <a href=\"foo.html\">Bar</a>\n");
    }

    [Fact]
    public void EmbeddedImage()
    {
        var tmp = Path.GetTempFileName();
        var base64 =
            "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";
        File.WriteAllBytes(tmp, Convert.FromBase64String(base64));
        var doc = new Parser.Parser().ParseBody(
            new Reader(
                """
                = Test
                                
                image::img.png[logo]
                """.Replace("img.png", tmp).Split("\n")
            )
        );
        var renderer = new AsciidoctorLikeHtmlRenderer(
            new AsciidoctorLikeHtmlRenderer.Configuration
            {
                AssetsBase = Directory.GetParent(tmp)!.FullName,
                Attributes = new Dictionary<string, string>
                {
                    { "noheader", "true" },
                    { "data-uri", "" }
                }
            }
        );
        renderer.VisitBody(doc);
        Assert.Equal(
            """
             <div class="sect0" id="_test">
              <h1>Test</h1>
             <div class="sectionbody">
             <div class="imageblock">
             <div class="content">
             <img src="data:image/png;base64,$base64" alt="logo">
             </div>
             </div>
             </div>
             </div>
            """.Replace("$base64", base64).Trim(),
            renderer.Result().Trim()
        );
    }

    [Fact]
    public void ImageRole()
    {
        var doc = new Parser.Parser().ParseBody(
            new Reader(
                """
                = Test
                                
                image::img.png[logo,.center.w80]
                """.Split('\n')
            )
        );
        var renderer = new AsciidoctorLikeHtmlRenderer(
            new AsciidoctorLikeHtmlRenderer.Configuration
            {
                Attributes = new Dictionary<string, string> { { "noheader", "true" } }
            }
        );
        renderer.VisitBody(doc);
        Assert.Equal(
            """
             <div class="sect0" id="_test">
              <h1>Test</h1>
             <div class="sectionbody">
             <div class="imageblock">
             <div class="content">
             <img src="img.png" alt="logo" class="center w80">
             </div>
             </div>
             </div>
             </div>
            """.Trim(),
            renderer.Result().Trim()
        );
    }

    [Fact]
    public void ImageUnderscores()
    {
        var doc = new Parser.Parser().ParseBody(
            new Reader(
                """
                = Test
                                
                * image:Apache_Feather_Logo.png[romain_asf,role="w32"] link:https://home.apache.org/committer-index.html#rmannibucau[ASF Member]
                """.Split("\n")
            )
        );
        var renderer = new AsciidoctorLikeHtmlRenderer(
            new AsciidoctorLikeHtmlRenderer.Configuration
            {
                Attributes = new Dictionary<string, string> { { "noheader", "true" } }
            }
        );
        renderer.VisitBody(doc);
        Assert.Equal(
            """
             <div class="sect0" id="_test">
              <h1>Test</h1>
             <div class="sectionbody">
             <div class="ulist">
             <ul>
              <li>
             <div class="paragraph">
             <p> <img src="Apache_Feather_Logo.png" alt="romain_asf" class="w32">
             <a href="https://home.apache.org/committer-index.html#rmannibucau">ASF Member</a>
            </p>
             </div>
              </li>
             </ul>
             </div>
             </div>
             </div>
            """.Trim(),
            renderer.Result().Trim()
        );
    }

    [Fact]
    public void Icon()
    {
        AssertRenderingContent(
            "icon:fas fa-gift[]",
            "<span class=\"icon\"><i class=\"fas fa-gift\"></i></span>"
        );
    }

    [Fact]
    public void CodeInSectionTitle()
    {
        AssertRenderingContent(
            "== Section `#1`",
            """
             <div class="sect1" id="_section_1">
              <h2>Section <code>#1</code></h2>
             <div class="sectionbody">
             </div>
             </div>
            """
        );
    }

    [Fact]
    public void CodeInSectionTitleComplex()
    {
        AssertRenderingContent(
            """
            == Title :: foo `bar.json`
                            
            foo
            """,
            """
             <div class="sect1" id="_title__foo_barjson">
              <h2>Title :: foo <code>bar.json</code></h2>
             <div class="sectionbody">
             <div class="paragraph">
             <p>
            foo
             </p>
             </div>
             </div>
             </div>
            """
        );
    }

    [Fact]
    public void LinkWithImage()
    {
        AssertRenderingContent(
            "link:http://foo.bar[this is image:foo.png[alt]]",
            """
             <a href="http://foo.bar">this is  <img src="foo.png" alt="alt">
            </a>
            """
        );
    }

    [Fact]
    public void Callouts()
    {
        AssertRenderingContent(
            """
            [source,properties]
            ----
            prefix.version = 1.2.3 <1>
            ----
            <.> Version of the tool to install, using `relaxed` option it can be a version prefix (`21.` for ex),
            """,
            """
             <div class="listingblock">
             <div class="content">
             <pre class="highlightjs highlight"><code class="language-properties hljs" data-lang="properties">prefix.version = 1.2.3 <b class="conum">(1)</b></code></pre>
             </div>
             </div>
             <div class="colist arabic">
              <ol>
               <li>
             <span>
            Version of the tool to install, using 
             </span>
            <code>relaxed</code> <span>
             option it can be a version prefix (
             </span>
            <code>21.</code> <span>
             for ex),
             </span>
               </li>
              </ol>
             </div>
            """
        );
    }

    private void AssertRendering(string adoc, string html)
    {
        var doc = new Parser.Parser().Parse(
            adoc,
            new ParserContext(new LocalContentResolver("target/missing"))
        );
        var renderer = new AsciidoctorLikeHtmlRenderer();
        renderer.Visit(doc);
        Assert.Equal(html.Trim(), renderer.Result().Trim());
    }

    private void AssertRenderingContent(string adoc, string html, string? work = null)
    {
        var doc = new Parser.Parser().ParseBody(
            new Reader(adoc.Split('\n')),
            new LocalContentResolver(work ?? "target/missing")
        );
        var renderer = new AsciidoctorLikeHtmlRenderer(
            new AsciidoctorLikeHtmlRenderer.Configuration
            {
                Attributes = new Dictionary<string, string> { { "noheader", "true" } }
            }
        );
        renderer.VisitBody(doc);
        var expected = html.Trim();
        var actual = renderer.Result().Trim();
        Assert.Equal(expected, actual);
    }
}
