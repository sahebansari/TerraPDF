using TerraPDF.Core;
using TerraPDF.Helpers;

namespace TerraPDF.Sample.Samples;

// =============================================================================
//  8. REPORT WITH BOOKMARKS / OUTLINES
//  Shows: anchor-based bookmarks — each outline entry is attached directly to
//  its heading with container.Bookmark("Title"[, parentTitle]), so the target
//  page and exact vertical position are resolved automatically during render.
//  Clicking an entry scrolls precisely to the anchored heading and keeps the
//  reader's current zoom level (/XYZ destinations with null zoom).
// =============================================================================
internal static class ReportWithBookmarks
{
    internal static void Generate(string path)
    {
        const string brand      = "#2E4057";
        const string lightBrand = "#F0F4F8";
        const string accent     = "#1ABC9C";
        const string muted      = "#607D8B";

        Document.Create(doc =>
        {
            doc.MetadataTitle("TerraPDF Developer Guide");
            doc.MetadataAuthor("TerraPDF Engineering Team");
            doc.MetadataSubject("Comprehensive guide to TerraPDF features and APIs");
            doc.MetadataKeywords("pdf; terra; dotnet; guide; documentation");
            doc.MetadataCreator("TerraPDF Sample Generator v1.0");

            // Bookmarks are anchored inline on the headings below — no page
            // numbers or Y offsets to maintain by hand. See each
            // .Bookmark("Title"[, parentTitle]) call in the page content.

            // ── Page 1: Introduction ──────────────────────────────────────────
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2.5, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(11));

                page.Header().Background(brand).Padding(10).Column(h =>
                    h.Item().AlignCenter().Text("TERRAPDF DEVELOPER GUIDE")
                        .Bold().FontSize(18).FontColor(Color.White));

                page.Content().Column(col =>
                {
                    col.Item().Bookmark("Introduction").PaddingTop(20).Text("Introduction")
                        .Bold().FontSize(22).FontColor(brand).AlignCenter();
                    col.Item().PaddingTop(12).Text(
                        "This guide provides a comprehensive overview of TerraPDF, " +
                        "a pure-C# document generation library with zero runtime dependencies. " +
                        "TerraPDF enables developers to create professional-quality PDF documents " +
                        "programmatically using a fluent, composable API. From simple text blocks " +
                        "to complex multi-page tables with repeating headers, TerraPDF handles " +
                        "all aspects of document layout, styling, and content pagination.")
                        .Justify().FontColor(muted);

                    col.Item().PaddingTop(16).Text("Getting Started")
                        .Bold().FontSize(16).FontColor(brand);
                    col.Item().PaddingTop(8).Text(
                        "To begin using TerraPDF, add the NuGet package to your project " +
                        "and start building documents with the fluent API. The library supports " +
                        "custom page sizes, margins, headers, footers, and a rich set of text " +
                        "formatting options including bold, italic, underline, strikethrough, " +
                        "and per-span styling. Page numbers can be inserted as dynamic placeholders.")
                        .Justify().FontColor(muted);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("TerraPDF Developer Guide  |  ").FontSize(9).FontColor(muted);
                    t.CurrentPageNumber().FontSize(9).FontColor(brand).Bold();
                    t.Span(" / ").FontSize(9).FontColor(muted);
                    t.TotalPages().FontSize(9).FontColor(brand).Bold();
                });
            });

            // ── Page 2: Getting Started ───────────────────────────────────────
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2.5, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(11));

                page.Header().Background(brand).Padding(10).Column(h =>
                    h.Item().AlignCenter().Text("GETTING STARTED")
                        .Bold().FontSize(16).FontColor(lightBrand));

                page.Content().Column(col =>
                {
                    // The top-level "Getting Started" entry and its first child
                    // are both anchored on this heading (chained wrappers).
                    col.Item()
                        .Bookmark("Getting Started")
                        .Bookmark("Installation", parentTitle: "Getting Started")
                        .PaddingTop(24).Text("Installation").Bold().FontSize(18).FontColor(brand);
                    col.Item().PaddingTop(10).Text(
                        "Install TerraPDF via NuGet: dotnet add package TerraPDF. " +
                        "The library targets .NET 8.0 and .NET 9.0, with no external dependencies. " +
                        "All PDF generation is done using pure managed code, ensuring compatibility " +
                        "across all platforms supported by .NET.")
                        .Justify().FontColor(muted);

                    col.Item().Bookmark("Quick Start", parentTitle: "Getting Started")
                        .PaddingTop(20).Text("Quick Start").Bold().FontSize(18).FontColor(brand);
                    col.Item().PaddingTop(10).Text(
                        "Create your first PDF in minutes with the fluent builder pattern. " +
                        "Start with Document.Create(), define one or more pages, and add content " +
                        "to the header, content, and footer slots. The library automatically handles " +
                        "multi-page pagination, table splitting, and page numbering.")
                        .Justify().FontColor(muted);

                    col.Item().Bookmark("Configuration", parentTitle: "Getting Started")
                        .PaddingTop(20).Text("Configuration").Bold().FontSize(18).FontColor(brand);
                    col.Item().PaddingTop(10).Text(
                        "Every PageDescriptor offers extensive configuration: page size (A0-A6, Letter, " +
                        "Legal, or custom), margins (uniform or per-edge), background colour, and a " +
                        "default text style that cascades to all child elements. Use the HeaderOnFirstPageOnly() " +
                        "method to restrict header rendering to the first page of a multi-page section.")
                        .Justify().FontColor(muted);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ").FontSize(9).FontColor(muted);
                    t.CurrentPageNumber().FontSize(9).FontColor(brand).Bold();
                    t.Span(" of ").FontSize(9).FontColor(muted);
                    t.TotalPages().FontSize(9).FontColor(brand).Bold();
                });
            });

            // ── Page 3: Core Features – Text & Typography ─────────────────────
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2.5, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(11));

                page.Header().Background(brand).Padding(10).Column(h =>
                    h.Item().AlignCenter().Text("CORE FEATURES – TEXT & TYPOGRAPHY")
                        .Bold().FontSize(16).FontColor(lightBrand));

                page.Content().Column(col =>
                {
                    col.Item()
                        .Bookmark("Core Features")
                        .Bookmark("Text & Typography", parentTitle: "Core Features")
                        .PaddingTop(10).Text("Text & Typography").Bold().FontSize(18).FontColor(brand);
                    col.Item().PaddingTop(8).Text(
                        "TerraPDF provides comprehensive typographic controls: font size, colour (hex or " +
                        "Material Design palette), bold, italic, underline, strikethrough, and line-height " +
                        "spacing. The TextBlock element tokenises input into spans, allowing mixed formatting " +
                        "within a single paragraph. Alignment options include left, centre, right, and justified.")
                        .Justify().FontColor(muted);

                    col.Item().Bookmark("Layout Containers", parentTitle: "Core Features")
                        .PaddingTop(16).Text("Layout Containers").Bold().FontSize(18).FontColor(brand);
                    col.Item().PaddingTop(8).Text(
                        "Three core layout containers enable flexible page composition:\n" +
                        "• Column  — vertically stacks items with configurable spacing; auto-paginates.\n" +
                        "• Row     — arranges children horizontally with relative/constant sizing.\n" +
                        "• Table   — grid layout with header rows that repeat on continuation pages.\n" +
                        "All containers support decorators: Padding, Margin, Background, Border, and Alignment.")
                        .Justify().FontColor(muted);

                    col.Item().Bookmark("Images & Hyperlinks", parentTitle: "Core Features")
                        .PaddingTop(16).Text("Images & Hyperlinks").Bold().FontSize(18).FontColor(brand);
                    col.Item().PaddingTop(8).Text(
                        "Embed PNG (RGB) and JPEG images directly. TerraPDF decodes PNGs and recompresses " +
                        "them with FlateDecode; JPEGs are embedded verbatim. Hyperlink elements create URI " +
                        "annotations — clickable areas that open external URLs in the viewer.")
                        .Justify().FontColor(muted);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ").FontSize(9).FontColor(muted);
                    t.CurrentPageNumber().FontSize(9).FontColor(brand).Bold();
                    t.Span(" of ").FontSize(9).FontColor(muted);
                    t.TotalPages().FontSize(9).FontColor(brand).Bold();
                });
            });

            // ── Page 4: Core Features – Tables & Decorators ───────────────────
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2.5, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(11));

                page.Header().Background(brand).Padding(10).Column(h =>
                    h.Item().AlignCenter().Text("CORE FEATURES – TABLES & DECORATORS")
                        .Bold().FontSize(16).FontColor(lightBrand));

                page.Content().Column(col =>
                {
                    col.Item().Bookmark("Tables", parentTitle: "Core Features")
                        .PaddingTop(10).Text("Tables").Bold().FontSize(18).FontColor(brand);
                    col.Item().PaddingTop(8).Text(
                        "Tables are the most powerful layout primitive in TerraPDF. Define columns using " +
                        "relative widths (proportional to available space) or fixed point sizes. Header rows " +
                        "are automatically repeated on every page when a table spans multiple pages. Cells " +
                        "support column-span and row-span for merged layouts. Rows are rendered with consistent " +
                        "heights across page breaks, ensuring predictable pagination.")
                        .Justify().FontColor(muted);

                    col.Item().Bookmark("Decorators", parentTitle: "Core Features")
                        .PaddingTop(16).Text("Decorators").Bold().FontSize(18).FontColor(brand);
                    col.Item().PaddingTop(8).Text(
                        "Every container and element can be wrapped with decorators that modify its box model. " +
                        "Padding adds inner spacing; Margin adds outer spacing; Background fills the content " +
                        "area; Border draws edges; Alignment changes the positioning context. Decorators chain " +
                        "freely and are applied in a well-defined order: Margin → Padding → Background → Border.")
                        .Justify().FontColor(muted);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ").FontSize(9).FontColor(muted);
                    t.CurrentPageNumber().FontSize(9).FontColor(brand).Bold();
                    t.Span(" of ").FontSize(9).FontColor(muted);
                    t.TotalPages().FontSize(9).FontColor(brand).Bold();
                });
            });

            // ── Page 5: Advanced Topics ───────────────────────────────────────
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2.5, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(11));

                page.Header().Background(brand).Padding(10).Column(h =>
                    h.Item().AlignCenter().Text("ADVANCED TOPICS")
                        .Bold().FontSize(16).FontColor(lightBrand));

                page.Content().Column(col =>
                {
                    col.Item()
                        .Bookmark("Advanced Topics")
                        .Bookmark("Multi-Page Documents", parentTitle: "Advanced Topics")
                        .PaddingTop(24).Text("Multi-Page Documents").Bold().FontSize(18).FontColor(brand);
                    col.Item().PaddingTop(8).Text(
                        "TerraPDF uses a two-pass rendering strategy: the first pass measures all content " +
                        "to determine the total page count (enabling accurate page-number placeholders), " +
                        "and the second pass emits the final PDF objects. Columns automatically split across " +
                        "pages when content overflows; explicit PageBreak() elements force a new page at any point.")
                        .Justify().FontColor(muted).FontSize(10);

                    col.Item().Bookmark("Custom Styling", parentTitle: "Advanced Topics")
                        .PaddingTop(16).Text("Custom Styling").Bold().FontSize(18).FontColor(brand);
                    col.Item().PaddingTop(8).Text(
                        "The TextStyle value object is immutable — every mutating method returns a new instance. " +
                        "This copy-on-write pattern ensures that styles are safely shared across elements without " +
                        "accidental mutation. Use the DefaultTextStyle() method on PageDescriptor to establish " +
                        "a base style, then override selectively on individual elements.")
                        .Justify().FontColor(muted).FontSize(10);

                    col.Item().Bookmark("Performance Tips", parentTitle: "Advanced Topics")
                        .PaddingTop(16).Text("Performance Tips").Bold().FontSize(18).FontColor(brand);
                    col.Item().PaddingTop(8).Text(
                        "TerraPDF is designed for high-throughput scenarios: PDFs are generated entirely in-memory " +
                        "with efficient string builders and binary stream compression. Avoid repeatedly creating " +
                        "identical styled TextBlocks — reuse IComponent implementations when the same content block " +
                        "appears on multiple pages or in multiple places.")
                        .Justify().FontColor(muted).FontSize(10);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ").FontSize(9).FontColor(muted);
                    t.CurrentPageNumber().FontSize(9).FontColor(brand).Bold();
                    t.Span(" of ").FontSize(9).FontColor(muted);
                    t.TotalPages().FontSize(9).FontColor(brand).Bold();
                });
            });

            // ── Page 6: Appendix ──────────────────────────────────────────────
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2.5, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(11));

                page.Header().Background(brand).Padding(10).Column(h =>
                    h.Item().AlignCenter().Text("APPENDIX")
                        .Bold().FontSize(16).FontColor(lightBrand));

                page.Content().Column(col =>
                {
                    col.Item()
                        .Bookmark("Appendix")
                        .Bookmark("API Reference", parentTitle: "Appendix")
                        .PaddingTop(24).Text("API Reference").Bold().FontSize(18).FontColor(brand);
                    col.Item().PaddingTop(8).Text(
                        "The TerraPDF API surface is intentionally small and composable. Key entry points:\n" +
                        "• Document.Create() — starts a new document builder.\n" +
                        "• IDocumentContainer.Page() — configures a page via PageDescriptor.\n" +
                        "• IContainer methods: Text(), Column(), Row(), Table(), Image(), Hyperlink().\n" +
                        "• TextStyle modifiers: Bold(), Italic(), Underline(), FontSize(), FontColor().\n" +
                        "All public methods validate arguments and throw appropriate exceptions for invalid input.")
                        .Justify().FontColor(muted).FontSize(10);

                    col.Item().Bookmark("Sample Code", parentTitle: "Appendix")
                        .PaddingTop(20).Text("Sample Code").Bold().FontSize(18).FontColor(brand);
                    col.Item().PaddingTop(8).Background(lightBrand).Padding(10).Text(
                        "var pdf = Document.Create(doc =>\n" +
                        "{\n    doc.Page(page =>\n" +
                        "    {\n        page.Size(PageSize.A4);\n        page.Content().Text(\"Hello\");\n    });\n" +
                        "}).PublishPdf(\"output.pdf\");")
                        .FontColor(Color.Blue.Darken1).FontSize(9).Justify();

                    col.Item().Bookmark("Migration Guide", parentTitle: "Appendix")
                        .PaddingTop(16).Text("Migration Guide").Bold().FontSize(18).FontColor(brand);
                    col.Item().PaddingTop(8).Text(
                        "Upgrading from TerraPDF 1.x to 2.0? Key breaking changes include the new descriptor " +
                        "pattern (PageDescriptor, TextDescriptor, SpanDescriptor), removal of the legacy fluent " +
                        "API methods in favour of explicit configuration objects, and the switch to immutable " +
                        "TextStyle. See the online migration guide for a detailed diff and automated upgrade scripts.")
                        .Justify().FontColor(muted).FontSize(10);

                    col.Item().PaddingTop(10).Background(accent).Padding(8).AlignCenter()
                        .Text("Thank you for choosing TerraPDF!")
                        .Bold().FontSize(12).FontColor(Color.White);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ").FontSize(9).FontColor(muted);
                    t.CurrentPageNumber().FontSize(9).FontColor(brand).Bold();
                    t.Span(" of ").FontSize(9).FontColor(muted);
                    t.TotalPages().FontSize(9).FontColor(brand).Bold();
                });
            });
        }).PublishPdf(path);

        Console.WriteLine($"  [8] Bookmarks demo        -> {path}");
    }
}
