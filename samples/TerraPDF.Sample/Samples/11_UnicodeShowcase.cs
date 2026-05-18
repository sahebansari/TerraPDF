using TerraPDF.Core;
using TerraPDF.Helpers;

namespace TerraPDF.Sample.Samples;

// =============================================================================
//  11. UNICODE / WINANSIENCODIG SHOWCASE
//  Shows: accented Latin characters (À–ÿ), Windows-1252 typographic specials
//  (curly quotes, dashes, ellipsis, bullet, euro, trade-mark …), European
//  multilingual body text, mixed styled spans, and a full WinAnsiEncoding
//  character-reference grid.
//
//  All measurement and rendering goes through the extended WinAnsiEncoding width
//  tables, so every glyph — not just printable ASCII — wraps and aligns correctly.
// =============================================================================
internal static class UnicodeShowcase
{
    internal static void Generate(string path)
    {
        // ── Palette ───────────────────────────────────────────────────────────
        const string brand      = "#1A237E";   // deep indigo
        const string brandLight = "#3949AB";   // medium indigo
        const string accent     = "#E65100";   // deep orange
        const string light      = "#E8EAF6";   // lavender tint
        const string stripe     = "#F5F5F5";   // alternating table row
        const string muted      = "#546E7A";   // blue-grey
        const string white      = "#FFFFFF";
        const string gridLine   = "#CFD8DC";

        // ── European language sample data ─────────────────────────────────────
        (string Lang, string Native, string Sample)[] languages =
        [
            ("English",    "English",    "The quick brown fox jumps over the lazy dog."),
            ("French",     "Français",   "Le café au lait est servi avec des croissants frais."),
            ("German",     "Deutsch",    "In der Münchener Straße öffnete das Schloß früh."),
            ("Spanish",    "Español",    "El señor Martínez cruzó la montaña en cañón."),
            ("Portuguese", "Português",  "O coração não mente — é a essência da nação."),
            ("Italian",    "Italiano",   "La città di Venezia è più bella al tramonto."),
            ("Dutch",      "Nederlands", "Ögenschijnlijk is het zoiets als déjà-vu."),
            ("Romanian",   "Romana",     "Soarele rãsare în fiecare dimineatã peste dealuri."),
            ("Hungarian",  "Magyar",     "Gyula és Ágnes sétáltak a városban, ahol a fák zöldek."),
            ("Norwegian",  "Norsk",      "Bjørn gikk på fjordstien i Ålesund om kvelden."),
            ("Swedish",    "Svenska",    "Björk och Åsa åkte till Göteborg på lördag."),
            ("Finnish",    "Suomi",      "Yöllä tähtitaivas heijastuu järven ylle."),
            ("Polish",     "Polski",     "Na starym wzgórzu stoi zamek, a niebo jest modre i jasne."),
            ("Czech",      "Cestina",    "Praha je zlaté srdce Evropy — slavná, stará a nádherná."),
            ("Turkish",    "Türkçe",     "Türk kültürü zengin ve güzel — müzik, dans ve sanat bir arada."),
            ("Catalan",    "Català",     "El Barça jugà a Girona i guanyà per sis a zero."),
            ("Danish",     "Dansk",      "Søren Ørsted stod på broen og så på åen."),
            ("Welsh",      "Cymraeg",    "Mae'r iâ ar y mynydd yn toddi yn yr haf."),
        ];

        // ── Windows-1252 typographic specials ─────────────────────────────────
        (string Uni, string Byte, string Glyph, string Name, string Context)[] specials =
        [
            ("U+2018", "0x91", "\u2018", "Left single quotation mark",  "\u2018Hello\u2019, he said."),
            ("U+2019", "0x92", "\u2019", "Right single quotation mark", "It\u2019s a fine day."),
            ("U+201C", "0x93", "\u201C", "Left double quotation mark",  "\u201CShe smiled.\u201D"),
            ("U+201D", "0x94", "\u201D", "Right double quotation mark", "Said \u201Cmerci\u201D."),
            ("U+2013", "0x96", "\u2013", "En dash",                     "Pages 42\u201347."),
            ("U+2014", "0x97", "\u2014", "Em dash",                     "Time\u2014and tide\u2014wait."),
            ("U+2026", "0x85", "\u2026", "Horizontal ellipsis",         "To be continued\u2026"),
            ("U+2022", "0x95", "\u2022", "Bullet",                      "\u2022 First item"),
            ("U+20AC", "0x80", "\u20AC", "Euro sign",                   "Price: \u20AC 1,299.00"),
            ("U+2122", "0x99", "\u2122", "Trade mark sign",             "TerraPDF\u2122"),
            ("U+0152", "0x8C", "\u0152", "OE ligature (capital)",       "\u0152uvre complète"),
            ("U+0153", "0x9C", "\u0153", "OE ligature (small)",         "Man\u0153uvre"),
            ("U+2020", "0x86", "\u2020", "Dagger",                      "See note \u2020 below."),
            ("U+2021", "0x87", "\u2021", "Double dagger",               "Also \u2021 ibid."),
            ("U+2030", "0x89", "\u2030", "Per mille sign",              "Error rate: 1.2\u2030"),
            ("U+0160", "0x8A", "\u0160", "S with caron (capital)",      "\u0160koda Auto"),
            ("U+0161", "0x9A", "\u0161", "S with caron (small)",        "Du\u0161an"),
        ];

        // ── Latin-1 Supplement character groups ───────────────────────────────
        (string Heading, string Chars)[] latinGroups =
        [
            ("Punctuation & Spacing",
                "\u00A0 \u00AB \u00BB \u00BF \u00A1 \u00B7 \u00B6 \u00A7"),
            ("Mathematical & Technical",
                "\u00B1 \u00D7 \u00F7 \u00B0 \u00B5 \u00B2 \u00B3 \u00BC \u00BD \u00BE"),
            ("Currency & Commerce",
                "\u00A2 \u00A3 \u00A4 \u00A5 \u00A6 \u00A9 \u00AE \u00AF"),
            ("Accented Capitals (À–Ö)",
                "À Á Â Ã Ä Å Æ Ç È É Ê Ë Ì Í Î Ï Ð Ñ Ò Ó Ô Õ Ö"),
            ("Accented Capitals (Ø–þ)",
                "Ø Ù Ú Û Ü Ý Þ ß"),
            ("Accented Lowercase (à–ö)",
                "à á â ã ä å æ ç è é ê ë ì í î ï ð ñ ò ó ô õ ö"),
            ("Accented Lowercase (ø–ÿ)",
                "ø ù ú û ü ý þ ÿ"),
        ];

        // ── WinAnsiEncoding reference grid helpers ────────────────────────────
        // Returns true for defined byte positions in WinAnsiEncoding.
        static bool IsDefinedWinAnsi(int b) =>
            b is >= 0x20 and <= 0x7E ||
            (b >= 0x80 && b <= 0xFF &&
             b is not 0x7F and not 0x81 and not 0x8D and not 0x8F and not 0x90 and not 0x9D);

        // Maps a WinAnsi byte value to its display Unicode character.
        static char WinAnsiToChar(int b) => b switch
        {
            0x80 => '\u20AC', 0x82 => '\u201A', 0x83 => '\u0192', 0x84 => '\u201E',
            0x85 => '\u2026', 0x86 => '\u2020', 0x87 => '\u2021', 0x88 => '\u02C6',
            0x89 => '\u2030', 0x8A => '\u0160', 0x8B => '\u2039', 0x8C => '\u0152',
            0x8E => '\u017D', 0x91 => '\u2018', 0x92 => '\u2019', 0x93 => '\u201C',
            0x94 => '\u201D', 0x95 => '\u2022', 0x96 => '\u2013', 0x97 => '\u2014',
            0x98 => '\u02DC', 0x99 => '\u2122', 0x9A => '\u0161', 0x9B => '\u203A',
            0x9C => '\u0153', 0x9E => '\u017E', 0x9F => '\u0178',
            _ => (char)b,
        };

        // ── Shared local helpers ──────────────────────────────────────────────

        // Shared page header
        void PageHeader(PageDescriptor page, string subtitle)
        {
            page.Header().Column(col =>
            {
                col.Item().Background(brand).PaddingVertical(10).PaddingHorizontal(14)
                   .Row(hdr =>
                   {
                       hdr.RelativeItem().AlignMiddle()
                          .Text("TerraPDF — Unicode & WinAnsiEncoding Showcase")
                          .Bold().FontSize(16).FontColor(Color.White);
                       hdr.AutoItem().AlignRight().AlignMiddle()
                          .Text(subtitle).FontSize(10).FontColor(light);
                   });
                // Thin accent stripe via fixed-height canvas
                col.Item().Canvas(3, _ => { });
            });
        }

        // Shared page footer
        void PageFooter(PageDescriptor page)
        {
            page.Footer().Column(f =>
            {
                f.Item().LineHorizontal(0.5, gridLine);
                f.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem()
                       .Text("TerraPDF — Unicode & WinAnsiEncoding Showcase")
                       .FontSize(8).FontColor(muted);
                    row.AutoItem().AlignRight().Text(t =>
                    {
                        t.Span("Page ").FontSize(8).FontColor(muted);
                        t.CurrentPageNumber().FontSize(8).FontColor(brand);
                        t.Span(" / ").FontSize(8).FontColor(muted);
                        t.TotalPages().FontSize(8).FontColor(brand);
                    });
                });
            });
        }             

        // Caption below a block
        void Caption(TerraPDF.Infra.IContainer c, string text) =>
            c.PaddingTop(3).AlignCenter()
             .Text(text).FontSize(8).FontColor(muted).Italic();

        Document.Create(doc =>
        {
            // ── Metadata ──────────────────────────────────────────────────────
            doc.MetadataTitle("TerraPDF — Unicode & WinAnsiEncoding Showcase");
            doc.MetadataAuthor("TerraPDF Engineering Team");
            doc.MetadataSubject(
                "Demonstrates full WinAnsiEncoding support: accented Latin, " +
                "Windows-1252 typographic specials, and multilingual body text.");
            doc.MetadataKeywords("pdf; unicode; winAnsi; latin; typography; multilingual");
            doc.MetadataCreator("TerraPDF Sample Generator v1.0");

            // ── PDF bookmarks ─────────────────────────────────────────────────
            doc.Bookmark("European Language Coverage",       1);
            doc.Bookmark("Typographic Specials & Symbols",   2);
            doc.Bookmark("WinAnsiEncoding Reference Grid",   3);

            // ==================================================================
            //  PAGE 1 — European Language Coverage
            // ==================================================================
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(10));

                PageHeader(page, "European Language Coverage");
                PageFooter(page);

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(10);

                    // ── Feature overview banner ──────────────────────────────
                    col.Item().Component(new UnicodeSectionHeader(brand,
                        "\u25B6  WinAnsiEncoding — Feature Overview"));

                    col.Item().Background(light).Border(0.5, gridLine).Padding(10).Column(box =>
                    {
                        box.Item().Text("What this showcase demonstrates")
                           .Bold().FontSize(11).FontColor(brand);
                        box.Item().PaddingTop(5).Text(t =>
                        {
                            t.Span("TerraPDF ships with ").FontColor(muted);
                            t.Span("extended WinAnsiEncoding width tables").Bold().FontColor(brand);
                            t.Span(
                                " covering the full byte range 32\u2013255 \u2014 not just printable ASCII. " +
                                "Every accented character (À\u2013ÿ), every Windows-1252 " +
                                "typographic special (\u20AC, \u2026, \u2018, \u2019, \u201C, \u201D, " +
                                "\u2013, \u2014, \u2122) and every Latin-1 symbol is measured with its " +
                                "exact ").FontColor(muted);
                            t.Span("Adobe AFM glyph width").Bold().FontColor(brand);
                            t.Span(
                                " so word-wrapping and justification stay pixel-perfect in any language.")
                                .FontColor(muted);
                        });
                        box.Item().PaddingTop(6).Text(t =>
                        {
                            t.Span("Content-stream encoding: ").Bold().FontColor(brand);
                            t.Span(
                                "Non-ASCII characters are octal-escaped (\\nnn) in PDF string literals, " +
                                "keeping the content stream pure ASCII while the reader resolves each byte " +
                                "via the font\u2019s /WinAnsiEncoding vector. " +
                                "Metadata and bookmark titles use UTF-16BE hex strings (<FEFF\u2026>) " +
                                "so viewer title bars and outline panels show the correct text too.")
                                .FontColor(muted);
                        });
                    });

                    // ── Language table ───────────────────────────────────────
                    col.Item().Component(new UnicodeSectionHeader(brand,
                        "\u25B6  Eighteen European Languages — Correct Rendering & Line-Wrapping"));

                    col.Item().Table(tbl =>
                    {
                        tbl.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(70);   // Language
                            c.ConstantColumn(72);   // Native name
                            c.RelativeColumn();     // Sample sentence
                        });

                        tbl.HeaderRow(row =>
                        {
                            row.Cell().Background(brandLight).Padding(5)
                               .Text("Language").Bold().FontSize(9).FontColor(Color.White);
                            row.Cell().Background(brandLight).Padding(5)
                               .Text("Native Name").Bold().FontSize(9).FontColor(Color.White);
                            row.Cell().Background(brandLight).Padding(5)
                               .Text("Sample Sentence (illustrates WinAnsi glyph coverage)")
                               .Bold().FontSize(9).FontColor(Color.White);
                        });

                        for (int i = 0; i < languages.Length; i++)
                        {
                            var (lang, native, sample) = languages[i];
                            string bg = i % 2 == 0 ? white : stripe;
                            tbl.Row(row =>
                            {
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(5)
                                   .Text(lang).FontSize(9).FontColor(muted);
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(5)
                                   .Text(native).Bold().FontSize(9).FontColor(brand);
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(5)
                                   .Text(sample).FontSize(9).FontColor(Color.Black);
                            });
                        }
                    });

                    Caption(col.Item(),
                        "All 18 sentences rendered using Helvetica/Times via WinAnsiEncoding " +
                        "— glyph widths sourced from Adobe Core-14 AFM tables.");
                });
            });

            // ==================================================================
            //  PAGE 2 — Typographic Specials & Latin-1 Symbols
            // ==================================================================
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(10));

                PageHeader(page, "Typographic Specials & Symbols");
                PageFooter(page);

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(12);

                    // ── Windows-1252 specials table ──────────────────────────
                    col.Item().Component(new UnicodeSectionHeader(brand,
                        "\u25B6  Windows-1252 Specials (byte range 0x80\u20130x9F)"));

                    col.Item().Text(
                        "Unicode code points U+0080\u2013U+009F are C1 controls and are never " +
                        "mapped to WinAnsi. Instead, bytes 0x80\u20130x9F encode 27 distinct " +
                        "characters \u2014 each with its own Unicode code point and AFM advance width.")
                        .FontSize(9).FontColor(muted);

                    col.Item().Table(tbl =>
                    {
                        tbl.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.2f); // Unicode
                            c.RelativeColumn(1.0f); // WinAnsi byte
                            c.RelativeColumn(0.6f); // Glyph
                            c.RelativeColumn(2.2f); // Name
                            c.RelativeColumn(3.0f); // Example
                        });

                        tbl.HeaderRow(row =>
                        {
                            row.Cell().Background(brandLight).Padding(5)
                               .Text("Unicode").Bold().FontSize(8).FontColor(Color.White);
                            row.Cell().Background(brandLight).Padding(5)
                               .Text("WinAnsi").Bold().FontSize(8).FontColor(Color.White);
                            row.Cell().Background(brandLight).Padding(5).AlignCenter()
                               .Text("Glyph").Bold().FontSize(8).FontColor(Color.White);
                            row.Cell().Background(brandLight).Padding(5)
                               .Text("Name").Bold().FontSize(8).FontColor(Color.White);
                            row.Cell().Background(brandLight).Padding(5)
                               .Text("Example in context").Bold().FontSize(8).FontColor(Color.White);
                        });

                        for (int i = 0; i < specials.Length; i++)
                        {
                            var (uni, wb, glyph, name, ctx) = specials[i];
                            string bg = i % 2 == 0 ? white : stripe;
                            tbl.Row(row =>
                            {
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(5)
                                   .Text(uni).FontSize(8).FontColor(muted);
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(5)
                                   .Text(wb).FontSize(8).FontColor(muted);
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(4).AlignCenter()
                                   .Text(glyph).Bold().FontSize(13).FontColor(accent);
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(5)
                                   .Text(name).FontSize(8).FontColor(Color.Black);
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(5)
                                   .Text(ctx).FontSize(8).FontColor(brandLight);
                            });
                        }
                    });

                    // ── Running-prose demonstration ──────────────────────────
                    col.Item().Component(new UnicodeSectionHeader(brand,
                        "\u25B6  Typographic Specials in Running Prose"));

                    col.Item().Background(light).Border(0.5, gridLine).Padding(10).Column(prose =>
                    {
                        prose.Item().Text(t =>
                        {
                            t.Span("Curly quotes: ").Bold().FontColor(brand);
                            t.Span("\u2018Single\u2019 and \u201Cdouble\u201D quotation marks " +
                                   "look far more professional than straight ASCII apostrophes.")
                               .FontColor(muted);
                        });
                        prose.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span("Dashes & ellipsis: ").Bold().FontColor(brand);
                            t.Span("An en\u2013dash separates page ranges (pp.\u00A042\u201347). " +
                                   "An em\u2014dash sets off a clause\u2014without spaces. " +
                                   "An ellipsis\u2026 trails off elegantly.").FontColor(muted);
                        });
                        prose.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span("Symbols: ").Bold().FontColor(brand);
                            t.Span("Copyright \u00A9 2025, registered trademark \u00AE, " +
                                   "trade mark \u2122. Price: \u20AC 1\u202F299.00. " +
                                   "Temperature: 23\u00B0C \u00B1 0.5\u00B0. " +
                                   "3\u00B2 + 4\u00B2 = 5\u00B2 (Pythagorean triple). " +
                                   "Rate: 2.4\u2030 per mille.").FontColor(muted);
                        });
                        prose.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span("Ligatures & AE: ").Bold().FontColor(brand);
                            t.Span("The \u0152uvre of \u00C6schylus \u2014 " +
                                   "man\u0153uvre, n\u0153ud, \u00E6on \u2014 " +
                                   "all rendered from WinAnsiEncoding glyphs.").FontColor(muted);
                        });
                    });

                    // ── Latin-1 supplement groups ────────────────────────────
                    col.Item().Component(new UnicodeSectionHeader(brand,
                        "\u25B6  Latin-1 Supplement Symbol Groups (U+00A0\u2013U+00FF)"));

                    col.Item().Table(tbl =>
                    {
                        tbl.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(130);
                            c.RelativeColumn();
                        });

                        tbl.HeaderRow(row =>
                        {
                            row.Cell().Background(brandLight).Padding(5)
                               .Text("Group").Bold().FontSize(8).FontColor(Color.White);
                            row.Cell().Background(brandLight).Padding(5)
                               .Text("Characters").Bold().FontSize(8).FontColor(Color.White);
                        });

                        for (int i = 0; i < latinGroups.Length; i++)
                        {
                            var (heading, chars) = latinGroups[i];
                            string bg = i % 2 == 0 ? white : stripe;
                            tbl.Row(row =>
                            {
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(5)
                                   .Text(heading).FontSize(8).FontColor(muted);
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(5)
                                   .Text(chars).Bold().FontSize(11).FontColor(accent);
                            });
                        }
                    });
                });
            });

            // ==================================================================
            //  PAGE 3 — Full WinAnsiEncoding Character Reference Grid
            // ==================================================================
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(10));

                PageHeader(page, "WinAnsiEncoding Reference Grid");
                PageFooter(page);

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Component(new UnicodeSectionHeader(brand,
                        "\u25B6  WinAnsiEncoding — Complete Byte-to-Glyph Reference (0x20\u20130xFF)"));

                    col.Item().Text(
                        "Every cell shows the rendered glyph, its decimal byte value, and Unicode " +
                        "code point. Grey cells are undefined positions. Bytes 0x80\u20130x9F are " +
                        "Windows-1252 specials; 0xA0\u20130xFF are Latin-1 Supplement.")
                        .FontSize(8).FontColor(muted);

                    // Grid: 16 glyph columns per row, rows covering 0x20–0xFF.
                    const int gridCols = 16;

                    col.Item().Table(tbl =>
                    {
                        tbl.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(24); // row label
                            for (int ci = 0; ci < gridCols; ci++)
                                c.RelativeColumn();
                        });

                        // ── Column header row (nibble offsets +0 … +F) ───────
                        tbl.Row(row =>
                        {
                            // Top-left corner (blank label cell)
                            row.Cell().Background(brand).Padding(3);
                            for (int ci = 0; ci < gridCols; ci++)
                                row.Cell().Background(brand).Padding(3).AlignCenter()
                                   .Text($"+{ci:X}").Bold().FontSize(7).FontColor(Color.White);
                        });

                        // ── Data rows ────────────────────────────────────────
                        int numRows = (0xFF - 0x20 + 1 + gridCols - 1) / gridCols; // = 15
                        for (int r = 0; r < numRows; r++)
                        {
                            int rowBase = 0x20 + r * gridCols;
                            tbl.Row(row =>
                            {
                                // Row label (e.g. "0x30")
                                row.Cell().Background(brand).Padding(3).AlignCenter()
                                   .Text($"0x{rowBase:X2}").Bold().FontSize(6).FontColor(Color.White);

                                for (int ci = 0; ci < gridCols; ci++)
                                {
                                    int b = rowBase + ci;

                                    if (b > 0xFF)
                                    {
                                        row.Cell(); // empty trailing cell
                                        continue;
                                    }

                                    if (!IsDefinedWinAnsi(b))
                                    {
                                        // Undefined — grey empty cell
                                        row.Cell().Background("#E0E0E0").Border(0.3, gridLine)
                                           .Padding(2).AlignCenter()
                                           .Text("\u2014").FontSize(7).FontColor(Color.Grey.Medium);
                                        continue;
                                    }

                                    char glyph     = WinAnsiToChar(b);
                                    int  codePoint = glyph;

                                    // Colour-code by section
                                    string bg = b <= 0x7E
                                        ? white       // printable ASCII
                                        : b <= 0x9F
                                            ? "#FFF8E1" // Win-1252 specials (amber)
                                            : "#E8F5E9"; // Latin-1 supplement (green)

                                    row.Cell().Background(bg).Border(0.3, gridLine).Padding(2)
                                       .Column(cell =>
                                       {
                                           // Some WinAnsi glyphs (e.g. NBSP U+00A0, soft-hyphen U+00AD)
                                           // are whitespace-only strings — substitute a visible indicator.
                                           string glyphStr = string.IsNullOrWhiteSpace(glyph.ToString())
                                               ? "\u00B7"   // middle dot as placeholder
                                               : glyph.ToString();

                                           cell.Item().AlignCenter()
                                               .Text(glyphStr).Bold().FontSize(9).FontColor(brand);
                                           cell.Item().AlignCenter()
                                               .Text($"{b}")
                                               .FontSize(5).FontColor(muted);
                                           cell.Item().AlignCenter()
                                               .Text($"U+{codePoint:X4}")
                                               .FontSize(5).FontColor(muted);
                                       });
                                }
                            });
                        }
                    });

                    // ── Colour legend ─────────────────────────────────────────
                    col.Item().PaddingTop(6).Column(legend =>
                    {
                        void LegendEntry(string bg, string label)
                        {
                            legend.Item().MarginBottom(5).Row(r =>
                            {
                                r.ConstantItem(12).Canvas(12, c =>
                                    c.StrokeRect(0, 0, 12, 12, gridLine).FillRect(0, 0, 12, 12, bg));
                                r.AutoItem().PaddingLeft(3).MarginBottom(2)
                                 .Text(label).FontSize(7).FontColor(muted);
                            });
                        }

                        LegendEntry("#FCFCFC",    "Printable ASCII (0x20\u20130x7E)");
                        LegendEntry("#FFF8E1", "Win-1252 Specials (0x80\u20130x9F)");
                        LegendEntry("#E8F5E9", "Latin-1 Supplement (0xA0\u20130xFF)");
                        LegendEntry("#E0E0E0", "Undefined");
                    });

                    Caption(col.Item(),
                        "All 224 defined WinAnsiEncoding positions (32\u2013255) \u2014 " +
                        "each glyph measured with its Adobe AFM advance width.");
                });
            });

        }).PublishPdf(path);

        Console.WriteLine($"  [11] Unicode / WinAnsi showcase -> {path}");
    }
}

// ---------------------------------------------------------------------------
//  Reusable component for this sample: coloured section heading band
// ---------------------------------------------------------------------------
internal sealed class UnicodeSectionHeader(string hexColor, string title)
    : TerraPDF.Infra.IComponent
{
    public void Compose(TerraPDF.Infra.IContainer container) =>
        container.Background(hexColor).PaddingVertical(5).PaddingHorizontal(10)
                 .Text(title).Bold().FontSize(10).FontColor(Color.White);
}
