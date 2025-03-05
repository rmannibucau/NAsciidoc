namespace NAsciidoc.Model;

public record PageBreak(IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.PageBreak;

    public IDictionary<string, string> Opts() => Options;
}
