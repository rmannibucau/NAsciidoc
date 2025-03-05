namespace NAsciidoc.Model
{
    public interface IElement
    /*
    permits
        Code, DescriptionList, LineBreak, Link, Macro, OrderedList,
        Paragraph, Section, Text, UnOrderedList, Admonition, Anchor,
        Table, Quote, OpenBlock, PassthroughBlock, ConditionalBlock,
        Attribute, PageBreak, Listing
    */
    {
        ElementType Type();

        IDictionary<string, string> Opts();

        public enum ElementType
        {
            // PREAMBLE, // not really supported/needed, if needed it can be detected by checking the paragraphs after first title and before next subtitle
            // EXAMPLE, // not really supported/needed, this is just a custom role
            // VERSE, // not really supported/needed, this is just a custom role
            Attribute,
            Paragraph,
            Section,
            LineBreak,
            PageBreak,
            Code, // including source blocks
            UnorderedList,
            OrderedList,
            DescriptionList,
            Link,
            Text,
            Listing,
            Macro, // icon, image, audio, video, kbd, btn, menu, doublefootnote, footnote, stem, xref, pass
            Admonition,
            Anchor,
            Table,
            OpenBlock,
            Quote, // TODO: we only support the markdown style quotes
            PassBlock,
            ConditionalBlock,
        }
    }
}
