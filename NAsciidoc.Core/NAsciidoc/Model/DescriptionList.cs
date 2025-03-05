namespace NAsciidoc.Model;

public record DescriptionList(
    IDictionary<IElement, IElement> Children,
    IDictionary<string, string> Options
) : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.DescriptionList;

    public IDictionary<string, string> Opts() => Options;
}
