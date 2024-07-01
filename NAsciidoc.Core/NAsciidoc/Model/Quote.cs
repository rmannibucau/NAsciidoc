namespace NAsciidoc.Model;

public record Quote(IList<IElement> Children, IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type()
    {
        return IElement.ElementType.Quote;
    }
}
