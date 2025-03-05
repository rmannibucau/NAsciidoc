namespace NAsciidoc.Model;

public record PassthroughBlock(string Value, IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.PassBlock;

    public IDictionary<string, string> Opts() => Options;
}
