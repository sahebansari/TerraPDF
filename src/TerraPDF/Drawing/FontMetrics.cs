namespace TerraPDF.Drawing;

/// <summary>
/// Character width tables from the Adobe Font Metrics (AFM) files for the
/// built-in Type1 fonts used by TerraPDF.
/// Widths are in PDF glyph units (1000 units = 1 em = font-size in points).
/// Source: Adobe Core 14 Font AFM files (public domain).
/// Note: Oblique/italic variants share the same advance widths as their upright counterparts,
/// except Times-Italic which has its own distinct width table.
/// </summary>
internal static class FontMetrics
{
    // -- Helvetica / Helvetica-Oblique -----------------------------------------
    // Helvetica-Oblique is a slanted version of Helvetica; advance widths are identical.
    // Widths for WinAnsiEncoding characters 32-126 (printable ASCII).

    private static readonly int[] HelveticaWidths =
    {
        // 32 (space) … 126 (~)
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278, // 32-47
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556, // 48-63
       1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778, // 64-79
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556, // 80-95
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556, // 96-111
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584,      // 112-126
    };

    // -- Times-Bold / Times-BoldItalic -----------------------------------------
    // Times-BoldItalic shares the same advance widths as Times-Bold.

    private static readonly int[] TimesBoldWidths =
    {
        // 32 (space) … 126 (~)
        250, 333, 555, 500, 500,1000, 833, 278, 333, 333, 500, 570, 250, 333, 250, 278, // 32-47
        500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 333, 333, 570, 570, 570, 500, // 48-63
        930, 722, 667, 722, 722, 667, 611, 778, 778, 389, 500, 778, 667, 944, 722, 778, // 64-79
        611, 778, 722, 556, 667, 722, 722,1000, 722, 722, 611, 333, 278, 333, 581, 500, // 80-95
        333, 500, 556, 444, 556, 444, 333, 500, 556, 278, 333, 556, 278, 833, 556, 500, // 96-111
        556, 556, 444, 389, 333, 556, 500, 722, 500, 500, 444, 394, 220, 394, 520,      // 112-126
    };

    // -- Times-Italic ----------------------------------------------------------
    // Times-Italic has distinct advance widths from Times-Roman and Times-Bold.
    // Widths for WinAnsiEncoding characters 32-126, sourced from Adobe Times-Italic AFM.

    private static readonly int[] TimesItalicWidths =
    {
        // 32 (space) ... 126 (~)
        250, 333, 420, 500, 500, 833, 778, 214, 333, 333, 500, 675, 250, 333, 250, 278, // 32-47
        500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 333, 333, 675, 675, 675, 500, // 48-63
        920, 611, 611, 667, 722, 611, 611, 722, 722, 333, 444, 667, 556, 833, 667, 722, // 64-79
        611, 722, 611, 500, 556, 722, 611, 833, 611, 556, 556, 389, 278, 389, 422, 500, // 80-95
        333, 500, 500, 444, 500, 444, 278, 500, 500, 278, 278, 444, 278, 722, 500, 500, // 96-111
        500, 500, 389, 389, 278, 500, 444, 667, 444, 444, 389, 400, 275, 400, 541,      // 112-126
    };

    // -- Public API ------------------------------------------------------------

    /// <summary>
    /// Returns the total width of <paramref name="text"/> in PDF points.
    /// Font selection mirrors <c>TextBlock.Draw()</c>:
    /// bold+italic -> Times-BoldItalic (same widths as Times-Bold),
    /// bold only   -> Times-Bold,
    /// italic only -> Times-Italic,
    /// neither     -> Helvetica / Helvetica-Oblique (same widths).
    /// </summary>
    internal static double MeasureWidth(string text, double fontSize, bool bold, bool italic = false)
    {
        var table = (bold, italic) switch
        {
            (true,  _)     => TimesBoldWidths,
            (false, true)  => TimesItalicWidths,
            _              => HelveticaWidths,
        };
        double total = 0;
        foreach (char c in text)
        {
            int index = c - 32;
            int units = (index >= 0 && index < table.Length) ? table[index] : 500; // fallback
            total += units * fontSize / 1000.0;
        }
        return total;
    }
}
