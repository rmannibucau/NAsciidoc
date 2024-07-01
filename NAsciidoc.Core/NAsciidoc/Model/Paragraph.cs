namespace NAsciidoc.Model;

public record Paragraph(IList<IElement> Children, IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type()
    {
        return IElement.ElementType.Paragraph;
    }
}
