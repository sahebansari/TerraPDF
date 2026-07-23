using System.Text;
using TerraPDF.Core;
using TerraPDF.Drawing;
using TerraPDF.Drawing.TrueType;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Unit and integration tests for custom (embedded) TrueType font support:
/// <see cref="TrueTypeFont"/> parsing, <see cref="FontFamily"/> registration,
/// font resolution/fallback, and the <c>Type0</c>/<c>CIDFontType2</c> object
/// graph <see cref="PdfDocument"/> emits for a registered font.
/// <para>
/// Test fixture: <c>TestAssets/Fonts/Lato-{Regular,Bold}.ttf</c>, the SIL Open
/// Font License-licensed Lato family (see the accompanying <c>OFL.txt</c>).
/// </para>
/// </summary>
public sealed class FontEmbeddingTests
{
    private static readonly string LatoRegularPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Fonts", "Lato-Regular.ttf");
    private static readonly string LatoBoldPath     = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Fonts", "Lato-Bold.ttf");
    private static readonly byte[] LatoRegularBytes = File.ReadAllBytes(LatoRegularPath);
    private static readonly byte[] LatoBoldBytes    = File.ReadAllBytes(LatoBoldPath);

    /// <summary>Every test registers under its own unique family name so the process-wide registry never leaks state between tests.</summary>
    private static string UniqueFamily([System.Runtime.CompilerServices.CallerMemberName] string caller = "") =>
        $"{caller}-{Guid.NewGuid():N}";

    // ---------------------------------------------------------------
    //  TrueTypeFont parsing
    // ---------------------------------------------------------------

    [Fact]
    public void ParseRejectsTooShortData()
    {
        Assert.Throws<ArgumentException>(() => TrueTypeFont.Parse(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void ParseRejectsOttoCffFonts()
    {
        byte[] fake = BuildFakeSfntHeader("OTTO");
        Assert.Throws<NotSupportedException>(() => TrueTypeFont.Parse(fake));
    }

    [Fact]
    public void ParseRejectsTrueTypeCollections()
    {
        byte[] fake = BuildFakeSfntHeader("ttcf");
        Assert.Throws<NotSupportedException>(() => TrueTypeFont.Parse(fake));
    }

    [Fact]
    public void ParseRejectsUnrecognisedSignature()
    {
        byte[] fake = BuildFakeSfntHeader("FAKE");
        Assert.Throws<ArgumentException>(() => TrueTypeFont.Parse(fake));
    }

    [Fact]
    public void ParseRejectsFontWithNoTablesAtAll()
    {
        // Valid sfnt version, zero tables: even 'glyf'/'loca' are absent, so this
        // is rejected as "not a TrueType-outline font" before any other table is checked.
        byte[] data = new byte[12];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0), 0x00010000u);
        Assert.Throws<NotSupportedException>(() => TrueTypeFont.Parse(data));
    }

    [Fact]
    public void ParseRejectsFontMissingHeadTable()
    {
        // 'glyf'/'loca' present (so it passes the outline-flavour check) but 'head' is absent.
        byte[] data = BuildSfntWithTables(("glyf", new byte[1]), ("loca", new byte[1]));
        Assert.Throws<ArgumentException>(() => TrueTypeFont.Parse(data));
    }

    [Fact]
    public void ParseValidTrueTypeFontSucceeds()
    {
        var font = TrueTypeFont.Parse(LatoRegularBytes);
        Assert.True(font.UnitsPerEm > 0);
        Assert.True(font.NumGlyphs > 0);
        Assert.Same(LatoRegularBytes, font.RawData);
    }

    [Fact]
    public void GetGlyphIdResolvesKnownLatinLetter()
    {
        var font = TrueTypeFont.Parse(LatoRegularBytes);
        ushort gid = font.GetGlyphId('A');
        Assert.NotEqual(0, gid); // 'A' must map to a real glyph, not .notdef
    }

    [Fact]
    public void GetGlyphIdReturnsNotDefForUnmappedCodepoint()
    {
        var font = TrueTypeFont.Parse(LatoRegularBytes);
        // U+E000 is a Private Use Area codepoint no text font maps.
        Assert.Equal(0, font.GetGlyphId(0xE000));
    }

    [Fact]
    public void AdvanceWidthForLetterAIsPlausible()
    {
        var font = TrueTypeFont.Parse(LatoRegularBytes);
        double width = font.GetAdvanceWidthInEm(font.GetGlyphId('A'));
        // 1000-unit glyph space: a Latin capital letter is comfortably within this band.
        Assert.InRange(width, 300, 1200);
    }

    // ---------------------------------------------------------------
    //  FontFamily registration + resolution
    // ---------------------------------------------------------------

    [Fact]
    public void RegisterFromFilePathSucceeds()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, LatoRegularPath);
        var resolved = PdfFonts.ResolveFont(family, bold: false, italic: false);
        Assert.True(resolved.IsCustom);
    }

    [Fact]
    public void RegisterFromStreamSucceeds()
    {
        string family = UniqueFamily();
        using var ms = new MemoryStream(LatoRegularBytes);
        FontFamily.Register(family, ms);
        Assert.True(PdfFonts.ResolveFont(family, false, false).IsCustom);
    }

    [Fact]
    public void UnregisteredFamilyFallsBackToStandardFonts()
    {
        var resolved = PdfFonts.ResolveFont("SomeFamilyNeverRegistered-xyz", false, false);
        Assert.False(resolved.IsCustom);
    }

    [Fact]
    public void RequestingUnregisteredBoldFallsBackToRegisteredRegular()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, LatoRegularBytes); // regular only

        var resolved = PdfFonts.ResolveFont(family, bold: true, italic: false);

        Assert.True(resolved.IsCustom); // falls back to the custom family, not Helvetica
        Assert.False(resolved.Custom!.Bold); // ...using the regular variant, since no bold was registered
    }

    [Fact]
    public void ExactBoldVariantIsPreferredWhenRegistered()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, LatoRegularBytes);
        FontFamily.Register(family, LatoBoldBytes, bold: true);

        var resolved = PdfFonts.ResolveFont(family, bold: true, italic: false);

        Assert.True(resolved.IsCustom);
        Assert.True(resolved.Custom!.Bold);
    }

    // ---------------------------------------------------------------
    //  CustomFontVariant measurement
    // ---------------------------------------------------------------

    [Fact]
    public void MeasureWidthScalesLinearlyWithFontSize()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, LatoRegularBytes);
        var variant = PdfFonts.ResolveFont(family, false, false).Custom!;

        double at10 = variant.MeasureWidth("Hello", 10);
        double at20 = variant.MeasureWidth("Hello", 20);

        Assert.True(at10 > 0);
        Assert.Equal(at20, at10 * 2, precision: 3);
    }

    [Fact]
    public void MeasureWidthEmptyStringIsZero()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, LatoRegularBytes);
        var variant = PdfFonts.ResolveFont(family, false, false).Custom!;

        Assert.Equal(0, variant.MeasureWidth("", 12));
    }

    // ---------------------------------------------------------------
    //  End-to-end PDF generation
    // ---------------------------------------------------------------

    [Fact]
    public void DocumentWithCustomFontEmitsType0ObjectGraph()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, LatoRegularBytes);

        byte[] pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSize.A4);
                page.DefaultTextStyle(s => s.FontFamily(family));
                page.Content().Text("Héllo, Wörld");
            });
        }).PublishPdf();

        string text = Encoding.Latin1.GetString(pdf);
        Assert.Contains("/Subtype /Type0", text);
        Assert.Contains("/Subtype /CIDFontType2", text);
        Assert.Contains("/Encoding /Identity-H", text);
        Assert.Contains("/FontFile2", text);
        Assert.Contains("/ToUnicode", text);
        Assert.Contains("/CIDToGIDMap /Identity", text);
    }

    [Fact]
    public void CustomFontUsedAcrossManyPagesIsEmbeddedExactlyOnce()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, LatoRegularBytes);

        byte[] pdf = Document.Create(container =>
        {
            for (int i = 0; i < 5; i++)
            {
                container.Page(page =>
                {
                    page.Size(PageSize.A4);
                    page.DefaultTextStyle(s => s.FontFamily(family));
                    page.Content().Text($"Page {i}");
                });
            }
        }).PublishPdf();

        string text = Encoding.Latin1.GetString(pdf);
        int fontFileCount = CountOccurrences(text, "/FontFile2 ");
        Assert.Equal(1, fontFileCount);
    }

    [Fact]
    public void TextWithCodepointMissingFromFontDoesNotThrow()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, LatoRegularBytes); // Latin/Cyrillic/Greek coverage only

        byte[] pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSize.A4);
                page.DefaultTextStyle(s => s.FontFamily(family));
                page.Content().Text("Mixed: café 世界"); // CJK glyphs are absent from Lato
            });
        }).PublishPdf();

        Assert.True(pdf.Length > 0);
    }

    [Fact]
    public void EncryptedDocumentWithCustomFontStillProducesValidStreams()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, LatoRegularBytes);

        byte[] pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSize.A4);
                page.DefaultTextStyle(s => s.FontFamily(family));
                page.Content().Text("Encrypted custom font text");
            });
            container.Encrypt(new EncryptionOptions { UserPassword = "user" });
        }).PublishPdf();

        string text = Encoding.Latin1.GetString(pdf);
        Assert.Contains("/Subtype /Type0", text);
        Assert.Contains("/Encrypt", text);
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------

    private static byte[] BuildFakeSfntHeader(string tag)
    {
        byte[] data = new byte[12];
        Encoding.ASCII.GetBytes(tag).CopyTo(data, 0);
        return data;
    }

    /// <summary>Builds a minimal sfnt file (valid version 1.0 header) containing exactly the given tables.</summary>
    private static byte[] BuildSfntWithTables(params (string Tag, byte[] Data)[] tables)
    {
        int headerSize = 12 + tables.Length * 16;
        int bodySize = tables.Sum(t => t.Data.Length);
        byte[] data = new byte[headerSize + bodySize];

        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0), 0x00010000u);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(4), (ushort)tables.Length);

        int recordPos = 12;
        int dataPos = headerSize;
        foreach (var (tag, tableData) in tables)
        {
            Encoding.ASCII.GetBytes(tag).CopyTo(data, recordPos);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(recordPos + 8), (uint)dataPos);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(recordPos + 12), (uint)tableData.Length);
            tableData.CopyTo(data, dataPos);
            recordPos += 16;
            dataPos += tableData.Length;
        }
        return data;
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, pos = 0;
        while ((pos = haystack.IndexOf(needle, pos, StringComparison.Ordinal)) >= 0)
        {
            count++;
            pos += needle.Length;
        }
        return count;
    }
}
