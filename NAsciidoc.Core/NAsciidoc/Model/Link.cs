namespace NAsciidoc.Model;

public record Link(string Url, string Label, IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.Link;

    public IDictionary<string, string> Opts() => Options;
}
