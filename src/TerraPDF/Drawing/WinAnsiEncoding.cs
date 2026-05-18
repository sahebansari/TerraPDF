namespace TerraPDF.Drawing;

/// <summary>
/// Maps Unicode code points to Windows-1252 (WinAnsiEncoding) byte values.
/// <para>
/// PDF standard Type1 fonts use WinAnsiEncoding, which is equivalent to Windows-1252.
/// The mapping is:
/// <list type="bullet">
///   <item>U+0020–U+007E  — printable ASCII; byte value equals code point.</item>
///   <item>U+00A0–U+00FF  — ISO-8859-1 Latin-1 Supplement; byte value equals code point.</item>
///   <item>Selected code points in the BMP — Windows-1252 special characters that
///         occupy byte positions 0x80–0x9F (e.g. € → 0x80, … → 0x85, " → 0x93).</item>
/// </list>
/// Characters with no WinAnsiEncoding representation (e.g. CJK, Arabic, most symbols)
/// are reported as unmappable; callers should substitute a fallback glyph (usually '?').
/// </para>
/// </summary>
internal static class WinAnsiEncoding
{
    // ------------------------------------------------------------------
    //  Windows-1252 special range  (byte positions 0x80–0x9F)
    //  Unicode code points in this range differ from their byte values;
    //  the table gives the forward mapping:  Unicode char → WinAnsi byte.
    // ------------------------------------------------------------------
    private static readonly Dictionary<char, byte> _extras = new(27)
    {
        { '\u20AC', 0x80 }, // €  Euro sign
        { '\u201A', 0x82 }, // ‚  Single low-9 quotation mark
        { '\u0192', 0x83 }, // ƒ  Latin small letter f with hook
        { '\u201E', 0x84 }, // „  Double low-9 quotation mark
        { '\u2026', 0x85 }, // …  Horizontal ellipsis
        { '\u2020', 0x86 }, // †  Dagger
        { '\u2021', 0x87 }, // ‡  Double dagger
        { '\u02C6', 0x88 }, // ˆ  Modifier letter circumflex accent
        { '\u2030', 0x89 }, // ‰  Per mille sign
        { '\u0160', 0x8A }, // Š  Latin capital letter S with caron
        { '\u2039', 0x8B }, // ‹  Single left-pointing angle quotation mark
        { '\u0152', 0x8C }, // Œ  Latin capital ligature OE
        { '\u017D', 0x8E }, // Ž  Latin capital letter Z with caron
        { '\u2018', 0x91 }, // '  Left single quotation mark
        { '\u2019', 0x92 }, // '  Right single quotation mark
        { '\u201C', 0x93 }, // "  Left double quotation mark
        { '\u201D', 0x94 }, // "  Right double quotation mark
        { '\u2022', 0x95 }, // •  Bullet
        { '\u2013', 0x96 }, // –  En dash
        { '\u2014', 0x97 }, // —  Em dash
        { '\u02DC', 0x98 }, // ˜  Small tilde
        { '\u2122', 0x99 }, // ™  Trade mark sign
        { '\u0161', 0x9A }, // š  Latin small letter s with caron
        { '\u203A', 0x9B }, // ›  Single right-pointing angle quotation mark
        { '\u0153', 0x9C }, // œ  Latin small ligature oe
        { '\u017E', 0x9E }, // ž  Latin small letter z with caron
        { '\u0178', 0x9F }, // Ÿ  Latin capital letter Y with diaeresis
    };

    // ------------------------------------------------------------------
    //  Public API
    // ------------------------------------------------------------------

    /// <summary>
    /// Tries to convert a Unicode character to its WinAnsiEncoding byte value.
    /// </summary>
    /// <param name="c">The Unicode character to convert.</param>
    /// <param name="winAnsiByte">
    ///   When the method returns <see langword="true"/>, contains the
    ///   WinAnsiEncoding byte value (0x20–0xFF) for <paramref name="c"/>.
    /// </param>
    /// <returns>
    ///   <see langword="true"/> if a mapping exists;
    ///   <see langword="false"/> for characters with no WinAnsi representation
    ///   (e.g. DEL U+007F, C1 controls U+0080–U+009F, or any code point
    ///   above U+00FF that is not in the Windows-1252 special table).
    /// </returns>
    internal static bool TryGetByte(char c, out byte winAnsiByte)
    {
        // Printable ASCII: code point == byte value, direct mapping.
        if (c >= '\u0020' && c <= '\u007E')
        {
            winAnsiByte = (byte)c;
            return true;
        }

        // ISO-8859-1 Latin-1 Supplement: code point == byte value.
        // Note: U+0080–U+009F (C1 controls) are intentionally excluded here;
        //       those Unicode code points do NOT correspond to the Windows-1252
        //       glyphs that live at bytes 0x80–0x9F.
        if (c >= '\u00A0' && c <= '\u00FF')
        {
            winAnsiByte = (byte)c;
            return true;
        }

        // Windows-1252 specials: reverse-lookup in the extras table.
        return _extras.TryGetValue(c, out winAnsiByte);
    }
}
