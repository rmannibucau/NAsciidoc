namespace NAsciidoc.Ascii2SVG.Tests;

using System.Reflection;
using NAsciidoc.Ascii2SVG;

public class SvgTests
{
    [Theory]
    [InlineData("0.a2s", "0.svg")]
    [InlineData("1.a2s", "1.svg")]
    [InlineData("2.a2s", "2.svg")]
    public void Render(string raw, string expected)
    {
        var input = Load(raw);
        var output = Load(expected);
        var svg = new Svg().Convert(input);
        Assert.Equal(output.Trim(), svg.Trim());
    }

    private static string Load(string path)
    {
        var assembly = typeof(SvgTests).Assembly;
        var name = assembly.GetCustomAttribute<AssemblyTitleAttribute>()!.Title;
        using var stream = assembly.GetManifestResourceStream($"{name}.resources.{path}");
        using var reader = new StreamReader(
            stream ?? throw new ArgumentException($"No resource {path}", nameof(path))
        );
        return reader.ReadToEnd();
    }
}
