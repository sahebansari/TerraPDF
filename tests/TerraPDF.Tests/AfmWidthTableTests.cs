using System.Globalization;
using TerraPDF.Drawing;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Checks every WinAnsiEncoding glyph width in <see cref="FontMetrics"/> against the
/// Adobe Core-14 AFM files (TestAssets/Afm, unmodified, distributed with Adobe's
/// MustRead.html as its terms require).
/// <para>
/// Widths must match what a viewer uses exactly: text runs are shown by a single
/// <c>Tj</c>, so the viewer, not TerraPDF, positions every glyph after the first, and
/// any table error shifts the rest of the run.
/// </para>
/// </summary>
public sealed class AfmWidthTableTests
{
    // WinAnsiEncoding glyph names for bytes 32–255 (PDF 32000-1, Annex D); null = undefined.
    private static readonly string?[] WinAnsiNames = BuildWinAnsiNames();

    private static string?[] BuildWinAnsiNames()
    {
        const string ascii =
            "space exclam quotedbl numbersign dollar percent ampersand quotesingle parenleft parenright asterisk plus comma " +
            "hyphen period slash zero one two three four five six seven eight nine colon semicolon less equal greater question at " +
            "A B C D E F G H I J K L M N O P Q R S T U V W X Y Z bracketleft backslash bracketright asciicircum underscore grave " +
            "a b c d e f g h i j k l m n o p q r s t u v w x y z braceleft bar braceright asciitilde";
        const string high =
            "Euro - quotesinglbase florin quotedblbase ellipsis dagger daggerdbl circumflex perthousand Scaron guilsinglleft OE - Zcaron - " +
            "- quoteleft quoteright quotedblleft quotedblright bullet endash emdash tilde trademark scaron guilsinglright oe - zcaron Ydieresis " +
            "space exclamdown cent sterling currency yen brokenbar section dieresis copyright ordfeminine guillemotleft logicalnot hyphen registered macron " +
            "degree plusminus twosuperior threesuperior acute mu paragraph periodcentered cedilla onesuperior ordmasculine guillemotright onequarter onehalf threequarters questiondown " +
            "Agrave Aacute Acircumflex Atilde Adieresis Aring AE Ccedilla Egrave Eacute Ecircumflex Edieresis Igrave Iacute Icircumflex Idieresis " +
            "Eth Ntilde Ograve Oacute Ocircumflex Otilde Odieresis multiply Oslash Ugrave Uacute Ucircumflex Udieresis Yacute Thorn germandbls " +
            "agrave aacute acircumflex atilde adieresis aring ae ccedilla egrave eacute ecircumflex edieresis igrave iacute icircumflex idieresis " +
            "eth ntilde ograve oacute ocircumflex otilde odieresis divide oslash ugrave uacute ucircumflex udieresis yacute thorn ydieresis";

        var names = new List<string?>(ascii.Split(' '));
        names.Add(null); // 127 DEL
        names.AddRange(high.Split(' ').Select(n => n == "-" ? null : n));
        Assert.Equal(224, names.Count);
        return [.. names];
    }

    /// <summary>The character TerraPDF maps to each WinAnsi byte.</summary>
    private static readonly Dictionary<byte, char> CharForByte = BuildCharForByte();

    private static Dictionary<byte, char> BuildCharForByte()
    {
        var map = new Dictionary<byte, char>();
        for (int c = 0; c <= char.MaxValue; c++)
            if (WinAnsiEncoding.TryGetByte((char)c, out byte b))
                map.TryAdd(b, (char)c);
        return map;
    }

    private static Dictionary<string, int> ReadAfm(string fontName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Afm", fontName + ".afm");
        var widths = new Dictionary<string, int>();
        foreach (string line in File.ReadLines(path))
        {
            if (!line.StartsWith("C ", StringComparison.Ordinal)) continue;
            string? name = null;
            int? wx = null;
            foreach (string field in line.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (field.StartsWith("N ", StringComparison.Ordinal)) name = field[2..].Trim();
                else if (field.StartsWith("WX ", StringComparison.Ordinal)) wx = int.Parse(field[3..], CultureInfo.InvariantCulture);
            }
            if (name is not null && wx is not null) widths[name] = wx.Value;
        }
        return widths;
    }

    [Theory]
    [InlineData("Helvetica",             "Helvetica", false, false, 556)]
    [InlineData("Helvetica-Bold",        "Helvetica", true,  false, 556)]
    [InlineData("Helvetica-Oblique",     "Helvetica", false, true,  556)]
    [InlineData("Helvetica-BoldOblique", "Helvetica", true,  true,  556)]
    [InlineData("Times-Roman",           "Times",     false, false, 500)]
    [InlineData("Times-Bold",            "Times",     true,  false, 500)]
    [InlineData("Times-Italic",          "Times",     false, true,  500)]
    [InlineData("Times-BoldItalic",      "Times",     true,  true,  500)]
    [InlineData("Courier",               "Courier",   false, false, 600)]
    public void EveryWinAnsiGlyphWidthMatchesTheAfm(string afmName, string family, bool bold, bool italic, int euroWidth)
    {
        var afm = ReadAfm(afmName);
        var mismatches = new List<string>();

        for (int index = 0; index < WinAnsiNames.Length; index++)
        {
            string? glyph = WinAnsiNames[index];
            byte code = (byte)(index + 32);
            if (glyph is null || !CharForByte.TryGetValue(code, out char c)) continue;

            // The 1997 Core-14 AFMs predate the Euro; viewers use these widths for it.
            int expected = afm.TryGetValue(glyph, out int wx) ? wx : glyph == "Euro" ? euroWidth : -1;
            Assert.True(expected >= 0, $"{afmName} has no width for {glyph}");

            double actual = FontMetrics.MeasureWidth(c.ToString(), 1000, PdfFonts.Resolve(family), bold, italic);
            if (Math.Abs(actual - expected) > 1e-9)
                mismatches.Add($"0x{code:X2} {glyph}: {actual} (AFM {expected})");
        }

        Assert.Empty(mismatches);
    }

    [Theory]
    [InlineData("Helvetica", false, 556)]
    [InlineData("Times",     true,  500)]
    [InlineData("Courier",   false, 600)]
    public void UnmappableCharactersMeasureAsTheQuestionMarkDrawnInTheirPlace(string family, bool bold, int questionWidth)
    {
        foreach (string text in new[] { "中", "Ж", "\u007F", "\u0081" })
        {
            double actual = FontMetrics.MeasureWidth(text, 1000, PdfFonts.Resolve(family), bold, false);
            Assert.Equal(questionWidth, actual, precision: 9);
        }
    }
}
