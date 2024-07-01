namespace NAsciidoc.Model;

public record OrderedList(IList<IElement> Children, IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type()
    {
        return IElement.ElementType.OrderedList;
    }
}
