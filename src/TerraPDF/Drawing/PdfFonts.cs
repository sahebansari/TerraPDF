using TerraPDF.Drawing.TrueType;

namespace TerraPDF.Drawing;

/// <summary>The three standard-14 text font families available without embedding.</summary>
internal enum PdfFontFamily
{
    Helvetica,
    Times,
    Courier,
}

/// <summary>
/// A font resolved for a piece of text: either one of the three standard-14 families
/// (no embedding, <see cref="StandardFamily"/> valid) or a registered custom TrueType
/// variant (embedded per-document, <see cref="Custom"/> valid). Keeping both behind one
/// type lets <see cref="Elements.TextBlock"/> and <see cref="PdfPage"/> carry a single
/// value through measurement and drawing without a second call path for every site.
/// </summary>
internal readonly struct ResolvedFont
{
    internal bool IsCustom { get; private init; }
    internal PdfFontFamily StandardFamily { get; private init; }
    internal CustomFontVariant? Custom { get; private init; }

    internal static ResolvedFont Standard(PdfFontFamily family) =>
        new() { IsCustom = false, StandardFamily = family };

    internal static ResolvedFont FromCustom(CustomFontVariant variant) =>
        new() { IsCustom = true, Custom = variant };
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

    /// <summary>
    /// Resolves a user-supplied family name and weight/slant to the font that should
    /// actually render the text: a registered custom TrueType variant when
    /// <paramref name="family"/> matches one (see <see cref="Helpers.FontFamily.Register(string, string, bool, bool)"/>),
    /// otherwise the same standard-14 resolution as <see cref="Resolve(string?)"/>.
    /// </summary>
    internal static ResolvedFont ResolveFont(string? family, bool bold, bool italic)
    {
        if (!string.IsNullOrWhiteSpace(family) &&
            CustomFontRegistry.TryGet(family, bold, italic, out var variant))
        {
            return ResolvedFont.FromCustom(variant!);
        }
        return ResolvedFont.Standard(Resolve(family));
    }
}
