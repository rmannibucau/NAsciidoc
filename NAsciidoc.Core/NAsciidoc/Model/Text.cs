namespace NAsciidoc.Model;

public record Text(IList<Text.Styling> Style, string Value, IDictionary<string, string> Options)
    : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.Text;

    public IDictionary<string, string> Opts() => Options;

    public enum Styling
    {
        Bold,
        Italic,
        Emphasis,
        Mark,
        Sub,
        Sup,
    }
}
