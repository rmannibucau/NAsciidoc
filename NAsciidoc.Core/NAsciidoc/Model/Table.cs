namespace NAsciidoc.Model;

public record Table(IList<IList<IElement>> Elements, IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.Table;

    public IDictionary<string, string> Opts() => Options;
}
