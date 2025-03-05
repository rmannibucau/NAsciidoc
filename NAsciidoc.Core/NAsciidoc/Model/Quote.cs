namespace NAsciidoc.Model;

public record Quote(IList<IElement> Children, IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.Quote;

    public IDictionary<string, string> Opts() => Options;
}
