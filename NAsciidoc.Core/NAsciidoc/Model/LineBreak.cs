namespace NAsciidoc.Model;

public record LineBreak() : IElement
{
    public IElement.ElementType Type()
    {
        return IElement.ElementType.LineBreak;
    }
}
