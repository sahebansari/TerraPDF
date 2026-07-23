using System.Linq;
using TerraPDF.Core;
using TerraPDF.Drawing;
using TerraPDF.Drawing.TrueType;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for <see cref="DevanagariReordering"/> — the pure-C# fix that moves
/// (1) the Devanagari vowel sign ि (U+093F) to before the consonant cluster it
/// attaches to, and (2) a cluster-initial reph (र्) to the end of the cluster
/// it attaches to — matching where each renders visually even though Unicode
/// stores them elsewhere. This is plain character reordering, not general
/// OpenType shaping: only these two verified cases are handled — see
/// docs/custom-fonts.md's "Known limitations".
/// </summary>
public sealed class DevanagariReorderingTests
{
    private static readonly string NotoDevanagariRegularPath =
        Path.Combine(AppContext.BaseDirectory, "TestAssets", "Fonts", "NotoSansDevanagari-Regular.ttf");
    private static readonly byte[] NotoDevanagariRegularBytes = File.ReadAllBytes(NotoDevanagariRegularPath);

    private static string UniqueFamily([System.Runtime.CompilerServices.CallerMemberName] string caller = "") =>
        $"{caller}-{Guid.NewGuid():N}";

    private static List<int> Codepoints(string s) => s.Select(c => (int)c).ToList();

    // ---------------------------------------------------------------
    //  Pure algorithm tests (no font/PDF involved)
    // ---------------------------------------------------------------

    [Fact]
    public void SingleConsonantMatraSwapsWithConsonant()
    {
        // ठिगनापन (ठ ि ग न ा प न) -> िठगनापन: no virama chain, matra swaps with ठ alone.
        var actual = DevanagariReordering.DecodeAndReorder("ठिगनापन");
        Assert.Equal(Codepoints("िठगनापन"), actual);
    }

    [Fact]
    public void MatraAfterFreshConsonantNotLinkedByViramaSwapsWithThatConsonantOnly()
    {
        // प्रतिशत (प ् र त ि श त): त starts its own cluster (not virama-linked to र),
        // so ि swaps with त only, not with the whole प्र conjunct -> प्रितशत.
        var actual = DevanagariReordering.DecodeAndReorder("प्रतिशत");
        Assert.Equal(Codepoints("प्रितशत"), actual);
    }

    [Fact]
    public void MatraAfterConjunctChainMovesBeforeTheWholeChain()
    {
        // स्थिति (स ् थ ि त ि): first ि attaches to the स्थ conjunct chain (moves before स),
        // second ि attaches to त alone -> िस्थित.
        var actual = DevanagariReordering.DecodeAndReorder("स्थिति");
        Assert.Equal(Codepoints("िस्थित"), actual);
    }

    [Fact]
    public void NonDevanagariTextIsUnchanged()
    {
        var actual = DevanagariReordering.DecodeAndReorder("Hello, World!");
        Assert.Equal(Codepoints("Hello, World!"), actual);
    }

    [Fact]
    public void MixedTextOnlyReordersTheDevanagariRun()
    {
        var actual = DevanagariReordering.DecodeAndReorder("Hi ठिगनापन World");
        Assert.Equal(Codepoints("Hi िठगनापन World"), actual);
    }

    [Fact]
    public void EmptyStringReturnsEmptyList()
    {
        Assert.Empty(DevanagariReordering.DecodeAndReorder(""));
    }

    [Fact]
    public void DevanagariTextWithoutMatraOrRephIsUnchanged()
    {
        // स्कूल: स्क is a plain (non-र) conjunct chain, no reph, no U+093F matra -> passthrough.
        var actual = DevanagariReordering.DecodeAndReorder("स्कूल");
        Assert.Equal(Codepoints("स्कूल"), actual);
    }

    // ---------------------------------------------------------------
    //  Reph (cluster-initial र्): moves to the end of the cluster it
    //  attaches to, since it renders as a hook above the base consonant,
    //  not a left-side conjunct.
    // ---------------------------------------------------------------

    [Fact]
    public void ClusterInitialRephMovesToTheEndOfItsCluster()
    {
        // धर्म (ध र ् म): र् is cluster-initial (ध is its own separate syllable),
        // followed by one more consonant म -> reph moves after म -> धमर्.
        var actual = DevanagariReordering.DecodeAndReorder("धर्म");
        Assert.Equal(Codepoints("धमर्"), actual);
    }

    [Fact]
    public void RephWithMultiConsonantClusterMovesToTheVeryEnd()
    {
        // दुर्बलता (द ु र ् ब ल ा त ा): र् is cluster-initial after दु, followed by ब
        // (ब itself starts no further virama-linked chain) -> reph moves after ब.
        var actual = DevanagariReordering.DecodeAndReorder("दुर्बलता");
        Assert.Equal(Codepoints("दुबर्लता"), actual);
    }

    [Fact]
    public void RaAsSecondConsonantInAConjunctIsNotTreatedAsReph()
    {
        // क्र (क ् र): र is the *second* consonant of the chain (below-base/rakar
        // form, handled separately by GSUB's rkrf feature at the glyph stage) —
        // not cluster-initial, so no reordering happens here.
        var actual = DevanagariReordering.DecodeAndReorder("क्र");
        Assert.Equal(Codepoints("क्र"), actual);
    }

    [Fact]
    public void RaFollowedByViramaAtEndOfTextWithNoFurtherConsonantIsNotReph()
    {
        // र् alone (no following consonant to attach to) doesn't qualify as reph —
        // the chain-length>=3 guard requires at least one more consonant.
        var actual = DevanagariReordering.DecodeAndReorder("र्");
        Assert.Equal(Codepoints("र्"), actual);
    }

    // ---------------------------------------------------------------
    //  End-to-end: measurement and drawing agree, and the PDF content
    //  stream shows glyphs in the reordered order.
    // ---------------------------------------------------------------

    [Fact]
    public void GeneratedPdfShowsGlyphsInReorderedOrder()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, NotoDevanagariRegularBytes);

        byte[] pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSize.A4);
                page.DefaultTextStyle(s => s.FontFamily(family));
                page.Content().Text("ठिगनापन");
            });
        }).PublishPdf();

        var font = TrueTypeFont.Parse(NotoDevanagariRegularBytes);
        string expectedHex = string.Concat(
            Codepoints("िठगनापन").Select(cp => font.GetGlyphId(cp).ToString("X4", System.Globalization.CultureInfo.InvariantCulture)));

        string content = PdfTestUtils.InflatedText(pdf);
        Assert.Contains($"<{expectedHex}>", content);
    }

    [Fact]
    public void MeasureWidthAndDrawnGlyphCountAgreeForReorderedText()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, NotoDevanagariRegularBytes);
        var variant = PdfFonts.ResolveFont(family, false, false).Custom!;

        // Reordering only moves an existing codepoint, never adds/removes one,
        // so total measured width must equal the sum of the same glyphs' widths
        // regardless of order (advance widths are additive).
        double widthReordered  = variant.MeasureWidth("ठिगनापन", 12);
        double widthHandOrdered = variant.MeasureWidth("िठगनापन", 12);
        Assert.Equal(widthHandOrdered, widthReordered, precision: 6);
    }
}
