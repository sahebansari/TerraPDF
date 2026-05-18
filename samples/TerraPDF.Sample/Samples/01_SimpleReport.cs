using TerraPDF.Core;
using TerraPDF.Helpers;

namespace TerraPDF.Sample.Samples;

// =============================================================================
//  1. SIMPLE REPORT
//  Shows: bold / italic / strikethrough / colour spans, justified paragraphs,
//  horizontal rules, multi-span mixed text, alternating-row table, page numbers.
// =============================================================================
internal static class SimpleReport
{
    internal static void Generate(string path)
    {
        const string accent = "#2E4057";
        const string light  = "#F0F4F8";

        Document.Create(doc =>
        {
            // ── Document metadata ──────────────────────────────────────────────
            doc.MetadataTitle("TerraPDF Developer Guide");
            doc.MetadataAuthor("TerraPDF Engineering Team");
            doc.MetadataSubject("Comprehensive guide to TerraPDF features and APIs");
            doc.MetadataKeywords("pdf; terra; dotnet; guide; documentation");
            doc.MetadataCreator("TerraPDF Sample Generator v1.0");

            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2.5, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(11));

                // ── Header ───────────────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Background(accent).Padding(10)
                       .Text("ANNUAL PERFORMANCE REPORT")
                       .Bold().FontSize(16).FontColor(Color.White).AlignCenter();

                    col.Item().PaddingTop(4)
                       .Text("Fiscal Year 2025  |  Prepared by Finance Division")
                       .FontColor(Color.Grey.Medium).AlignCenter();

                    col.Item().PaddingTop(6).LineHorizontal(1.5, accent);
                });

                // ── Content ──────────────────────────────────────────────────────
                page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                {
                    col.Spacing(8);

                    // Helper: coloured section heading
                    void H(string title) =>
                        col.Item().MarginVertical(6).Background(light).Padding(6)
                           .Text(title).Bold().FontSize(13).FontColor(accent);

                    // 1. Executive summary
                    H("1. Executive Summary");

                    col.Item().Text(
                        "This report summarises the financial and operational performance of the " +
                        "organisation for fiscal year 2025. Overall revenue increased by 12% " +
                        "year-on-year while operating costs were held flat, resulting in an improved " +
                        "EBITDA margin of 28%. The board considers these results to be in line with " +
                        "the medium-term strategic plan approved in 2023.").Justify();

                    col.Item().Text(t =>
                    {
                        t.Span("Key highlight: ").Bold();
                        t.Span("Net profit reached $4.2 M, the highest in the company's history.");
                    });

                    // 2. Text formatting
                    H("2. Text Formatting Showcase");

                    col.Item().Text(t =>
                    {
                        t.Span("Normal  ");
                        t.Span("Bold  ").Bold();
                        t.Span("Italic  ").Italic();
                        t.Span("Strikethrough  ").Strikethrough();
                        t.Span("Coloured  ").FontColor(Color.Blue.Medium);
                        t.Span("Large").FontSize(16).FontColor(accent);
                    });

                    col.Item().AlignCenter()
                       .Text("Centred heading text").Bold().FontSize(13).FontColor(accent);

                    col.Item().AlignRight()
                       .Text("Right-aligned annotation").Italic().FontColor(Color.Grey.Darken1);

                    // 3. Table
                    H("3. Department Revenue Summary");

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });

                        table.HeaderRow(row =>
                        {
                            row.Cell().Background(accent).Padding(5).Text("Department").Bold().FontColor(Color.White);
                            row.Cell().Background(accent).Padding(5).AlignRight().Text("Q1-Q2 ($K)").Bold().FontColor(Color.White);
                            row.Cell().Background(accent).Padding(5).AlignRight().Text("Q3-Q4 ($K)").Bold().FontColor(Color.White);
                            row.Cell().Background(accent).Padding(5).AlignRight().Text("Total ($K)").Bold().FontColor(Color.White);
                        });

                        (string Name, string H1, string H2, string Tot)[] depts =
                        [
                            ("Sales",       "1,840", "2,110", "3,950"),
                            ("Engineering", "  620", "  580", "1,200"),
                            ("Marketing",   "  310", "  290", "  600"),
                            ("Support",     "  200", "  210", "  410"),
                        ];

                        bool shade = false;
                        foreach (var d in depts)
                        {
                            string bg = shade ? light : Color.White;
                            table.Row(row =>
                            {
                                row.Cell().Background(bg).Padding(5).Text(d.Name);
                                row.Cell().Background(bg).Padding(5).AlignRight().Text(d.H1);
                                row.Cell().Background(bg).Padding(5).AlignRight().Text(d.H2);
                                row.Cell().Background(bg).Padding(5).AlignRight().Text(d.Tot).Bold();
                            });
                            shade = !shade;
                        }
                    });

                    // 4. Disclaimer
                    H("4. Notes & Disclaimers");

                    col.Item().Text(
                        "All figures are unaudited and subject to revision. Currency amounts are " +
                        "expressed in thousands of US dollars (USD) unless otherwise stated. " +
                        "This document is intended for internal use only and must not be distributed " +
                        "to third parties without prior written consent.")
                       .Justify().FontColor(Color.Grey.Darken1).FontSize(10);
                });

                // ── Footer ───────────────────────────────────────────────────────
                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(1, accent);
                    col.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem()
                           .Text("(c) 2025 Acme Corporation - Confidential")
                           .FontColor(Color.Grey.Medium).FontSize(9);

                        row.AutoItem().AlignRight().Text(t =>
                        {
                            t.Span("Page ").FontSize(9).FontColor(Color.Grey.Medium);
                            t.CurrentPageNumber().FontSize(9).FontColor(Color.Grey.Medium);
                            t.Span(" of ").FontSize(9).FontColor(Color.Grey.Medium);
                            t.TotalPages().FontSize(9).FontColor(Color.Grey.Medium);
                        });
                    });
                });
            });
        }).PublishPdf(path);

        Console.WriteLine($"  [1] Simple report         -> {path}");
    }
}
