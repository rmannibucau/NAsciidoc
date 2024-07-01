namespace NAsciidoc.Model;

public record Link(string Url, string Label, IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type()
    {
        return IElement.ElementType.Link;
    }
}
