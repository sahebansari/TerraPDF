using TerraPDF.Barcodes;
using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>Tests for the Code128 (Subset B) encoder and the <c>Barcode(...)</c> Fluent API.</summary>
public sealed class Code128Tests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).PublishPdf();

    // ── Symbol table self-check ──────────────────────────────────────────
    // Catches transcription errors in the hand-entered pattern table: every
    // symbol's element widths must sum to 11 modules (13 for STOP), and every
    // element width must be in [1,4] per the Code128 spec.

    [Fact]
    public void SymbolTableHasValidPatternWidths()
    {
        string[] patterns = Code128Encoder.Patterns;

        Assert.Equal(107, patterns.Length);
        for (int value = 0; value < patterns.Length; value++)
        {
            string pattern  = patterns[value];
            int    expected = value == 106 ? 13 : 11; // 106 = STOP
            int    sum      = 0;
            foreach (char c in pattern)
            {
                int digit = c - '0';
                Assert.InRange(digit, 1, 4);
                sum += digit;
            }
            Assert.Equal(expected, sum);
        }
    }

    // ── Encoder correctness ───────────────────────────────────────────────

    [Fact]
    public void EncodeProducesExpectedModuleCount()
    {
        // START B (11) + 'A' + 'B' + 'C' (11 each) + checksum (11) + STOP (13) = 68
        bool[] modules = Code128Encoder.Encode("ABC");
        Assert.Equal(68, modules.Length);
    }

    [Fact]
    public void EncodeAlwaysStartsWithABar()
    {
        bool[] modules = Code128Encoder.Encode("Hello");
        Assert.True(modules[0]);
    }

    [Fact]
    public void EncodeRejectsNonPrintableAscii()
    {
        Assert.Throws<NotSupportedException>(() => Code128Encoder.Encode("A\tB"));
    }

    [Fact]
    public void EncodeIsDeterministic()
    {
        bool[] a = Code128Encoder.Encode("PRODUCT-12345");
        bool[] b = Code128Encoder.Encode("PRODUCT-12345");
        Assert.Equal(a, b);
    }

    // ── Fluent API / rendering ────────────────────────────────────────────

    [Fact]
    public void BarcodeRendersFilledRectsIntoContentStream()
    {
        byte[] pdf = Build(doc => doc.Page(p => p.Content().Barcode("ABC-123")));
        string text = PdfTestUtils.InflatedText(pdf);
        Assert.Contains(" re\n", text);
        Assert.Contains("f\n", text);
    }

    [Fact]
    public void BarcodeWithCaptionRendersText()
    {
        byte[] pdf = Build(doc => doc.Page(p => p.Content().Barcode("CAP-42", showCaption: true)));
        string text = PdfTestUtils.InflatedText(pdf);
        Assert.Contains("(CAP-42) Tj", text);
    }

    [Fact]
    public void BarcodeRejectsInvalidArguments()
    {
        Assert.Throws<ArgumentException>(() =>
            Build(doc => doc.Page(p => p.Content().Barcode(""))));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Build(doc => doc.Page(p => p.Content().Barcode("X", width: -1))));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Build(doc => doc.Page(p => p.Content().Barcode("X", height: 0))));
    }
}
