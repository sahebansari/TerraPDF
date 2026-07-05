using TerraPDF.Core;
using TerraPDF.Drawing;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Unit tests for <see cref="FontMetrics"/>.
/// Verifies that glyph-width measurements match known Adobe AFM values
/// so that text layout calculations stay correct across refactors.
/// </summary>
public sealed class FontMetricsTests
{
    // Each glyph-width value is: (AFM_units / 1000) * fontSize.
    // Helvetica widths at array index (char_code - 32):
    //   'A'(65) -> index 33 -> 667 units   ' '(32) -> index 0 -> 278 units
    //   'H'(72) -> index 40 -> 722 units   'e'(101)-> index 69 -> 556 units
    //   'l'(108)-> index 76 -> 222 units   'o'(111)-> index 79 -> 556 units

    // Family is passed as a string because xUnit test methods must be public
    // while PdfFontFamily is internal; PdfFonts.Resolve maps the name.

    [Theory]
    // ── Helvetica family (bold/italic stay in the same typeface) ──────────────
    [InlineData("A",     10, "Helvetica", false, false, 6.67)]  // Helvetica 'A' = 667
    [InlineData("A",     10, "Helvetica", true,  false, 7.22)]  // Helvetica-Bold 'A' = 722
    [InlineData("A",     10, "Helvetica", false, true,  6.67)]  // Helvetica-Oblique 'A' = 667 (same as upright)
    [InlineData("A",     10, "Helvetica", true,  true,  7.22)]  // Helvetica-BoldOblique 'A' = 722
    [InlineData("a",     10, "Helvetica", true,  false, 5.56)]  // Helvetica-Bold 'a' = 556
    [InlineData(" ",     12, "Helvetica", false, false, 3.336)] // Helvetica space = 278 @ 12pt
    [InlineData("Hello", 10, "Helvetica", false, false, 22.78)] // H(722)+e(556)+l(222)+l(222)+o(556)
    // ── Times family ───────────────────────────────────────────────────────────
    [InlineData("A",     10, "Times",     false, false, 7.22)]  // Times-Roman 'A' = 722
    [InlineData("A",     10, "Times",     true,  false, 7.22)]  // Times-Bold 'A' = 722
    [InlineData("A",     10, "Times",     false, true,  6.11)]  // Times-Italic 'A' = 611
    [InlineData("A",     10, "Times",     true,  true,  6.67)]  // Times-BoldItalic 'A' = 667
    [InlineData("a",     10, "Times",     false, false, 4.44)]  // Times-Roman 'a' = 444
    // ── Courier family (monospaced: everything is 600 units) ───────────────────
    [InlineData("A",     10, "Courier",   false, false, 6.0)]
    [InlineData("i",     10, "Courier",   true,  true,  6.0)]
    [InlineData("Hello", 10, "Courier",   false, false, 30.0)]
    public void MeasureWidthMatchesAdobeAfmValues(
        string text, double fontSize, string family, bool bold, bool italic, double expectedPts)
    {
        double actual = FontMetrics.MeasureWidth(
            text, fontSize, PdfFonts.Resolve(family), bold, italic);
        Assert.Equal(expectedPts, actual, precision: 1);
    }

    [Fact]
    public void MeasureWidthEmptyStringReturnsZero()
    {
        Assert.Equal(0.0, FontMetrics.MeasureWidth("", 12, PdfFontFamily.Helvetica, false, false));
    }

    [Fact]
    public void MeasureWidthControlCharacterUsesFallbackWidth()
    {
        // Characters outside the 32-126 range fall back to 500 units.
        double actual = FontMetrics.MeasureWidth("\x01", 10, PdfFontFamily.Helvetica, false, false);
        Assert.Equal(5.0, actual, precision: 2); // 500 / 1000 * 10
    }

    // ── Family-name resolution ────────────────────────────────────────────────

    [Theory]
    [InlineData(null,              "Helvetica")]
    [InlineData("",                "Helvetica")]
    [InlineData("Helvetica",       "Helvetica")]
    [InlineData("arial",           "Helvetica")]
    [InlineData("Times",           "Times")]
    [InlineData("times new roman", "Times")]
    [InlineData("Times-Roman",     "Times")]
    [InlineData("Courier",         "Courier")]
    [InlineData("courier new",     "Courier")]
    [InlineData("Comic Sans",      "Helvetica")] // unknown → fallback
    public void ResolveMapsFamilyNames(string? name, string expected)
    {
        Assert.Equal(expected, PdfFonts.Resolve(name).ToString());
    }

    // ── Alias mapping (F1–F12) ────────────────────────────────────────────────

    [Theory]
    [InlineData("Helvetica", false, false, "F1")]
    [InlineData("Helvetica", true,  false, "F2")]
    [InlineData("Helvetica", false, true,  "F3")]
    [InlineData("Helvetica", true,  true,  "F4")]
    [InlineData("Times",     false, false, "F5")]
    [InlineData("Times",     true,  false, "F6")]
    [InlineData("Courier",   false, false, "F9")]
    [InlineData("Courier",   true,  true,  "F12")]
    public void AliasSelectsCorrectFontVariant(
        string family, bool bold, bool italic, string expectedAlias)
    {
        Assert.Equal(expectedAlias, PdfFonts.Alias(PdfFonts.Resolve(family), bold, italic));
    }

    // ── End-to-end: rendered documents select the correct variant ────────────

    private static string Render(Action<Infra.IContainer> content)
    {
        byte[] bytes = Document.Create(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            content(p.Content());
        })).PublishPdf();
        return System.Text.Encoding.Latin1.GetString(bytes);
    }

    [Fact]
    public void BoldTextStaysInHelveticaFamily()
    {
        string pdf = Render(c => c.Text("Bold sample").Bold());
        // Bold must select F2 (Helvetica-Bold), not the old Times-Bold mapping.
        Assert.Contains("/F2 ", pdf);
        Assert.Contains("/BaseFont /Helvetica-Bold", pdf);
    }

    [Fact]
    public void FontFamilySelectsCourierVariant()
    {
        string pdf = Render(c => c.Text("mono").FontFamily("Courier"));
        Assert.Contains("/F9 ", pdf);
        Assert.Contains("/BaseFont /Courier", pdf);
    }

    [Fact]
    public void FontFamilyTimesWithBoldItalicSelectsTimesBoldItalic()
    {
        string pdf = Render(c => c.Text("serif").FontFamily("Times").Bold().Italic());
        Assert.Contains("/F8 ", pdf);
        Assert.Contains("/BaseFont /Times-BoldItalic", pdf);
    }
}
