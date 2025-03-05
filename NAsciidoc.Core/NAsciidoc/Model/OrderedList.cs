namespace NAsciidoc.Model;

public record OrderedList(IList<IElement> Children, IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.OrderedList;

    public IDictionary<string, string> Opts() => Options;
}
