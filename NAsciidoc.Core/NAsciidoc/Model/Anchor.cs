using System.Collections.Immutable;

namespace NAsciidoc.Model;

public record Anchor(string Value, string Label) : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.Anchor;

    public IDictionary<string, string> Opts() => ImmutableDictionary<string, string>.Empty;
}
