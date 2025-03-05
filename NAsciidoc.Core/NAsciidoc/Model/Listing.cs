namespace NAsciidoc.Model;

public record Listing(string Value, IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.Listing;

    public IDictionary<string, string> Opts() => Options;
}
