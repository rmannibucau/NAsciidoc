namespace NAsciidoc.Model
{
    public record Admonition(Admonition.AdmonitionLevel Level, IElement Content) : IElement
    {
        public IElement.ElementType Type()
        {
            return IElement.ElementType.Admonition;
        }

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
