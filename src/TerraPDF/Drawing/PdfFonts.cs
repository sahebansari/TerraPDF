namespace TerraPDF.Drawing;

/// <summary>The three standard-14 text font families available without embedding.</summary>
internal enum PdfFontFamily
{
    Helvetica,
    Times,
    Courier,
}

/// <summary>
/// Registry of the twelve standard-14 text fonts (3 families × regular/bold/italic/bold-italic)
/// used by TerraPDF.  Maps a resolved <see cref="PdfFontFamily"/> + weight/slant to the
/// page-resource alias (<c>F1</c>–<c>F12</c>) and PostScript BaseFont name.
/// </summary>
internal static class PdfFonts
{
    /// <summary>
    /// All twelve font variants in alias order (F1–F12).
    /// Index = family × 4 + (bold ? 1 : 0) + (italic ? 2 : 0).
    /// </summary>
    internal static readonly (string Alias, string BaseFont)[] All =
    [
        ("F1",  "Helvetica"),
        ("F2",  "Helvetica-Bold"),
        ("F3",  "Helvetica-Oblique"),
        ("F4",  "Helvetica-BoldOblique"),
        ("F5",  "Times-Roman"),
        ("F6",  "Times-Bold"),
        ("F7",  "Times-Italic"),
        ("F8",  "Times-BoldItalic"),
        ("F9",  "Courier"),
        ("F10", "Courier-Bold"),
        ("F11", "Courier-Oblique"),
        ("F12", "Courier-BoldOblique"),
    ];

    /// <summary>Returns the page-resource alias (e.g. <c>"F2"</c>) for a font variant.</summary>
    internal static string Alias(PdfFontFamily family, bool bold, bool italic) =>
        All[VariantIndex(family, bold, italic)].Alias;

    private static int VariantIndex(PdfFontFamily family, bool bold, bool italic) =>
        (int)family * 4 + (bold ? 1 : 0) + (italic ? 2 : 0);

    /// <summary>
    /// Resolves a user-supplied family name to one of the three standard families.
    /// Matching is case-insensitive and accepts common aliases
    /// (<c>"Arial"</c> → Helvetica, <c>"Times New Roman"</c> → Times,
    /// <c>"Courier New"</c> → Courier).  Unknown or null names fall back to Helvetica.
    /// </summary>
    internal static PdfFontFamily Resolve(string? family)
    {
        if (string.IsNullOrWhiteSpace(family))
            return PdfFontFamily.Helvetica;

        string f = family.Trim();
        if (f.StartsWith("Times", StringComparison.OrdinalIgnoreCase))
            return PdfFontFamily.Times;
        if (f.StartsWith("Courier", StringComparison.OrdinalIgnoreCase))
            return PdfFontFamily.Courier;
        return PdfFontFamily.Helvetica;
    }
}
