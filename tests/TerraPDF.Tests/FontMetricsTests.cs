using TerraPDF.Drawing;
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
    // Times-Bold 'A'(65) -> 722 units  |  Times-Italic 'A'(65) -> 611 units

    [Theory]
    [InlineData("A",        10, false, false, 6.67)]   // Helvetica 'A' = 667 units
    [InlineData("A",        10, true,  false, 7.22)]   // Times-Bold 'A' = 722 units
    [InlineData("A",        10, false, true,  6.11)]   // Times-Italic 'A' = 611 units
    [InlineData("A",        10, true,  true,  7.22)]   // Times-BoldItalic 'A' = 722 (same as Bold)
    [InlineData(" ",        12, false, false, 3.336)]  // Helvetica space = 278 units @ 12pt
    [InlineData("Hello",    10, false, false, 22.78)]  // H(722)+e(556)+l(222)+l(222)+o(556) = 2278 / 1000 * 10
    public void MeasureWidthMatchesAdobeAfmValues(
        string text, double fontSize, bool bold, bool italic, double expectedPts)
    {
        double actual = FontMetrics.MeasureWidth(text, fontSize, bold, italic);
        Assert.Equal(expectedPts, actual, precision: 1);
    }

    [Fact]
    public void MeasureWidthEmptyStringReturnsZero()
    {
        Assert.Equal(0.0, FontMetrics.MeasureWidth("", 12, false));
    }

    [Fact]
    public void MeasureWidthControlCharacterUsesFallbackWidth()
    {
        // Characters outside the 32-126 range fall back to 500 units.
        double actual = FontMetrics.MeasureWidth("\x01", 10, false);
        Assert.Equal(5.0, actual, precision: 2); // 500 / 1000 * 10
    }
}
