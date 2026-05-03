namespace TerraPDF.Sample;

using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;

/// <summary>
/// Sample document demonstrating the Table of Contents (TOC) feature.
/// Generates a multi-chapter report with automatic page numbering and internal navigation.
/// </summary>
public class ReportWithToc : IDocument
{
    public void Compose(IDocumentContainer container)
    {
        // Add a Table of Contents page at the beginning
        container.TableOfContents(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.PageColor(Color.White);
            p.DefaultTextStyle(s => s.FontFamily("Helvetica").FontSize(12).FontColor("#000000").SemiBold().LineHeight(1.5));
            p.Header().MarginBottom(12)
               .Text("Table of Contents").AlignCenter()
               .Bold().FontSize(20).FontColor(Color.Black);
        });

        // Chapter 1 — Introduction
        container.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.PageColor(Color.White);
            p.DefaultTextStyle(s => s.FontSize(11).LineHeight(1.6));

            p.Header()
                .Text("Report with Table of Contents")
                .Bold().FontSize(14).FontColor(Color.Grey.Medium);

            p.Content().Column(col =>
            {
                col.Spacing(8);

                col.Item().H1("Introduction");

                col.Item().Text("This report demonstrates TerraPDF's Table of Contents generation feature. " +
                                "Headings are automatically collected from the document and used to build " +
                                "an interactive TOC page with clickable page numbers.").Justify();

                col.Item().H2("What is TerraPDF?");

                col.Item().Text("TerraPDF is a free, pure C# library designed for fast and reliable PDF generation. " +
                                "It provides a fluent, composable API that covers the full document-authoring lifecycle " +
                                "— from page layout and rich text to tables, images, hyperlinks, and multi-page pagination " +
                                "— with no native binaries, no third-party runtime packages, and no licensing restrictions.")
                       .Justify();

                col.Item().Text("Key features include:").Bold();

                col.Item().Column(inner =>
                {
                    inner.Item().MarginLeft(10).Text("• Zero dependencies — pure C#").Italic();
                    inner.Item().MarginLeft(10).Text("• Fluent, intuitive API").Italic();
                    inner.Item().MarginLeft(10).Text("• Multi-page automatic pagination").Italic();
                    inner.Item().MarginLeft(10).Text("• Internal hyperlinks and bookmarks").Italic();
                    inner.Item().MarginLeft(10).Text("• Tables with repeating headers").Italic();
                });

                col.Item().H2("Table of Contents Feature");

                col.Item().Text("The Table of Contents feature allows you to automatically generate a navigable contents " +
                                "page from your document's headings. Simply call .TableOfContents() once at the desired " +
                                "location (typically at the beginning), then use .H1(), .H2(), etc. for section headings " +
                                "anywhere in the document.");

                col.Item().H3("How it works");

                col.Item().Text("TerraPDF performs a two-pass layout: first it measures the document and collects all " +
                                "heading positions, then it renders the final PDF with a fully populated TOC containing " +
                                "accurate page numbers and internal links.");

                col.Item().H4("Supported heading levels");

                col.Item().Text("Six levels are available: H1 (largest) through H6 (smallest). Each level has sensible " +
                                "default styling which can be overridden using the TextDescriptor fluent API.");

                col.Item().H5("Example customisation");

                col.Item().Text("You can customise any heading after creation, e.g. .H2('Title').FontColor(Color.Blue).Underline();");

                col.Item().H6("Technical note");

                col.Item().Text("Headings nested inside Tables or Rows are also collected. The TOC page itself is ignored " +
                                "so headings do not self-reference.");
            });
        });

        // Chapter 2 — Features demo
        container.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.PageColor(Color.White);
            p.DefaultTextStyle(s => s.FontSize(11).LineHeight(1.6));

            p.Header()
                .AlignCenter()
                .Text("Chapter 2 — Features")
                .Bold().FontSize(14).FontColor(Color.Grey.Medium);

            p.Content().Column(col =>
            {
                col.Spacing(8);

                col.Item().H1("Available Heading Levels");

                col.Item().Text("TerraPDF provides six levels of headings with the following defaults:").Justify();

                // List heading levels as simple text
                col.Item().MarginLeft(20).Text("H1  —  Chapter title   (24 pt, bold)").FontSize(11);
                col.Item().MarginLeft(20).Text("H2  —  Section title   (20 pt, bold)").FontSize(11);
                col.Item().MarginLeft(20).Text("H3  —  Sub-section    (16 pt, bold)").FontSize(11);
                col.Item().MarginLeft(20).Text("H4  —  Minor heading   (14 pt, italic)").FontSize(11);
                col.Item().MarginLeft(20).Text("H5  —  Small heading   (12 pt, bold)").FontSize(11);
                col.Item().MarginLeft(20).Text("H6  —  Tiny heading    (11 pt, regular)").FontSize(11);

                col.Item().H2("Styling Customisation");

                col.Item().Text("You can customise any heading using the fluent TextDescriptor API after creation. For example:")
                       .Justify();

                col.Item().MarginLeft(20).Background(Color.Grey.Lighten4).Padding(8)
                      .Text("container.H2('Custom Heading').FontColor(Color.Blue.Darken2).Underline();")
                      .FontSize(10).Italic();

                col.Item().H2("Sample Table");

                col.Item().Text("Tables do not generate TOC entries themselves, but any heading inside a table cell is " +
                                "picked up by the scanner.").Justify();

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(4);
                        cols.RelativeColumn(1);
                        cols.ConstantColumn(60);
                    });

                    table.HeaderRow(row =>
                    {
                        row.Cell().Background(Color.Blue.Darken2).Padding(6).Text("Description").Bold().FontColor(Color.White);
                        row.Cell().Background(Color.Blue.Darken2).Padding(6).AlignCenter().Text("Qty").Bold().FontColor(Color.White);
                        row.Cell().Background(Color.Blue.Darken2).Padding(6).AlignRight().Text("Amount").Bold().FontColor(Color.White);
                    });

                    table.Row(row =>
                    {
                        row.Cell().Padding(6).Text("TerraPDF License");
                        row.Cell().Padding(6).AlignCenter().Text("1");
                        row.Cell().Padding(6).AlignRight().Text("Free");
                    });
                    table.Row(row =>
                    {
                        row.Cell().Padding(6).Text("Support Hours");
                        row.Cell().Padding(6).AlignCenter().Text("24/7");
                        row.Cell().Padding(6).AlignRight().Text("$0.00");
                    });
                });

                col.Item().H3("Table note");
                col.Item().Text("The table above is purely illustrative and does not affect the TOC.");

                col.Item().H4("Further reading");
                col.Item().Text("Refer to Chapter 3 for information about internal links and navigation.");
            });
        });

        // Chapter 3 — Internal links
        container.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.PageColor(Color.White);
            p.DefaultTextStyle(s => s.FontSize(11).LineHeight(1.6));

            p.Header()
                .AlignCenter()
                .Text("Chapter 3 — Internal Navigation")
                .Bold().FontSize(14).FontColor(Color.Grey.Medium);

            p.Content().Column(col =>
            {
                col.Spacing(8);

                col.Item().H1("Linking Within Documents");

                col.Item().Text("Internal links allow readers to click and jump to specific pages in the PDF. TerraPDF's " +
                                "TOC automatically creates these internal links for all headings, but you can also add " +
                                "custom internal-link anchors anywhere in your content.")
                       .Justify();

                col.Item().H2("Using InternalLink()");

                col.Item().Text("Wrap any content in .InternalLink(targetPage, topY) to make it navigable:")
                       .Justify();

                col.Item().MarginLeft(20).Background(Color.Grey.Lighten4).Padding(8)
                      .Text("container.InternalLink(2, 120).Text('Go to Chapter 2');")
                      .FontSize(10).Italic();

                col.Item().Text("The TOC entries themselves are created this way behind the scenes.");
            });
        });
    }
}
