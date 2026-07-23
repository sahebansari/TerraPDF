using System.Text;

namespace TerraPDF.Drawing.TrueType;

/// <summary>
/// One registered (family, bold, italic) combination — a parsed <see cref="TrueTypeFont"/>
/// plus the derived PDF metadata needed to embed it as a <c>CIDFontType2</c> composite font.
/// Instances are cached and reused by <see cref="CustomFontRegistry"/>, so reference equality
/// is sufficient to deduplicate a font embedded across many pages of the same document.
/// </summary>
internal sealed class CustomFontVariant
{
    internal string FamilyName { get; }
    internal bool Bold { get; }
    internal bool Italic { get; }
    internal TrueTypeFont Font { get; }

    /// <summary>PDF <c>/BaseFont</c> name — the family name with whitespace stripped plus a style suffix.</summary>
    internal string BaseFontName { get; }

    internal CustomFontVariant(string familyName, bool bold, bool italic, TrueTypeFont font)
    {
        FamilyName = familyName;
        Bold       = bold;
        Italic     = italic;
        Font       = font;

        string sanitized = new(familyName.Where(c => !char.IsWhiteSpace(c)).ToArray());
        string suffix = (bold, italic) switch
        {
            (true, true)   => ",BoldItalic",
            (true, false)  => ",Bold",
            (false, true)  => ",Italic",
            _              => string.Empty,
        };
        BaseFontName = sanitized + suffix;
    }

    /// <summary>
    /// Total advance width of <paramref name="text"/> in PDF points at <paramref name="fontSize"/>,
    /// walking Unicode scalar values (surrogate pairs kept intact, Devanagari-reordered via
    /// <see cref="DevanagariReordering.DecodeAndReorder"/> and mapped to glyphs — including
    /// conjunct-ligature substitution — via <see cref="DevanagariConjuncts.MapToGlyphs"/>)
    /// through the font's <c>hmtx</c> table. Codepoints missing from the font measure as
    /// <c>.notdef</c> (glyph 0)'s width.
    /// </summary>
    internal double MeasureWidth(string text, double fontSize)
    {
        double total = 0;
        var codepoints = DevanagariReordering.DecodeAndReorder(text);
        foreach (var (gid, _) in DevanagariConjuncts.MapToGlyphs(codepoints, Font))
        {
            total += Font.GetAdvanceWidthInEm(gid) * fontSize / 1000.0;
        }
        return total;
    }

    /// <summary>
    /// PDF <c>/Flags</c> value for the <c>FontDescriptor</c>. Always marks the font Nonsymbolic
    /// (bit 6) since embedded text is addressed by Unicode through <c>Identity-H</c>, and adds
    /// Italic (bit 7) when the registration or the font's own <c>post</c> italic angle indicates
    /// a slanted design. Finer classification (serif, fixed-pitch) is not attempted — conforming
    /// readers render from the embedded outlines regardless of these hints.
    /// </summary>
    internal int DescriptorFlags
    {
        get
        {
            int flags = 0x20; // Nonsymbolic
            if (Italic || Font.ItalicAngle != 0) flags |= 0x40; // Italic
            return flags;
        }
    }

    /// <summary>Heuristic <c>/StemV</c> value (no true stem measurement is available without outline analysis).</summary>
    internal int StemV => Bold ? 120 : 80;
}
