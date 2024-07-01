using System.Collections.Immutable;
using NAsciidoc.Model;

namespace NAsciidoc.Renderer;

public class TocVisitorTests
{
    [Fact]
    public void Run()
    {
        var tocVisitor = new TocVisitor(2, 1);
        tocVisitor.VisitBody(
            new Body(
                [
                    new Section(
                        1,
                        new Text([], "S1", ImmutableDictionary<string, string>.Empty),
                        [],
                        ImmutableDictionary<string, string>.Empty
                    ),
                    new Section(
                        1,
                        new Text([], "S2", ImmutableDictionary<string, string>.Empty),
                        [],
                        ImmutableDictionary<string, string>.Empty
                    ),
                    new Section(
                        1,
                        new Text([], "S3", ImmutableDictionary<string, string>.Empty),
                        [
                            new Section(
                                2,
                                new Text([], "S31", ImmutableDictionary<string, string>.Empty),
                                [],
                                ImmutableDictionary<string, string>.Empty
                            ),
                            new Section(
                                2,
                                new Text([], "S32", ImmutableDictionary<string, string>.Empty),
                                [],
                                ImmutableDictionary<string, string>.Empty
                            ),
                            new Section(
                                2,
                                new Text([], "S33", ImmutableDictionary<string, string>.Empty),
                                [
                                    new Section(
                                        3,
                                        new Text(
                                            [],
                                            "S331",
                                            ImmutableDictionary<string, string>.Empty
                                        ),
                                        [],
                                        ImmutableDictionary<string, string>.Empty
                                    )
                                ],
                                ImmutableDictionary<string, string>.Empty
                            )
                        ],
                        ImmutableDictionary<string, string>.Empty
                    ),
                    new Section(
                        1,
                        new Text([], "S4", ImmutableDictionary<string, string>.Empty),
                        [],
                        ImmutableDictionary<string, string>.Empty
                    ),
                    new Section(
                        1,
                        new Text([], "S5", ImmutableDictionary<string, string>.Empty),
                        [],
                        ImmutableDictionary<string, string>.Empty
                    )
                ]
            )
        );
        Assert.Equal(
            """
             <ul class="sectlevel1">
             <li><a href="#_s1">S1</a>
             </li>
             <li><a href="#_s2">S2</a>
             </li>
             <li><a href="#_s3">S3</a>
             <ul class="sectlevel2">
             <li><a href="#_s31">S31</a></li>
             <li><a href="#_s32">S32</a></li>
             <li><a href="#_s33">S33</a></li>
             </ul>
             </li>
             <li><a href="#_s4">S4</a>
             </li>
             <li><a href="#_s5">S5</a>
             </li>
             </ul>

            """,
            tocVisitor.Result().ToString()
        );
    }
}
