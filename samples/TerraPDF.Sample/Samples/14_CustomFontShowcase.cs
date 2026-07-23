using TerraPDF.Core;
using TerraPDF.Helpers;

namespace TerraPDF.Sample.Samples;

// =============================================================================
//  14. CUSTOM FONT EMBEDDING SHOWCASE
//  Shows: FontFamily.Register(...) loading a real TrueType font (Lato, SIL Open
//  Font License) as regular + bold variants, full Unicode text (Cyrillic, Greek)
//  that the three built-in WinAnsiEncoding fonts cannot render, and a direct
//  side-by-side comparison against a standard font falling back to "?".
// =============================================================================
internal static class CustomFontShowcase
{
    internal static void Generate(string path, string regularFontPath, string boldFontPath)
    {
        const string brand   = "#004D40"; // deep teal
        const string accent  = "#00695C";
        const string light   = "#E0F2F1";
        const string muted   = "#455A64";
        const string gridLine = "#B2DFDB";

        // Registering is process-wide and thread-safe — call once (e.g. at
        // startup) and reference "Lato" by name from any document afterwards.
        FontFamily.Register("Lato", regularFontPath);
        FontFamily.Register("Lato", boldFontPath, bold: true);

        (string Language, string Text)[] multilingual =
        [
            ("English",  "TerraPDF now embeds real TrueType fonts."),
            ("Russian",  "TerraPDF теперь встраивает настоящие TrueType-шрифты."),
            ("Greek",    "Το TerraPDF ενσωματώνει πλέον πραγματικές γραμματοσειρές TrueType."),
            ("Bulgarian","TerraPDF вече вгражда истински TrueType шрифтове."),
        ];

        Document.Create(doc =>
        {
            doc.MetadataTitle("TerraPDF — Custom Font Embedding Showcase");
            doc.MetadataAuthor("TerraPDF Engineering Team");
            doc.MetadataSubject(
                "Demonstrates FontFamily.Register: embedding a TrueType font and " +
                "rendering full-Unicode text (Cyrillic, Greek) beyond WinAnsiEncoding.");
            doc.MetadataKeywords("pdf; fonts; truetype; embedding; unicode; cyrillic; greek");

            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Background(brand).PaddingVertical(10).PaddingHorizontal(14)
                       .Text("TerraPDF — Custom Font Embedding").Bold().FontSize(16).FontColor(Color.White);
                    col.Item().Canvas(3, _ => { });
                });

                page.Footer().Column(f =>
                {
                    f.Item().LineHorizontal(0.5, gridLine);
                    f.Item().PaddingTop(4).AlignCenter()
                     .Text("TerraPDF — 2.0.0").FontSize(8).FontColor(muted);
                });

                page.Content().PaddingTop(14).Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Background(light).Border(0.5, gridLine).Padding(12).Column(box =>
                    {
                        box.Item().Text("FontFamily.Register(...)").Bold().FontSize(13).FontColor(brand);
                        box.Item().PaddingTop(6).Text(t =>
                        {
                            t.Span("This document registers the ").FontColor(muted);
                            t.Span("Lato").Bold().FontColor(brand);
                            t.Span(" family (SIL Open Font License) as a regular and bold " +
                                   "TrueType variant, then uses it exactly like a built-in family via ")
                                .FontColor(muted);
                            t.Span("TextStyle.FontFamily(\"Lato\")").Bold().FontColor(accent);
                            t.Span(". The whole font file is embedded in the PDF (no subsetting yet), " +
                                   "so it renders identically in any conforming viewer — no system " +
                                   "font installation required.").FontColor(muted);
                        });
                    });

                    col.Item().Text("Custom font, regular weight")
                       .Bold().FontSize(12).FontColor(brand).FontFamily("Lato");
                    col.Item().Text("The quick brown fox jumps over the lazy dog.")
                       .FontFamily("Lato").FontSize(12);

                    col.Item().Text("Custom font, bold weight")
                       .Bold().FontSize(12).FontColor(brand).FontFamily("Lato");
                    col.Item().Text("The quick brown fox jumps over the lazy dog.")
                       .FontFamily("Lato").Bold().FontSize(12);

                    col.Item().Text("Full Unicode: Cyrillic & Greek script")
                       .Bold().FontSize(12).FontColor(brand).FontFamily("Lato");

                    col.Item().Table(tbl =>
                    {
                        tbl.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(80);
                            c.RelativeColumn();
                        });

                        tbl.HeaderRow(row =>
                        {
                            row.Cell().Background(accent).Padding(5)
                               .Text("Language").Bold().FontSize(9).FontColor(Color.White);
                            row.Cell().Background(accent).Padding(5)
                               .Text("Rendered with the embedded \"Lato\" font")
                               .Bold().FontSize(9).FontColor(Color.White);
                        });

                        for (int i = 0; i < multilingual.Length; i++)
                        {
                            var (lang, sample) = multilingual[i];
                            string bg = i % 2 == 0 ? Color.White : light;
                            tbl.Row(row =>
                            {
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(6)
                                   .Text(lang).FontSize(9).FontColor(muted);
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(6)
                                   .Text(sample).FontFamily("Lato").FontSize(11).FontColor(Color.Black);
                            });
                        }
                    });

                    col.Item().Text("Same text, standard Helvetica (WinAnsiEncoding only)")
                       .Bold().FontSize(12).FontColor(brand);

                    col.Item().Background(light).Border(0.5, gridLine).Padding(10).Column(fallback =>
                    {
                        foreach (var (lang, sample) in multilingual[1..])
                        {
                            fallback.Item().Text(t =>
                            {
                                t.Span($"{lang}: ").Bold().FontColor(muted).FontSize(9);
                                // No FontFamily("Lato") here — Helvetica has no Cyrillic/Greek
                                // glyphs, so every such character substitutes as "?" (see
                                // WinAnsiEncoding.TryGetByte / PdfPage.EscapeForPdfString).
                                t.Span(sample).FontSize(11).FontColor(Color.Black);
                            });
                        }
                        fallback.Item().PaddingTop(4).Text(
                            "Without a registered font covering these scripts, unmappable " +
                            "characters fall back to \"?\" — exactly what the rows above avoid.")
                            .Italic().FontSize(8).FontColor(muted);
                    });
                });
            });
        }).PublishPdf(path);

        Console.WriteLine($"  [14] Custom font embedding showcase -> {path}");
    }
}
