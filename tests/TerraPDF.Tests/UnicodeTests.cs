using TerraPDF.Core;
using TerraPDF.Drawing;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for Unicode / WinAnsiEncoding support across the full pipeline:
/// <list type="number">
///   <item><see cref="WinAnsiEncoding.TryGetByte"/> — mapping correctness</item>
///   <item><see cref="FontMetrics.MeasureWidth"/> — extended width tables (Latin-1 + Win-1252 specials)</item>
///   <item><see cref="PdfPage.EscapeForPdfString"/> — content-stream encoding of non-ASCII characters</item>
///   <item>End-to-end document generation with non-ASCII text does not throw</item>
/// </list>
/// </summary>
public sealed class UnicodeTests
{
    // ──────────────────────────────────────────────────────────────────────────
    //  1.  WinAnsiEncoding.TryGetByte
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    // Printable ASCII: byte value equals code point
    [InlineData(' ',  0x20, true)]
    [InlineData('A',  0x41, true)]
    [InlineData('~',  0x7E, true)]
    // DEL (0x7F) — not in WinAnsi
    [InlineData('\u007F', 0x00, false)]
    // Latin-1 Supplement: byte value equals code point
    [InlineData('\u00A0', 0xA0, true)]   // NBSP
    [InlineData('\u00E9', 0xE9, true)]   // é
    [InlineData('\u00C0', 0xC0, true)]   // À
    [InlineData('\u00FF', 0xFF, true)]   // ÿ
    // Windows-1252 specials
    [InlineData('\u20AC', 0x80, true)]   // €
    [InlineData('\u2026', 0x85, true)]   // …
    [InlineData('\u2018', 0x91, true)]   // '
    [InlineData('\u2019', 0x92, true)]   // '
    [InlineData('\u201C', 0x93, true)]   // "
    [InlineData('\u201D', 0x94, true)]   // "
    [InlineData('\u2013', 0x96, true)]   // –
    [InlineData('\u2014', 0x97, true)]   // —
    [InlineData('\u2122', 0x99, true)]   // ™
    [InlineData('\u2022', 0x95, true)]   // •
    // Unmappable (CJK, Arabic)
    [InlineData('\u4E2D', 0x00, false)]  // 中
    [InlineData('\u0600', 0x00, false)]  // Arabic U+0600
    public void TryGetByteReturnsCorrectMapping(char input, byte expectedByte, bool expectedResult)
    {
        bool result = WinAnsiEncoding.TryGetByte(input, out byte actual);
        Assert.Equal(expectedResult, result);
        if (expectedResult)
            Assert.Equal(expectedByte, actual);
    }

    [Fact]
    public void TryGetByteC1ControlRangeIsNotMapped()
    {
        // U+0080–U+009F are C1 controls in Unicode; they must NOT be treated as
        // WinAnsi bytes 0x80–0x9F.  Those byte positions belong to the
        // Windows-1252 specials (€, ‚, ƒ …) which have different Unicode code points.
        for (char c = '\u0080'; c <= '\u009F'; c++)
        {
            bool mapped = WinAnsiEncoding.TryGetByte(c, out _);
            Assert.False(mapped, $"C1 control U+{(int)c:X4} should not be mapped");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  2.  FontMetrics — extended width tables
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    // é (U+00E9, WinAnsi 0xE9) — Helvetica AFM: 556 units
    [InlineData("é",  10, "Helvetica", false, false, 5.56)]
    // À (U+00C0, WinAnsi 0xC0) — Helvetica AFM: 667 units
    [InlineData("À",  10, "Helvetica", false, false, 6.67)]
    // ü (U+00FC, WinAnsi 0xFC) — Helvetica AFM: 556 units
    [InlineData("ü",  10, "Helvetica", false, false, 5.56)]
    // – en-dash (U+2013, WinAnsi 0x96) — Helvetica AFM: 556 units
    [InlineData("–",  10, "Helvetica", false, false, 5.56)]
    // … ellipsis (U+2026, WinAnsi 0x85) — Helvetica AFM: 1000 units
    [InlineData("…",  10, "Helvetica", false, false, 10.0)]
    // ™ trade mark (U+2122, WinAnsi 0x99) — Helvetica AFM: 1000 units
    [InlineData("™",  10, "Helvetica", false, false, 10.0)]
    // é in Helvetica-Bold — AFM: 556 units
    [InlineData("é",  10, "Helvetica", true,  false, 5.56)]
    // é in Times-Bold — AFM: 444 units
    [InlineData("é",  10, "Times",     true,  false, 4.44)]
    // é in Times-Italic — AFM: 444 units
    [InlineData("é",  10, "Times",     false, true,  4.44)]
    // Unmappable character falls back to 500 units
    [InlineData("中", 10, "Helvetica", false, false, 5.0)]
    public void MeasureWidthSupportsExtendedWinAnsiCharacters(
        string text, double fontSize, string family, bool bold, bool italic, double expectedPts)
    {
        double actual = FontMetrics.MeasureWidth(
            text, fontSize, PdfFonts.Resolve(family), bold, italic);
        Assert.Equal(expectedPts, actual, precision: 1);
    }

    [Fact]
    public void MeasureWidthMixedAsciiAndAccentedSumsCorrectly()
    {
        // "café" = c(500) + a(556) + f(278) + é(556) = 1890 units @ 10 pt = 18.9 pt
        double actual = FontMetrics.MeasureWidth("café", 10, PdfFontFamily.Helvetica, false, false);
        Assert.Equal(18.9, actual, precision: 1);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  3.  PdfPage.EscapeForPdfString — content-stream encoding
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    // Pure ASCII — pass through unchanged (special chars escaped)
    [InlineData("Hello",       "Hello")]
    [InlineData("A(B)C",       "A\\(B\\)C")]
    [InlineData("a\\b",        "a\\\\b")]
    // é (0xE9) → octal \351
    [InlineData("é",           "\\351")]
    // À (0xC0) → octal \300
    [InlineData("À",           "\\300")]
    // NBSP (U+00A0, 0xA0) → octal \240
    [InlineData("\u00A0",      "\\240")]
    // — em-dash (U+2014, WinAnsi 0x97) → octal \227
    [InlineData("—",           "\\227")]
    // € euro (U+20AC, WinAnsi 0x80) → octal \200
    [InlineData("€",           "\\200")]
    // … ellipsis (U+2026, WinAnsi 0x85) → octal \205
    [InlineData("…",           "\\205")]
    // Unmappable → '?'
    [InlineData("中",          "?")]
    // Mixed: "café" → c(c) a(a) f(f) é(\351)
    [InlineData("café",        "caf\\351")]
    public void EscapeForPdfStringEncodesCorrectly(string input, string expected)
    {
        string actual = PdfPage.EscapeForPdfString(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EscapeForPdfStringResultIsAsciiOnly()
    {
        // The output of EscapeForPdfString must never contain bytes > 0x7E so the
        // content stream stays ASCII-safe regardless of input.
        const string mixed = "Héllo Wörld — «café» ™ … €";
        string escaped = PdfPage.EscapeForPdfString(mixed);
        foreach (char c in escaped)
            Assert.True(c <= '\u007E', $"Non-ASCII char U+{(int)c:X4} found in escaped output");
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  4.  End-to-end: document generation with non-ASCII text
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GeneratePdfWithAccentedBodyTextDoesNotThrow()
    {
        byte[] bytes = Document.Create(doc =>
        {
            doc.MetadataTitle("Métriques & Données — Rapport Annuel");
            doc.MetadataAuthor("Ångström Üniversity");
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Column(col =>
                {
                    col.Item().Text("Héllo Wörld — «café au lait»");
                    col.Item().Text("Straße: Münchener Straße 42");
                    col.Item().Text("Ñoño: señor español");
                    col.Item().Text("Português: coração");
                    col.Item().Text("Symbols: © ® ™ € … – —");
                    col.Item().Text("Curly quotes: \u2018single\u2019 \u201Cdouble\u201D");
                    col.Item().Text("Latin ext: Ä Ö Ü ä ö ü ß À Ç Ê Î Ô");
                });
            });
        }).PublishPdf();

        Assert.True(bytes.Length > 0);

        // Verify the PDF header is intact
        string header = System.Text.Encoding.ASCII.GetString(bytes, 0, 8);
        Assert.Equal("%PDF-1.7", header);
    }

    [Fact]
    public void GeneratePdfWithUnicodeMetadataEmbedsBomHexString()
    {
        // Non-ASCII metadata should produce UTF-16BE hex strings (<FEFF...>) in the PDF
        byte[] bytes = Document.Create(doc =>
        {
            doc.MetadataTitle("Titre: Données — «Café»");
            doc.MetadataAuthor("Autheur: André");
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Text("test");
            });
        }).PublishPdf();

        string content = System.Text.Encoding.Latin1.GetString(bytes);

        // The Info dictionary must contain UTF-16BE hex strings for non-ASCII titles
        Assert.Contains("<FEFF", content);
    }

    [Fact]
    public void GeneratePdfWithAsciiOnlyMetadataUsesLiteralString()
    {
        // Pure-ASCII metadata should stay as a plain literal (no BOM hex)
        byte[] bytes = Document.Create(doc =>
        {
            doc.MetadataTitle("Annual Report 2024");
            doc.MetadataAuthor("TerraPDF Team");
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Text("test");
            });
        }).PublishPdf();

        string content = System.Text.Encoding.Latin1.GetString(bytes);

        Assert.Contains("/Title (Annual Report 2024)", content);
        Assert.DoesNotContain("<FEFF", content);
    }
}
