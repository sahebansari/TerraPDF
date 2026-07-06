using TerraPDF.Barcodes;
using TerraPDF.Barcodes.QrCode;
using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for the from-scratch QR code engine (Reed-Solomon math, bitstream
/// encoding, matrix placement/masking, format/version BCH info) and the
/// <c>QrCode(...)</c> Fluent API.
/// </summary>
public sealed class QrCodeTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).PublishPdf();

    // ── Galois field / Reed-Solomon ──────────────────────────────────────

    [Fact]
    public void GaloisFieldMultiplyIsCommutativeAndHasIdentity()
    {
        for (int a = 1; a < 256; a++)
        {
            Assert.Equal(GaloisField.Multiply(a, 1), a);
            for (int b = 1; b < 256; b += 37)
                Assert.Equal(GaloisField.Multiply(a, b), GaloisField.Multiply(b, a));
        }
    }

    [Theory]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(30)]
    public void ReedSolomonCodewordsHaveZeroSyndrome(int ecCount)
    {
        byte[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        byte[] ec   = GaloisField.ComputeEcCodewords(data, ecCount);

        byte[] full = [.. data, .. ec];

        // Evaluating the full codeword polynomial at each root alpha^i (i=0..ecCount-1)
        // must be zero for a valid Reed-Solomon codeword (Horner's method in GF(256)).
        for (int i = 0; i < ecCount; i++)
        {
            int root   = GaloisField.Exp(i);
            int result = 0;
            foreach (byte coefficient in full)
                result = GaloisField.Multiply(result, root) ^ coefficient;
            Assert.Equal(0, result);
        }
    }

    // ── Format / version information BCH validity ────────────────────────
    // Independently re-implements GF(2) polynomial division (not calling into
    // QrMatrixBuilder's own division helper) so a bug in that helper can't
    // hide from these tests.

    private static int Gf2Mod(int value, int generator)
    {
        int genLen(int v) { int l = 0; while (v != 0) { v >>= 1; l++; } return l; }
        int generatorLength = genLen(generator);
        while (genLen(value) >= generatorLength)
            value ^= generator << (genLen(value) - generatorLength);
        return value;
    }

    [Fact]
    public void FormatInfoBitsAreValidBchCodewords()
    {
        const int FormatGenerator = 0b10100110111;
        const int FormatXorMask   = 0b101010000010010;

        foreach (QrErrorCorrectionLevel level in Enum.GetValues<QrErrorCorrectionLevel>())
            for (int mask = 0; mask < 8; mask++)
            {
                int raw = QrMatrixBuilder.ComputeFormatInfoBits(level, mask) ^ FormatXorMask;
                Assert.Equal(0, Gf2Mod(raw, FormatGenerator));
            }
    }

    [Theory]
    [InlineData(7)]
    [InlineData(20)]
    [InlineData(40)]
    public void VersionInfoBitsAreValidBchCodewords(int version)
    {
        const int VersionGenerator = 0b1111100100101;
        int raw = QrMatrixBuilder.ComputeVersionInfoBits(version);
        Assert.Equal(0, Gf2Mod(raw, VersionGenerator));
    }

    // ── Matrix structural invariants ─────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(21)]
    [InlineData(40)]
    public void MatrixSizeMatchesVersionFormula(int version)
    {
        // Build directly via QrEncoder/QrMatrixBuilder, choosing a data length
        // that forces this exact version, to check the matrix size formula
        // (4*version+17) independently of QrCodeGenerator's own version selection.
        var encoded = QrEncoder.Encode(SampleTextForVersion(version), QrErrorCorrectionLevel.L);
        var matrix  = QrMatrixBuilder.Build(encoded.Version, encoded.Level, encoded.Codewords);
        Assert.Equal(4 * encoded.Version + 17, matrix.GetLength(0));
    }

    private static string SampleTextForVersion(int version)
    {
        // Byte-mode capacity grows roughly monotonically with version; repeat a
        // filler character until the encoder is forced to pick this exact version.
        for (int len = 1; len <= 3000; len++)
        {
            var data = QrEncoder.Encode(new string('A', len), QrErrorCorrectionLevel.L);
            if (data.Version == version) return new string('A', len);
            if (data.Version > version) return new string('A', len - 1);
        }
        throw new InvalidOperationException("Could not find sample text for version " + version);
    }

    [Fact]
    public void FinderPatternCornersAreDark()
    {
        var qr = QrCodeGenerator.Generate("https://example.com/terrapdf", QrErrorCorrectionLevel.M);
        Assert.True(qr.Modules[0, 0]);
        Assert.True(qr.Modules[0, qr.Size - 1]);
        Assert.True(qr.Modules[qr.Size - 1, 0]);
        // Bottom-right corner is never a finder pattern.
    }

    [Fact]
    public void DarkModuleIsAlwaysSet()
    {
        var qr = QrCodeGenerator.Generate("dark-module-check", QrErrorCorrectionLevel.H);
        int version = (qr.Size - 17) / 4;
        Assert.True(qr.Modules[8, 4 * version + 9]);
    }

    [Fact]
    public void TimingPatternAlternates()
    {
        var qr = QrCodeGenerator.Generate("timing-pattern-check", QrErrorCorrectionLevel.Q);
        for (int i = 8; i <= qr.Size - 9; i++)
            Assert.Equal(i % 2 == 0, qr.Modules[6, i]);
    }

    // ── Capacity / errors ─────────────────────────────────────────────────

    [Fact]
    public void TooLongForVersion40ThrowsNotSupported()
    {
        string huge = new('A', 5000);
        Assert.Throws<NotSupportedException>(() => QrEncoder.Encode(huge, QrErrorCorrectionLevel.H));
    }

    [Fact]
    public void HigherErrorCorrectionNeedsMoreCapacityForSameText()
    {
        string text = new('X', 200);
        var low  = QrEncoder.Encode(text, QrErrorCorrectionLevel.L);
        var high = QrEncoder.Encode(text, QrErrorCorrectionLevel.H);
        Assert.True(high.Version >= low.Version);
    }

    // ── Fluent API / rendering ────────────────────────────────────────────

    [Fact]
    public void QrCodeRendersFilledRectsIntoContentStream()
    {
        byte[] pdf = Build(doc => doc.Page(p => p.Content().QrCode("https://terrapdf.example/")));
        string text = PdfTestUtils.InflatedText(pdf);
        Assert.Contains(" re\n", text);
        Assert.Contains("f\n", text);
    }

    [Theory]
    [InlineData(QrErrorCorrectionLevel.L)]
    [InlineData(QrErrorCorrectionLevel.M)]
    [InlineData(QrErrorCorrectionLevel.Q)]
    [InlineData(QrErrorCorrectionLevel.H)]
    public void QrCodeRendersAtEveryErrorCorrectionLevel(QrErrorCorrectionLevel level)
    {
        byte[] pdf = Build(doc => doc.Page(p => p.Content().QrCode("Test data 123", level: level)));
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void QrCodeRejectsInvalidArguments()
    {
        Assert.Throws<ArgumentException>(() =>
            Build(doc => doc.Page(p => p.Content().QrCode(""))));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Build(doc => doc.Page(p => p.Content().QrCode("X", size: -1))));
    }
}
