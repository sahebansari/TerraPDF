using TerraPDF.Drawing.TrueType;

namespace TerraPDF.Helpers;

/// <summary>
/// Registers custom TrueType fonts (<c>.ttf</c>, or <c>.otf</c> files that still carry a
/// <c>glyf</c>/<c>loca</c> table) so they can be used by name with <see cref="TextStyle.FontFamily(string)"/>.
/// <para>
/// Registration is process-wide and thread-safe: each font file is parsed once and the
/// result is cached, so register fonts once at application startup and reference them by
/// name from any document rendered afterwards, including concurrently.
/// </para>
/// <para>
/// A family can have up to four registered variants (regular, bold, italic, bold-italic).
/// Requesting a style that wasn't registered falls back to the closest available variant
/// rather than throwing (e.g. <c>.Bold()</c> on a family with only a regular file registered
/// renders with the regular outlines).
/// </para>
/// <para>
/// <b>Known limitations (current version):</b> only TrueType-outline fonts are supported —
/// CFF-flavoured OpenType (<c>OTTO</c>) and TrueType Collections (<c>.ttc</c>) throw
/// <see cref="NotSupportedException"/>. Fonts are embedded in full; glyph subsetting is not
/// yet implemented, so embedding a large font increases the output PDF's size accordingly.
/// </para>
/// </summary>
/// <example>
/// <code>
/// FontFamily.Register("Brand", "fonts/Brand-Regular.ttf");
/// FontFamily.Register("Brand", "fonts/Brand-Bold.ttf", bold: true);
///
/// Document.Create(container =>
/// {
///     container.Page(page =>
///     {
///         page.DefaultTextStyle(s => s.FontFamily("Brand"));
///         page.Content().Text("Héllo, Wörld — 世界").Bold();
///     });
/// })
/// .PublishPdf("output.pdf");
/// </code>
/// </example>
public static class FontFamily
{
    /// <summary>Registers a font variant from a file path.</summary>
    /// <param name="familyName">The name used to reference this font via <see cref="TextStyle.FontFamily(string)"/>.</param>
    /// <param name="fontFilePath">Path to a <c>.ttf</c>/<c>.otf</c> font file.</param>
    /// <param name="bold">Registers this file as the family's bold variant.</param>
    /// <param name="italic">Registers this file as the family's italic variant.</param>
    /// <exception cref="ArgumentException"><paramref name="familyName"/> is null/whitespace, <paramref name="fontFilePath"/> is null/whitespace, or the file is not a valid TrueType font.</exception>
    /// <exception cref="NotSupportedException">The font is CFF-flavoured OpenType or a TrueType Collection.</exception>
    public static void Register(string familyName, string fontFilePath, bool bold = false, bool italic = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFilePath);
        Register(familyName, File.ReadAllBytes(fontFilePath), bold, italic);
    }

    /// <summary>Registers a font variant from raw font file bytes.</summary>
    /// <param name="familyName">The name used to reference this font via <see cref="TextStyle.FontFamily(string)"/>.</param>
    /// <param name="fontFileBytes">Raw <c>.ttf</c>/<c>.otf</c> font file bytes.</param>
    /// <param name="bold">Registers this data as the family's bold variant.</param>
    /// <param name="italic">Registers this data as the family's italic variant.</param>
    /// <exception cref="ArgumentException"><paramref name="familyName"/> is null/whitespace, or the data is not a valid TrueType font.</exception>
    /// <exception cref="NotSupportedException">The font is CFF-flavoured OpenType or a TrueType Collection.</exception>
    public static void Register(string familyName, byte[] fontFileBytes, bool bold = false, bool italic = false) =>
        CustomFontRegistry.Register(familyName, fontFileBytes, bold, italic);

    /// <summary>Registers a font variant read from a stream (read to completion, not disposed).</summary>
    /// <param name="familyName">The name used to reference this font via <see cref="TextStyle.FontFamily(string)"/>.</param>
    /// <param name="fontFileStream">A stream positioned at the start of a <c>.ttf</c>/<c>.otf</c> font file.</param>
    /// <param name="bold">Registers this data as the family's bold variant.</param>
    /// <param name="italic">Registers this data as the family's italic variant.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fontFileStream"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="familyName"/> is null/whitespace, or the data is not a valid TrueType font.</exception>
    /// <exception cref="NotSupportedException">The font is CFF-flavoured OpenType or a TrueType Collection.</exception>
    public static void Register(string familyName, Stream fontFileStream, bool bold = false, bool italic = false)
    {
        ArgumentNullException.ThrowIfNull(fontFileStream);
        using var ms = new MemoryStream();
        fontFileStream.CopyTo(ms);
        Register(familyName, ms.ToArray(), bold, italic);
    }
}
