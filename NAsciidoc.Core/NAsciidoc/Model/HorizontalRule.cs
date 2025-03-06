namespace NAsciidoc.Model;

public sealed record HorizontalRule(IDictionary<string, string> Options) : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.HorizontalRule;

    public IDictionary<string, string> Opts() => Options;
}
