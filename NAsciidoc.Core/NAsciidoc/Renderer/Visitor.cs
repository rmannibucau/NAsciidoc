using NAsciidoc.Model;

namespace NAsciidoc.Renderer
{
    public class Visitor<R>
    {
        public virtual void Visit(Document document)
        {
            VisitHeader(document.Header);
            VisitBody(document.Body);
        }

        public virtual void VisitHeader(Header header)
        {
            if (!string.IsNullOrWhiteSpace(header.Title))
            {
                VisitTitle(header.Title);
            }
            if (!string.IsNullOrWhiteSpace(header.Author?.Name))
            {
                VisitAuthor(header.Author.Name, header.Author.Mail);
            }
            if (!string.IsNullOrWhiteSpace(header.Revision?.Number))
            {
                VisitRevision(
                    header.Revision.Number,
                    header.Revision.Date ?? "",
                    header.Revision.RevMark ?? ""
                );
            }
        }

        public virtual void VisitBody(Body body)
        {
            foreach (var it in body.Children)
            {
                VisitElement(it);
            }
        }

        public virtual void VisitElement(IElement element)
        {
            switch (element.Type())
            {
                case IElement.ElementType.PassBlock:
                    VisitPassthroughBlock((PassthroughBlock)element);
                    break;
                case IElement.ElementType.OpenBlock:
                    VisitOpenBlock((OpenBlock)element);
                    break;
                case IElement.ElementType.Quote:
                    VisitQuote((Quote)element);
                    break;
                case IElement.ElementType.Table:
                    VisitTable((Table)element);
                    break;
                case IElement.ElementType.Anchor:
                    VisitAnchor((Anchor)element);
                    break;
                case IElement.ElementType.Admonition:
                    VisitAdmonition((Admonition)element);
                    break;
                case IElement.ElementType.Macro:
                    VisitMacro((Macro)element);
                    break;
                case IElement.ElementType.DescriptionList:
                    VisitDescriptionList((DescriptionList)element);
                    break;
                case IElement.ElementType.OrderedList:
                    VisitOrderedList((OrderedList)element);
                    break;
                case IElement.ElementType.UnorderedList:
                    VisitUnOrderedList((UnOrderedList)element);
                    break;
                case IElement.ElementType.Link:
                    VisitLink((Link)element);
                    break;
                case IElement.ElementType.Code:
                    VisitCode((Code)element);
                    break;
                case IElement.ElementType.Text:
                    VisitText((Text)element);
                    break;
                case IElement.ElementType.LineBreak:
                    VisitLineBreak((LineBreak)element);
                    break;
                case IElement.ElementType.PageBreak:
                    VisitPageBreak((PageBreak)element);
                    break;
                case IElement.ElementType.Paragraph:
                    VisitParagraph((Paragraph)element);
                    break;
                case IElement.ElementType.Section:
                    VisitSection((Section)element);
                    break;
                case IElement.ElementType.ConditionalBlock:
                    VisitConditionalBlock((ConditionalBlock)element);
                    break;
                case IElement.ElementType.Attribute:
                    VisitAttribute((Model.Attribute)element);
                    break;
                case IElement.ElementType.Listing:
                    VisitListing((Listing)element);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported block: {element.Type()}");
            }
        }

        public virtual void VisitListing(Listing element)
        {
            // no-op
        }

        public virtual void VisitPageBreak(PageBreak element)
        {
            // no-op
        }

        public virtual void VisitAttribute(Model.Attribute element)
        {
            var attribute = Context().Attribute(element.Name);
            if (attribute is not null)
            {
                foreach (var it in element.Evaluator(attribute))
                {
                    VisitElement(it);
                }
            }
        }

        public virtual void VisitConditionalBlock(ConditionalBlock element)
        {
            if (element.Evaluator(Context()))
            {
                foreach (var it in element.Children)
                {
                    VisitElement(it);
                }
            }
        }

        ConditionalBlock.IContext Context()
        {
            return new ConditionalBlock.EmptyContext();
        }

        public virtual void VisitSection(Section element)
        {
            foreach (var it in element.Children)
            {
                VisitElement(it);
            }
        }

        public virtual void VisitParagraph(Paragraph element)
        {
            foreach (var it in element.Children)
            {
                VisitElement(it);
            }
        }

        public virtual void VisitLineBreak(LineBreak element)
        {
            // no-op
        }

        public virtual void VisitText(Text element)
        {
            // no-op
        }

        public virtual void VisitCode(Code element)
        {
            // no-op
        }

        public virtual void VisitLink(Link element)
        {
            // no-op
        }

        public virtual void VisitUnOrderedList(UnOrderedList element)
        {
            foreach (var it in element.Children)
            {
                VisitElement(it);
            }
        }

        public virtual void VisitOrderedList(OrderedList element)
        {
            foreach (var it in element.Children)
            {
                VisitElement(it);
            }
        }

        public virtual void VisitDescriptionList(DescriptionList element)
        {
            foreach (var it in element.Children)
            {
                VisitElement(it.Key);
                VisitElement(it.Value);
            }
        }

        public virtual void VisitMacro(Macro element)
        {
            // no-op
        }

        public virtual void VisitAdmonition(Admonition element)
        {
            VisitElement(element.Content);
        }

        public virtual void VisitAnchor(Anchor element)
        {
            // no-op
        }

        public virtual void VisitTable(Table element)
        {
            foreach (var it in element.Elements.SelectMany(it => it))
            {
                VisitElement(it);
            }
        }

        public virtual void VisitQuote(Quote element)
        {
            foreach (var it in element.Children)
            {
                VisitElement(it);
            }
        }

        public virtual void VisitOpenBlock(OpenBlock element)
        {
            foreach (var it in element.Children)
            {
                VisitElement(it);
            }
        }

        public virtual void VisitPassthroughBlock(PassthroughBlock element)
        {
            // no-op
        }

        public virtual void VisitRevision(String number, String date, String remark)
        {
            // no-op
        }

        public virtual void VisitAuthor(String name, String mail)
        {
            // no-op
        }

        public virtual void VisitTitle(String title)
        {
            // no-op
        }

        public virtual R? Result()
        {
            return default;
        }
    }
}
