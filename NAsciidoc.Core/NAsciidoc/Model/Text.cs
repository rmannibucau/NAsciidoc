namespace NAsciidoc.Model;

public record Text(IList<Text.Styling> Style, string Value, IDictionary<string, string> Options)
    : IElement
{
    public IElement.ElementType Type()
    {
        return IElement.ElementType.Text;
    }

    public enum Styling
    {
        Bold,
        Italic,
        Emphasis,
        Mark,
        Sub,
        Sup
    }
}
