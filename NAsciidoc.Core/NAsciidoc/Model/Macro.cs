namespace NAsciidoc.Model;

public record Macro(
    string Name, // ex: icon, kbd, image, ...
    string Label, // todo: IElement - depends the macro?
    IDictionary<string, string> Options,
    bool Inline
) : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.Macro;

    public IDictionary<string, string> Opts() => Options;
}
