namespace NAsciidoc.Model;

public record UnOrderedList(IList<IElement> Children, IDictionary<string, string> Options)
    : IElement
{
    public IElement.ElementType Type()
    {
        return IElement.ElementType.UnorderedList;
    }
}
