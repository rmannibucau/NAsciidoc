namespace NAsciidoc.Model;

public record ConditionalBlock(
    Predicate<ConditionalBlock.IContext> Evaluator,
    IList<IElement> Children,
    IDictionary<string, string> Options
) : IElement
{
    public IElement.ElementType Type() => IElement.ElementType.ConditionalBlock;

    public IDictionary<string, string> Opts() => Options;

    public interface IContext
    {
        string? Attribute(string key);
    }

    public class EmptyContext : IContext
    {
        public string? Attribute(string key) => null;
    }

    public class DictionaryContext(IDictionary<string, string> Attributes) : IContext
    {
        public string? Attribute(string key) => Attributes.TryGetValue(key, out var v) ? v : null;
    }

    public record Ifdef(string Attribute)
    {
        public bool Test(IContext context)
        {
            return context.Attribute(Attribute) is not null;
        }
    }

    public record Ifndef(string Attribute)
    {
        public bool Test(IContext context)
        {
            return context.Attribute(Attribute) is null;
        }
    }

    public record Ifeval(Predicate<IContext> Evaluator)
    {
        public bool Test(IContext context)
        {
            return Evaluator(context);
        }
    }
}
