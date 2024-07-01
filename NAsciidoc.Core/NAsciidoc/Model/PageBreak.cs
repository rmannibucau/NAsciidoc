namespace NAsciidoc.Model;

public record PageBreak(IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type()
    {
        return IElement.ElementType.PageBreak;
    }
}
