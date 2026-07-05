using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for the tier-4 output-format changes: Flate-compressed content
/// streams, streamed serialization, and per-line batched text objects.
/// </summary>
public sealed class OutputFormatTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).PublishPdf();

    private static string Raw(byte[] b) => System.Text.Encoding.Latin1.GetString(b);

    private static int CountOccurrences(string text, string token)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(token, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += token.Length;
        }
        return count;
    }

    private static byte[] SimpleDoc(string text) =>
        Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Text(text);
        }));

    // ── Content-stream compression ────────────────────────────────────────────

    [Fact]
    public void ContentStreamsAreFlateCompressed()
    {
        byte[] bytes = SimpleDoc("CompressionMarkerText");
        string raw = Raw(bytes);

        Assert.Contains("/Filter /FlateDecode", raw);
        // Drawn text must not appear in the raw bytes …
        Assert.DoesNotContain("CompressionMarkerText", raw);
        // … but must survive inflation intact.
        Assert.Contains("CompressionMarkerText", PdfTestUtils.InflatedText(bytes));
    }

    [Fact]
    public void CompressionActuallyShrinksTextHeavyDocuments()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                for (int i = 0; i < 50; i++)
                    col.Item().Text($"Paragraph {i}: the quick brown fox jumps over the lazy dog, " +
                                    "again and again, to make this content stream worth compressing.");
            });
        }));

        // The file must be smaller than its own inflated form — i.e. the
        // compressed streams really are smaller than their decoded content.
        Assert.True(bytes.Length < PdfTestUtils.InflatedText(bytes).Length,
            $"Compressed output ({bytes.Length}B) is not smaller than its inflated form.");
    }

    [Fact]
    public void EncryptedContentStreamsDeclareFlateDecode()
    {
        byte[] bytes = Build(c =>
        {
            c.Encrypt(new EncryptionOptions { UserPassword = "u" });
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("EncryptedAndCompressed");
            });
        });

        string raw = Raw(bytes);
        Assert.Contains("/Filter /FlateDecode", raw);
        // Encrypted: neither raw nor merely-inflated text may reveal the content.
        Assert.DoesNotContain("EncryptedAndCompressed", raw);
        Assert.DoesNotContain("EncryptedAndCompressed", PdfTestUtils.InflatedText(bytes));
    }

    // ── Per-line text objects ─────────────────────────────────────────────────

    [Fact]
    public void SingleStyleLineUsesOneTextObjectAndOneFontOp()
    {
        byte[] bytes = SimpleDoc("one two three");
        string content = PdfTestUtils.InflatedText(bytes);

        // One line → one BT/ET pair; one style → one Tf and one rg.
        Assert.Equal(1, CountOccurrences(content, "BT\n"));
        Assert.Equal(1, CountOccurrences(content, "ET\n"));
        Assert.Equal(1, CountOccurrences(content, " Tf\n"));
        Assert.Equal(1, CountOccurrences(content, " rg\n"));
        // Words are still individual show ops at exact positions.
        Assert.Contains("(one) Tj", content);
        Assert.Contains("(two) Tj", content);
        Assert.Contains("(three) Tj", content);
    }

    [Fact]
    public void EachWrappedLineGetsItsOwnTextObject()
    {
        byte[] bytes = SimpleDoc("first line\nsecond line");
        string content = PdfTestUtils.InflatedText(bytes);

        Assert.Equal(2, CountOccurrences(content, "BT\n"));
        Assert.Equal(2, CountOccurrences(content, "ET\n"));
    }

    [Fact]
    public void UnderlineStrokeIsDrawnOutsideTheTextObject()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Text("underlined").Underline();
        }));
        string content = PdfTestUtils.InflatedText(bytes);

        Assert.Contains("(underlined) Tj", content);

        // The underline (a stroked line: "… l\nS\n") must appear after ET —
        // graphics operators are illegal inside BT…ET.
        int et = content.IndexOf("ET\n", StringComparison.Ordinal);
        int stroke = content.IndexOf(" l\nS\n", StringComparison.Ordinal);
        Assert.True(et >= 0 && stroke > et,
            "Underline stroke must be emitted after the text object closes.");
    }
}
