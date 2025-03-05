namespace NAsciidoc.Model;

public record Code(
    string Value,
    IList<CallOut> CallOuts,
    IDictionary<string, string> Options,
    bool Inline
) : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.Code;

    public IDictionary<string, string> Opts() => Options;
}
