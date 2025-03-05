namespace NAsciidoc.Model;

public record OpenBlock(IList<IElement> Children, IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.OpenBlock;

    public IDictionary<string, string> Opts() => Options;
}
