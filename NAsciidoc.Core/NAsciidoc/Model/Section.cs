namespace NAsciidoc.Model;

public record Section(
    int Level,
    IElement Title,
    IList<IElement> Children,
    IDictionary<string, string> Options
) : IElement
{
    public IElement.ElementType Type()
    {
        return IElement.ElementType.Section;
    }
}
