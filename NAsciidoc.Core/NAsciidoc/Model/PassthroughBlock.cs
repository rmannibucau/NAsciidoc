namespace NAsciidoc.Model;

public record PassthroughBlock(string Value, IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type()
    {
        return IElement.ElementType.PassBlock;
    }
}
