using System.Text;

namespace NAsciidoc.Parser
{
    public class LocalContentResolver(string root) : IContentResolver
    {
        public IEnumerable<string>? Resolve(string reference, Encoding? encoding)
        {
            var resolved = Path.IsPathRooted(reference) ? reference : Path.Combine(root, reference);
            if (!File.Exists(resolved))
            {
                return null;
            }
            return File.ReadLines(resolved, encoding ?? Encoding.UTF8);
        }
    }
}
