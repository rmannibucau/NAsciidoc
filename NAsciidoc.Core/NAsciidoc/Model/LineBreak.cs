using System.Collections.Immutable;

namespace NAsciidoc.Model;

public record LineBreak() : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.LineBreak;

    public IDictionary<string, string> Opts() => ImmutableDictionary<string, string>.Empty;
}
