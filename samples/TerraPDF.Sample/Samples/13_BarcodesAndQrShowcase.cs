using TerraPDF.Barcodes;
using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;

namespace TerraPDF.Sample.Samples;

// =============================================================================
//  13. BARCODES & QR CODES SHOWCASE
//
//  Shows: Barcode(...) (Code128, Subset B) and QrCode(...) (ISO/IEC 18004,
//         versions 1-40) — both rendered as vector-filled rectangles, so they
//         stay crisp at any zoom. Demonstrates sizing, colour, error
//         correction levels, captions, and placement inside a Row and a
//         Table cell to show they compose like any other element.
// =============================================================================
internal static class BarcodesAndQrShowcase
{
    private const string Brand   = "#1A3C5E";
    private const string Accent  = "#E87722";
    private const string Muted   = "#7A8A99";
    private const string Light   = "#F4F7FA";
    private const string GridLine = "#D0D8E0";
    private const string White   = "#FFFFFF";
    private const string Success = "#27AE60";

    internal static void Generate(string path)
    {
        Document.Create(doc =>
        {
            doc.MetadataTitle("TerraPDF — Barcodes & QR Codes Showcase");
            doc.MetadataAuthor("TerraPDF Engineering Team");
            doc.MetadataSubject("Demonstrates the Barcode(...) and QrCode(...) Fluent API");
            doc.MetadataKeywords("pdf; barcode; code128; qr code; iso 18004; TerraPDF");
            doc.MetadataCreator("TerraPDF Sample Generator");

            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Background(Brand).PaddingVertical(10).PaddingHorizontal(14)
                       .Text("TerraPDF — Barcodes & QR Codes").Bold().FontSize(15).FontColor(White);
                    col.Item().Canvas(3, _ => { });
                });

                page.Footer().BorderTop(0.5, GridLine).PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text("TerraPDF — Barcodes & QR Codes Showcase").FontSize(8).FontColor(Muted);
                    row.AutoItem().Text(t =>
                    {
                        t.Span("Page ").FontSize(8).FontColor(Muted);
                        t.CurrentPageNumber().FontSize(8).FontColor(Brand).Bold();
                        t.Span(" / ").FontSize(8).FontColor(Muted);
                        t.TotalPages().FontSize(8).FontColor(Brand).Bold();
                    });
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(16);

                    // ── 1. Plain Code128 barcode ──────────────────────────────
                    SectionHeader(col.Item(), "1  Code128 Barcode");
                    col.Item().Background(Light).Border(0.5, GridLine).Padding(14)
                       .Barcode("TERRAPDF-2026");
                    CodeBlock(col.Item(), "container.Barcode(\"TERRAPDF-2026\");");

                    // ── 2. Barcode with caption + custom colour ───────────────
                    SectionHeader(col.Item(), "2  Barcode With Caption & Colour");
                    col.Item().Background(Light).Border(0.5, GridLine).Padding(14)
                       .Barcode("SKU-00042-A", width: 260, height: 50,
                            hexColor: Brand, showCaption: true);
                    CodeBlock(col.Item(),
                        "container.Barcode(\"SKU-00042-A\", width: 260, height: 50,\n"
                      + "    hexColor: \"#1A3C5E\", showCaption: true);");

                    // ── 3. QR codes at different error-correction levels ──────
                    SectionHeader(col.Item(), "3  QR Code Error Correction Levels");
                    col.Item().Background(Light).Border(0.5, GridLine).Padding(14)
                       .Row(row =>
                       {
                           row.Spacing(12);
                           foreach (var level in new[]
                           {
                               QrErrorCorrectionLevel.L, QrErrorCorrectionLevel.M,
                               QrErrorCorrectionLevel.Q, QrErrorCorrectionLevel.H,
                           })
                           {
                               row.RelativeItem().Column(c =>
                               {
                                   c.Item().AlignCenter().QrCode(
                                       "https://terrapdf.example/" + level, size: 90, level: level);
                                   c.Item().PaddingTop(4).AlignCenter()
                                    .Text($"Level {level}").FontSize(8).FontColor(Muted);
                               });
                           }
                       });
                    CodeBlock(col.Item(),
                        "container.QrCode(\"https://terrapdf.example/\", size: 90,\n"
                      + "    level: QrErrorCorrectionLevel.Q);");

                    // ── 4. Custom colours ──────────────────────────────────────
                    SectionHeader(col.Item(), "4  Custom Module & Background Colour");
                    col.Item().Background(Light).Border(0.5, GridLine).Padding(14).Row(row =>
                    {
                        row.Spacing(12);
                        row.AutoItem().QrCode("Brand-coloured QR", size: 100,
                            hexColor: Brand, backgroundHexColor: "#EAF1F8");
                        row.AutoItem().QrCode("Accent-coloured QR", size: 100,
                            hexColor: Accent, backgroundHexColor: White);
                    });

                    // ── 5. Inside a table cell (proves "anywhere" placement) ──
                    SectionHeader(col.Item(), "5  Inside A Table Cell");
                    col.Item().Table(tbl =>
                    {
                        tbl.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(90);
                            c.RelativeColumn(2);
                            c.ConstantColumn(140);
                        });

                        tbl.HeaderRow(row =>
                        {
                            TableHeader(row.Cell(), "QR");
                            TableHeader(row.Cell(), "Product");
                            TableHeader(row.Cell(), "Barcode");
                        });

                        (string Name, string Sku)[] products =
                        [
                            ("Wireless Mouse", "SKU-10231"),
                            ("Mechanical Keyboard", "SKU-10456"),
                        ];

                        foreach (var (name, sku) in products)
                        {
                            tbl.Row(row =>
                            {
                                row.Cell().Padding(6).AlignCenter()
                                   .QrCode($"https://terrapdf.example/p/{sku}", size: 60);
                                row.Cell().Padding(6).AlignMiddle().Column(c =>
                                {
                                    c.Item().Text(name).Bold().FontColor(Brand);
                                    c.Item().Text(sku).FontColor(Muted).FontSize(8);
                                });
                                row.Cell().Padding(6).AlignMiddle()
                                   .Barcode(sku, height: 30);
                            });
                        }
                    });

                    // ── API summary ────────────────────────────────────────────
                    SectionHeader(col.Item(), "API Summary");
                    col.Item().Background(Light).Border(0.5, GridLine).Padding(12).Column(info =>
                    {
                        info.Spacing(4);
                        info.Item().Text(t =>
                        {
                            t.Span("Barcode(data, width?, height, hexColor, backgroundHexColor, showCaption, quietZoneModules)")
                             .FontSize(9).Bold().FontColor(Brand);
                        });
                        info.Item().Text("Code128 (Subset B) — encodes printable ASCII 0x20-0x7E.")
                            .FontSize(9).FontColor(Muted);
                        info.Item().PaddingTop(6).Text(t =>
                        {
                            t.Span("QrCode(data, size?, level, hexColor, backgroundHexColor, quietZoneModules)")
                             .FontSize(9).Bold().FontColor(Brand);
                        });
                        info.Item().Text("ISO/IEC 18004, byte-mode, versions 1-40, all four error correction levels.")
                            .FontSize(9).FontColor(Muted);
                        info.Item().PaddingTop(6).Text(t =>
                        {
                            t.Span("Both render as vector-filled rectangles").Bold().FontColor(Success);
                            t.Span(" — no raster image pipeline, crisp at any zoom, and place inside any container: "
                                 + "Column, Row, Table cell, header, footer.").FontColor(Muted);
                        });
                    });
                });
            });
        }).PublishPdf(path);
        Console.WriteLine($"  [13] Barcodes & QR codes showcase -> {path}");
    }

    // -- Shared helpers -----------------------------------------------------

    private static void SectionHeader(IContainer container, string title) =>
        container.PaddingTop(4).BorderBottom(1.5, Brand).PaddingBottom(4)
                 .Text(title).Bold().FontSize(11).FontColor(Brand);

    private static void TableHeader(IContainer cell, string text) =>
        cell.Background(Brand).Padding(5).Text(text).Bold().FontSize(8).FontColor(White);

    private static void CodeBlock(IContainer container, string code) =>
        container.RoundedBox(4, "#1E2D3D", "#1E2D3D").Padding(10)
                 .Text(code).FontSize(8).FontColor("#A8D8EA");
}
