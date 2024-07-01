namespace NAsciidoc.Model;

public record Header(
    string Title,
    Author? Author,
    Revision? Revision,
    IDictionary<string, string> Attributes
) { }
