namespace NAsciidoc.Model;

public record Anchor(string Value, string Label) : IElement
{
    public IElement.ElementType Type()
    {
        return IElement.ElementType.Anchor;
    }
}
