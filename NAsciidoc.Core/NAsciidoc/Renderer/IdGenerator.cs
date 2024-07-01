using System.Text.RegularExpressions;

namespace NAsciidoc.Renderer
{
    public static partial class IdGenerator
    {
        public static string ForTitle(string title)
        {
            return '_'
                + ForbiddenChars()
                    .Replace(
                        Tags()
                            .Replace(title, "")
                            .ToLowerInvariant()
                            .Replace(' ', '_')
                            .Replace("\n", ""),
                        ""
                    );
        }

        [GeneratedRegex("<[^>]+>")]
        private static partial Regex Tags();

        [GeneratedRegex("[^\\w]+")]
        private static partial Regex ForbiddenChars();
    }
}
