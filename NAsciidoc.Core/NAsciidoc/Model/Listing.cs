namespace NAsciidoc.Model;

public record Listing(string Value, IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type()
    {
        return IElement.ElementType.Listing;
    }
}
