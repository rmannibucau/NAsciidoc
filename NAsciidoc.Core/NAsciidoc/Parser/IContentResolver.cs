using System.Text;

namespace NAsciidoc.Parser
{
    public interface IContentResolver
    {
        IEnumerable<string>? Resolve(string reference, Encoding? encoding);
    }
}
