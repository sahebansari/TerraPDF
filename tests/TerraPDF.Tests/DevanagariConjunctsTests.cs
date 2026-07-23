using System.Linq;
using TerraPDF.Core;
using TerraPDF.Drawing;
using TerraPDF.Drawing.TrueType;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for <see cref="DevanagariConjuncts"/> and the GSUB parsing behind it
/// (<c>TrueTypeFont.Gsub.cs</c>) — substitutes conjunct-forming ligatures the
/// font itself defines via the <c>half</c>/<c>akhn</c>/<c>cjct</c>/<c>rphf</c>/
/// <c>rkrf</c> GSUB features. <c>blwf</c> (below-base forms for non-र
/// consonants) is intentionally not covered — see docs/custom-fonts.md's
/// "Known limitations".
/// </summary>
public sealed class DevanagariConjunctsTests
{
    private static readonly string NotoDevanagariRegularPath =
        Path.Combine(AppContext.BaseDirectory, "TestAssets", "Fonts", "NotoSansDevanagari-Regular.ttf");
    private static readonly byte[] NotoDevanagariRegularBytes = File.ReadAllBytes(NotoDevanagariRegularPath);

    private static string UniqueFamily([System.Runtime.CompilerServices.CallerMemberName] string caller = "") =>
        $"{caller}-{Guid.NewGuid():N}";

    private static List<int> Codepoints(string s) => s.Select(c => (int)c).ToList();

    // ---------------------------------------------------------------
    //  Exact assertions for the two mandatory 'akhn' ligatures — glyph
    //  IDs (क=25, virama=81, ष=59 -> 179; ज=32, virama=81, ञ=34 -> 180)
    //  and script/feature structure were confirmed by hand-inspecting
    //  NotoSansDevanagari-Regular.ttf's actual GSUB table during design.
    // ---------------------------------------------------------------

    [Fact]
    public void KshaAkhnLigatureCollapsesToASingleGlyph()
    {
        var font = TrueTypeFont.Parse(NotoDevanagariRegularBytes);
        var glyphs = DevanagariConjuncts.MapToGlyphs(Codepoints("क्ष"), font);

        var single = Assert.Single(glyphs);
        Assert.Equal((ushort)179, single.GlyphId);
        Assert.Equal((int)'क', single.Codepoint); // representative codepoint = first of the sequence
    }

    [Fact]
    public void JnyaAkhnLigatureCollapsesToASingleGlyph()
    {
        var font = TrueTypeFont.Parse(NotoDevanagariRegularBytes);
        var glyphs = DevanagariConjuncts.MapToGlyphs(Codepoints("ज्ञ"), font);

        var single = Assert.Single(glyphs);
        Assert.Equal((ushort)180, single.GlyphId);
    }

    // ---------------------------------------------------------------
    //  Structural assertions for half-form conjuncts (don't hardcode
    //  the font's internal half-form glyph IDs — just prove substitution
    //  actually happened: fewer output glyphs than input codepoints,
    //  which can only occur when a virama glyph got dropped).
    // ---------------------------------------------------------------

    [Fact]
    public void HalfFormConjunctProducesFewerGlyphsThanCodepoints()
    {
        var font = TrueTypeFont.Parse(NotoDevanagariRegularBytes);
        var codepoints = Codepoints("स्वास्थ्य"); // स्व and स्थ्य are both half-form conjuncts
        var glyphs = DevanagariConjuncts.MapToGlyphs(codepoints, font);

        Assert.True(glyphs.Count < codepoints.Count,
            $"Expected substitution to reduce glyph count below {codepoints.Count}, got {glyphs.Count}");
    }

    [Fact]
    public void MeasureWidthReflectsSubstitutedGlyphsNotRawCodepoints()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, NotoDevanagariRegularBytes);
        var variant = PdfFonts.ResolveFont(family, false, false).Custom!;
        var font = TrueTypeFont.Parse(NotoDevanagariRegularBytes);

        double measuredWidth = variant.MeasureWidth("क्ष", 12);

        double naiveUnsubstitutedWidth =
            Codepoints("क्ष").Sum(cp => font.GetAdvanceWidthInEm(font.GetGlyphId(cp)) * 12 / 1000.0);

        Assert.NotEqual(naiveUnsubstitutedWidth, measuredWidth, precision: 6);
    }

    // ---------------------------------------------------------------
    //  र-conjuncts: reph (rphf) and below-base/post-base 'ra' (rkrf) —
    //  glyph IDs confirmed by hand-inspecting NotoSansDevanagari-Regular.ttf's
    //  actual GSUB table during design (RA=52, VIRAMA=81; reph=181; rakar-KA=254).
    // ---------------------------------------------------------------

    [Fact]
    public void RephCollapsesToASingleGlyphAfterTheBaseConsonant()
    {
        var font = TrueTypeFont.Parse(NotoDevanagariRegularBytes);

        // धर्म is reordered (by DevanagariReordering) from ध-र-्-म to ध-म-र्-
        // before glyph mapping — MapToGlyphs itself does no reordering.
        var reordered = DevanagariReordering.DecodeAndReorder("धर्म");
        var glyphs = DevanagariConjuncts.MapToGlyphs(reordered, font);

        Assert.Equal(3, glyphs.Count); // ध, म, reph (र + ् collapse to one glyph)
        Assert.Equal(font.GetGlyphId('ध'), glyphs[0].GlyphId);
        Assert.Equal(font.GetGlyphId('म'), glyphs[1].GlyphId);
        Assert.Equal((ushort)181, glyphs[2].GlyphId);
    }

    [Fact]
    public void RakarFormCollapsesConsonantViramaRaToASingleGlyph()
    {
        var font = TrueTypeFont.Parse(NotoDevanagariRegularBytes);

        // क्र needs no reordering (र is already the last, correctly-positioned
        // consonant of the chain) — passed through DecodeAndReorder unchanged.
        var codepoints = DevanagariReordering.DecodeAndReorder("क्र");
        var glyphs = DevanagariConjuncts.MapToGlyphs(codepoints, font);

        var single = Assert.Single(glyphs);
        Assert.Equal((ushort)254, single.GlyphId);
    }

    [Fact]
    public void DoubleConjunctEndingInRaCombinesHalfFormAndRakarForm()
    {
        var font = TrueTypeFont.Parse(NotoDevanagariRegularBytes);

        // ष्ट्र (ष-्-ट-्-र): ष takes its half-form (followed by ट), ट+्+र
        // collapses to the rakar form -> 2 glyphs from 5 codepoints.
        var codepoints = Codepoints("ष्ट्र");
        var glyphs = DevanagariConjuncts.MapToGlyphs(codepoints, font);

        Assert.Equal(2, glyphs.Count);
    }

    [Fact]
    public void RaAsSecondConsonantIsNotMisreadAsReph()
    {
        var font = TrueTypeFont.Parse(NotoDevanagariRegularBytes);

        // प्र (प-्-र): र is the *second* consonant, not cluster-initial, so
        // this must produce the rakar ligature, never the reph glyph (181).
        var codepoints = DevanagariReordering.DecodeAndReorder("प्र");
        var glyphs = DevanagariConjuncts.MapToGlyphs(codepoints, font);

        var single = Assert.Single(glyphs);
        Assert.NotEqual((ushort)181, single.GlyphId);
    }

    // ---------------------------------------------------------------
    //  Graceful fallback
    // ---------------------------------------------------------------

    [Fact]
    public void FontWithNoDevanagariGsubDataFallsBackToOneGlyphPerCodepoint()
    {
        string latoPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Fonts", "Lato-Regular.ttf");
        var font = TrueTypeFont.Parse(File.ReadAllBytes(latoPath));

        var codepoints = Codepoints("क्ष"); // Lato has no Devanagari coverage or GSUB data at all
        var glyphs = DevanagariConjuncts.MapToGlyphs(codepoints, font);

        Assert.Equal(codepoints.Count, glyphs.Count); // no exception, no (incorrect) substitution
    }

    [Fact]
    public void NonDevanagariTextIsUnaffected()
    {
        var font = TrueTypeFont.Parse(NotoDevanagariRegularBytes);
        var codepoints = Codepoints("Hello, World!");
        var glyphs = DevanagariConjuncts.MapToGlyphs(codepoints, font);

        Assert.Equal(codepoints.Count, glyphs.Count);
    }

    // ---------------------------------------------------------------
    //  End-to-end: the actual PDF content stream shows the ligature glyph
    // ---------------------------------------------------------------

    [Fact]
    public void GeneratedPdfShowsTheAkhnLigatureGlyph()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, NotoDevanagariRegularBytes);

        byte[] pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSize.A4);
                page.DefaultTextStyle(s => s.FontFamily(family));
                page.Content().Text("क्ष");
            });
        }).PublishPdf();

        string content = PdfTestUtils.InflatedText(pdf);
        Assert.Contains("<00B3> Tj", content); // 179 decimal = 0x00B3
    }

    [Fact]
    public void GeneratedPdfShowsTheRephGlyphAfterTheBaseConsonant()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, NotoDevanagariRegularBytes);

        byte[] pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSize.A4);
                page.DefaultTextStyle(s => s.FontFamily(family));
                page.Content().Text("धर्म");
            });
        }).PublishPdf();

        string content = PdfTestUtils.InflatedText(pdf);
        // ध (gid 43 = 0x002B), म (gid 50 = 0x0032), reph (gid 181 = 0x00B5) —
        // in that order: base consonant first, reph glyph last.
        Assert.Contains("<002B003200B5> Tj", content);
    }
}
