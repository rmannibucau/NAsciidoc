---
uid: getting-started
---

# Getting Started

## Import the project

Project is bundled as a NuGet package: `NAsciidoc.Core`.
The easiest to import it is to run `dotnet add package NAsciidoc.Core` command on your project (`csproj`).

Once done you have access to the two main entry points:

`NAsciidoc.Parser.Parser`
:   the Asciidoc parser, it loads the document passed as parameter as an AST,

`NAsciidoc.Renderer.AsciidoctorLikeHtmlRenderer`
:   a simple HTML renderer mimicing as much as possible Asciidoctor rendering - enabling to reuse `asciidoctor.css` style sheet.

## Render a document

To render a document with previous classes just run:

```cs
// load the AST
var context = new ParserContext(new LocalContentResolver("path/to/workspace"));
var doc = new Parser.Parser().Parse(
    """
    = My Adoc

    With some content.
    """,
    context
);

// render as HTML - configuration has some customization
var conf = new AsciidoctorLikeHtmlRenderer.Configuration();
var renderer = new AsciidoctorLikeHtmlRenderer(conf);
renderer.Visit(doc);

// print the rendered HTML document
var html = renderer.Result();
Console.WriteLine(html);
```

> [!TIP]
> The renderer is a `Visitor<x>` which basically goes thru the AST to create an output.
> It is easy to implement to get another rendering and potentially something else than HTML (for example it can be used to render a document as a `Spectre.Console` output, a PDF, etc...).


## Diagrams/Graphics

By default the project is integrated with `NAsciidoc.Ascii2SVG` which is a dotnet port of [ascii2svg](https://github.com/asciitosvg/asciitosvg).
It is usable in asciidoc blocks:

[source,adoc]
----
= My Doc
                            
[a2s, format="svg"]
....
.-------------------------.
|                         |
| .---.-. .-----. .-----. |
| | .-. | +-->  | |  <--| |
| | '-' | |  <--| +-->  | |
| '---'-' '-----' '-----' |
|  ascii     2      svg   |
|                         |
'-------------------------'
....
----
