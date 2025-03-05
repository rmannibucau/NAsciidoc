namespace NAsciidoc.Model;

public record Paragraph(IList<IElement> Children, IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.Paragraph;

    public IDictionary<string, string> Opts() => Options;
}
