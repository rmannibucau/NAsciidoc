namespace NAsciidoc.Model;

public record Attribute(string Name, Func<string, IList<IElement>> Evaluator) : IElement
{
    public IElement.ElementType Type()
    {
        return IElement.ElementType.Attribute;
    }
}
