namespace NAsciidoc.Model;

public record Table(IList<IList<IElement>> Elements, IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type()
    {
        return IElement.ElementType.Table;
    }
}
