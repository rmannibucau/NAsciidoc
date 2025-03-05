using System.Collections.Immutable;

namespace NAsciidoc.Model
{
    public record Admonition(Admonition.AdmonitionLevel Level, IElement Content) : IElement
    {
        public IElement.ElementType Type() => IElement.ElementType.Admonition;

        public IDictionary<string, string> Opts() => ImmutableDictionary<string, string>.Empty;

        public enum AdmonitionLevel
        {
            Note,
            Tip,
            Important,
            Caution,
            Warning,
        }
    }
}
