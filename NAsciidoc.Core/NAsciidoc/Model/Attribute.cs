using System.Collections.Immutable;

namespace NAsciidoc.Model;

public record Attribute(string Name, Func<string, IList<IElement>> Evaluator) : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.Attribute;

    public IDictionary<string, string> Opts() => ImmutableDictionary<string, string>.Empty;
}
