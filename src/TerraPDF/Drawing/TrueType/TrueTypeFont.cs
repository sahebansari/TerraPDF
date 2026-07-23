using System.Buffers.Binary;

namespace TerraPDF.Drawing.TrueType;

/// <summary>
/// Parses just enough of an sfnt-wrapped TrueType-outline font (<c>.ttf</c>, or an
/// <c>.otf</c> that still carries a <c>glyf</c>/<c>loca</c> table) to embed it in a
/// PDF as a <c>CIDFontType2</c> composite font and measure text set in it.
/// <para>
/// No glyph-outline interpretation is performed — the whole file is embedded
/// verbatim (<see cref="RawData"/>), so only the tables needed for character-to-glyph
/// mapping, advance widths, and descriptor metrics are read: <c>head</c>, <c>maxp</c>,
/// <c>hhea</c>/<c>hmtx</c>, <c>cmap</c>, and (optionally) <c>OS/2</c>/<c>post</c>.
/// </para>
/// <para>
/// CFF-flavoured OpenType (<c>OTTO</c>) and TrueType Collections (<c>ttcf</c>) are
/// rejected with a clear <see cref="NotSupportedException"/> — a different embedding
/// path (<c>/FontFile3</c>, <c>CIDFontType0</c>) is needed for those and is not yet
/// implemented.
/// </para>
/// </summary>
internal sealed partial class TrueTypeFont
{
    /// <summary>The original, unmodified font file bytes — embedded as-is (no subsetting).</summary>
    internal byte[] RawData { get; }

    /// <summary>Design units per em (typically 1000 or 2048), from the <c>head</c> table.</summary>
    internal int UnitsPerEm { get; }

    /// <summary>Number of glyphs in the font, from the <c>maxp</c> table.</summary>
    internal int NumGlyphs { get; }

    /// <summary>Ascender, in 1000-unit glyph space (scaled from font units).</summary>
    internal double Ascent { get; }

    /// <summary>Descender, in 1000-unit glyph space (scaled from font units; typically negative).</summary>
    internal double Descent { get; }

    /// <summary>Capital-letter height, in 1000-unit glyph space. Falls back to <see cref="Ascent"/> when unavailable.</summary>
    internal double CapHeight { get; }

    /// <summary>Italic slant angle in degrees (0 for upright fonts), from the <c>post</c> table.</summary>
    internal double ItalicAngle { get; }

    /// <summary>Font bounding box [xMin, yMin, xMax, yMax] in 1000-unit glyph space.</summary>
    internal (double XMin, double YMin, double XMax, double YMax) FontBBox { get; }

    private readonly ushort[] _advanceWidths; // per glyph ID, in font design units
    private readonly List<(uint Start, uint End, uint StartGlyphId)> _cmapRanges; // sorted by Start, non-overlapping

    // Devanagari conjunct-forming ligature rules parsed from GSUB (see TrueTypeFont.Gsub.cs):
    // first component glyph -> candidate rules (remaining components, output glyph),
    // each list pre-sorted longest-remaining-first for greedy longest-match lookup.
    // Empty when the font has no GSUB table / no Devanagari script / none of the
    // relevant features — never populated with partial/incorrect data.
    private readonly Dictionary<ushort, List<(ushort[] Tail, ushort Output)>> _devanagariLigatures;

    private TrueTypeFont(
        byte[] rawData, int unitsPerEm, int numGlyphs,
        double ascent, double descent, double capHeight, double italicAngle,
        (double, double, double, double) fontBBox,
        ushort[] advanceWidths, List<(uint, uint, uint)> cmapRanges,
        Dictionary<ushort, List<(ushort[], ushort)>> devanagariLigatures)
    {
        RawData         = rawData;
        UnitsPerEm      = unitsPerEm;
        NumGlyphs       = numGlyphs;
        Ascent          = ascent;
        Descent         = descent;
        CapHeight       = capHeight;
        ItalicAngle     = italicAngle;
        FontBBox        = fontBBox;
        _advanceWidths  = advanceWidths;
        _cmapRanges     = cmapRanges;
        _devanagariLigatures = devanagariLigatures;
    }

    /// <summary>
    /// Returns the glyph ID mapped to <paramref name="codepoint"/> (a Unicode scalar
    /// value), or 0 (<c>.notdef</c>) if the font's <c>cmap</c> has no entry for it.
    /// </summary>
    internal ushort GetGlyphId(int codepoint)
    {
        // Binary search for the range containing codepoint.
        int lo = 0, hi = _cmapRanges.Count - 1;
        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            var (start, end, startGlyphId) = _cmapRanges[mid];
            if (codepoint < start) hi = mid - 1;
            else if (codepoint > end) lo = mid + 1;
            else return (ushort)(startGlyphId + (uint)(codepoint - start));
        }
        return 0;
    }

    /// <summary>Returns the advance width of <paramref name="glyphId"/> in 1000-unit glyph space.</summary>
    internal double GetAdvanceWidthInEm(ushort glyphId)
    {
        ushort raw = _advanceWidths.Length == 0
            ? (ushort)0
            : _advanceWidths[Math.Min(glyphId, _advanceWidths.Length - 1)];
        return raw * 1000.0 / UnitsPerEm;
    }

    /// <summary>
    /// Tries to match the longest Devanagari conjunct-forming ligature rule (from
    /// GSUB's <c>half</c>/<c>akhn</c>/<c>cjct</c> features — see <c>TrueTypeFont.Gsub.cs</c>)
    /// starting at <paramref name="glyphs"/>[0]. Returns <see langword="false"/> when this
    /// font has no rule for that starting glyph (including fonts with no Devanagari GSUB
    /// data at all) — callers fall back to drawing the glyphs individually.
    /// </summary>
    internal bool TryGetDevanagariLigature(ReadOnlySpan<ushort> glyphs, out ushort ligatureGlyphId, out int consumedCount)
    {
        ligatureGlyphId = 0;
        consumedCount   = 0;
        if (glyphs.Length == 0 || !_devanagariLigatures.TryGetValue(glyphs[0], out var candidates))
            return false;

        foreach (var (rest, output) in candidates) // pre-sorted longest-first
        {
            if (rest.Length + 1 > glyphs.Length) continue;
            bool match = true;
            for (int i = 0; i < rest.Length; i++)
            {
                if (glyphs[1 + i] != rest[i]) { match = false; break; }
            }
            if (!match) continue;
            ligatureGlyphId = output;
            consumedCount   = rest.Length + 1;
            return true;
        }
        return false;
    }

    // --------------------------------------------------------------
    //  Parsing
    // --------------------------------------------------------------

    /// <summary>
    /// Parses an sfnt font file. Throws <see cref="ArgumentException"/> for malformed
    /// or incomplete data, and <see cref="NotSupportedException"/> for recognised but
    /// unsupported font flavours (CFF/OTTO outlines, TrueType Collections).
    /// </summary>
    internal static TrueTypeFont Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 12)
            throw new ArgumentException("Not a valid font file: too short to contain an sfnt header.", nameof(data));

        uint sfntVersion = ReadU32(data, 0);
        if (sfntVersion == 0x4F54544Fu) // 'OTTO'
            throw new NotSupportedException(
                "This is a CFF-flavoured OpenType font (OTTO outlines). TerraPDF currently " +
                "only embeds TrueType-outline fonts (.ttf, or .otf with a 'glyf' table). " +
                "CFF/PostScript-outline embedding is not yet supported.");
        if (sfntVersion == 0x74746366u) // 'ttcf'
            throw new NotSupportedException(
                "TrueType Collection (.ttc) files are not supported. Extract and register a single font file.");
        if (sfntVersion != 0x00010000u && sfntVersion != 0x74727565u) // 1.0 or 'true'
            throw new ArgumentException("Not a valid TrueType font file: unrecognised sfnt version.", nameof(data));

        ushort numTables = ReadU16(data, 4);
        var tables = new Dictionary<string, (uint Offset, uint Length)>(numTables);
        int recordPos = 12;
        for (int i = 0; i < numTables; i++, recordPos += 16)
        {
            RequireLength(data, recordPos, 16, "table directory");
            string tag = System.Text.Encoding.ASCII.GetString(data, recordPos, 4);
            uint offset = ReadU32(data, recordPos + 8);
            uint length = ReadU32(data, recordPos + 12);
            tables[tag] = (offset, length);
        }

        (uint Offset, uint Length) Require(string tag)
        {
            if (!tables.TryGetValue(tag, out var t))
                throw new ArgumentException($"Not a valid TrueType font: missing required '{tag}' table.", nameof(data));
            return t;
        }

        if (!tables.ContainsKey("glyf") || !tables.ContainsKey("loca"))
            throw new NotSupportedException(
                "This font has no 'glyf'/'loca' tables, so it is not a TrueType-outline font " +
                "(likely CFF/PostScript outlines). CFF embedding is not yet supported.");

        // -- head: unitsPerEm, bounding box --------------------------------
        var head = Require("head");
        RequireLength(data, (int)head.Offset, 54, "head");
        int unitsPerEm = ReadU16(data, (int)head.Offset + 18);
        if (unitsPerEm == 0) unitsPerEm = 1000;
        short xMin = ReadI16(data, (int)head.Offset + 36);
        short yMin = ReadI16(data, (int)head.Offset + 38);
        short xMax = ReadI16(data, (int)head.Offset + 40);
        short yMax = ReadI16(data, (int)head.Offset + 42);
        double scale = 1000.0 / unitsPerEm;
        var fontBBox = (xMin * scale, yMin * scale, xMax * scale, yMax * scale);

        // -- maxp: glyph count ----------------------------------------------
        var maxp = Require("maxp");
        RequireLength(data, (int)maxp.Offset, 6, "maxp");
        int numGlyphs = ReadU16(data, (int)maxp.Offset + 4);

        // -- hhea + hmtx: advance widths --------------------------------------
        var hhea = Require("hhea");
        RequireLength(data, (int)hhea.Offset, 36, "hhea");
        int numHMetrics = ReadU16(data, (int)hhea.Offset + 34);
        short ascentUnits  = ReadI16(data, (int)hhea.Offset + 4);
        short descentUnits = ReadI16(data, (int)hhea.Offset + 6);

        var hmtx = Require("hmtx");
        var advanceWidths = new ushort[Math.Max(numGlyphs, 1)];
        {
            int pos = (int)hmtx.Offset;
            ushort lastAdvance = 0;
            int hCount = Math.Min(numHMetrics, numGlyphs);
            for (int g = 0; g < hCount; g++)
            {
                RequireLength(data, pos, 4, "hmtx");
                lastAdvance = ReadU16(data, pos);
                advanceWidths[g] = lastAdvance;
                pos += 4;
            }
            for (int g = hCount; g < numGlyphs; g++)
                advanceWidths[g] = lastAdvance;
        }

        // -- OS/2 (optional): ascent/descent/capHeight override --------------
        double ascent  = ascentUnits * scale;
        double descent = descentUnits * scale;
        double capHeight = ascent;
        if (tables.TryGetValue("OS/2", out var os2) && os2.Length >= 68)
        {
            ushort os2Version = ReadU16(data, (int)os2.Offset);
            short typoAscender  = ReadI16(data, (int)os2.Offset + 68);
            // sTypoAscender/Descender live at offset 68/70 only when the table is
            // long enough (all versions since OS/2 v0 include them).
            if (os2.Length >= 72)
            {
                short typoDescender = ReadI16(data, (int)os2.Offset + 70);
                ascent  = typoAscender  * scale;
                descent = typoDescender * scale;
            }
            if (os2Version >= 2 && os2.Length >= 90)
            {
                short sCapHeight = ReadI16(data, (int)os2.Offset + 88);
                if (sCapHeight != 0)
                    capHeight = sCapHeight * scale;
            }
        }

        // -- post (optional): italic angle -----------------------------------
        double italicAngle = 0;
        if (tables.TryGetValue("post", out var post) && post.Length >= 8)
        {
            int fixedAngle = ReadI32(data, (int)post.Offset + 4);
            italicAngle = fixedAngle / 65536.0;
        }

        // -- cmap: Unicode → glyph ID -----------------------------------------
        var cmap = Require("cmap");
        var cmapRanges = ParseCmap(data, (int)cmap.Offset);

        // -- GSUB (optional): Devanagari conjunct-forming ligature rules ------
        var devanagariLigatures = ParseDevanagariLigatures(data, tables);

        return new TrueTypeFont(data, unitsPerEm, numGlyphs, ascent, descent, capHeight,
            italicAngle, fontBBox, advanceWidths, cmapRanges, devanagariLigatures);
    }

    /// <summary>
    /// Selects the best available <c>cmap</c> subtable (preferring full-Unicode
    /// format 12, then BMP format 4) and returns its character ranges as a sorted,
    /// non-overlapping list of (start, end, startGlyphId) — bounded in size by
    /// construction (format 4 covers at most the BMP; format 12 groups are stored
    /// as ranges rather than expanded, so a crafted huge range costs one list entry,
    /// not one entry per codepoint).
    /// </summary>
    private static List<(uint, uint, uint)> ParseCmap(byte[] data, int cmapOffset)
    {
        RequireLength(data, cmapOffset, 4, "cmap");
        ushort numTables = ReadU16(data, cmapOffset + 2);

        int bestScore = -1;
        int bestSubtableOffset = -1;

        for (int i = 0; i < numTables; i++)
        {
            int recPos = cmapOffset + 4 + i * 8;
            RequireLength(data, recPos, 8, "cmap");
            ushort platformId = ReadU16(data, recPos);
            ushort encodingId = ReadU16(data, recPos + 2);
            uint subOffset    = ReadU32(data, recPos + 4);
            int absOffset     = cmapOffset + (int)subOffset;
            if (absOffset < 0 || absOffset + 2 > data.Length) continue;

            ushort format = ReadU16(data, absOffset);
            int score = format switch
            {
                12 when platformId == 3 && encodingId == 10 => 100,
                12 when platformId == 0 => 90,
                4  when platformId == 3 && encodingId == 1  => 50,
                4  when platformId == 0 => 40,
                4  => 20,
                12 => 30,
                _  => -1,
            };
            if (score > bestScore)
            {
                bestScore = score;
                bestSubtableOffset = absOffset;
            }
        }

        if (bestSubtableOffset < 0)
            throw new ArgumentException(
                "Not a valid TrueType font for embedding: no usable 'cmap' subtable (format 4 or 12) found.");

        ushort bestFormat = ReadU16(data, bestSubtableOffset);
        return bestFormat == 12
            ? ParseCmapFormat12(data, bestSubtableOffset)
            : ParseCmapFormat4(data, bestSubtableOffset);
    }

    private static List<(uint, uint, uint)> ParseCmapFormat4(byte[] data, int pos)
    {
        RequireLength(data, pos, 14, "cmap format 4");
        int segCountX2 = ReadU16(data, pos + 6);
        int segCount   = segCountX2 / 2;

        int endCodePos      = pos + 14;
        int startCodePos    = endCodePos + segCountX2 + 2; // +2 skips reservedPad
        int idDeltaPos      = startCodePos + segCountX2;
        int idRangeOffPos   = idDeltaPos + segCountX2;

        var ranges = new List<(uint, uint, uint)>(segCount);
        for (int s = 0; s < segCount; s++)
        {
            ushort endCode   = ReadU16(data, endCodePos + s * 2);
            ushort startCode = ReadU16(data, startCodePos + s * 2);
            short  idDelta   = ReadI16(data, idDeltaPos + s * 2);
            ushort idRangeOffset = ReadU16(data, idRangeOffPos + s * 2);

            if (startCode == 0xFFFF && endCode == 0xFFFF) continue; // terminator segment

            if (idRangeOffset == 0)
            {
                uint startGlyphId = (uint)(ushort)(startCode + idDelta);
                ranges.Add((startCode, endCode, startGlyphId));
            }
            else
            {
                // Indexed through glyphIdArray — expand one entry per code point.
                // Bounded: a format 4 segment can span at most 0..0xFFFF codes.
                int glyphIdArrayPos = idRangeOffPos + s * 2 + idRangeOffset;
                for (int c = startCode; c <= endCode; c++)
                {
                    int gPos = glyphIdArrayPos + (c - startCode) * 2;
                    if (gPos + 2 > data.Length) break;
                    ushort g = ReadU16(data, gPos);
                    if (g == 0) continue;
                    uint glyphId = (uint)(ushort)(g + idDelta);
                    ranges.Add(((uint)c, (uint)c, glyphId));
                    if (c == 0xFFFF) break; // avoid ushort wraparound
                }
            }
        }

        ranges.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return MergeAdjacentRanges(ranges);
    }

    private static List<(uint, uint, uint)> ParseCmapFormat12(byte[] data, int pos)
    {
        RequireLength(data, pos, 16, "cmap format 12");
        uint nGroups = ReadU32(data, pos + 12);
        var ranges = new List<(uint, uint, uint)>((int)Math.Min(nGroups, 100_000));

        int groupPos = pos + 16;
        for (uint g = 0; g < nGroups; g++, groupPos += 12)
        {
            RequireLength(data, groupPos, 12, "cmap format 12 group");
            uint startCharCode = ReadU32(data, groupPos);
            uint endCharCode   = ReadU32(data, groupPos + 4);
            uint startGlyphId  = ReadU32(data, groupPos + 8);
            ranges.Add((startCharCode, endCharCode, startGlyphId));
        }

        ranges.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return ranges;
    }

    /// <summary>Merges consecutive ranges where the glyph ID sequence continues unbroken.</summary>
    private static List<(uint, uint, uint)> MergeAdjacentRanges(List<(uint Start, uint End, uint StartGlyphId)> ranges)
    {
        if (ranges.Count == 0) return ranges;
        var merged = new List<(uint, uint, uint)> { ranges[0] };
        for (int i = 1; i < ranges.Count; i++)
        {
            var (prevStart, prevEnd, prevGid) = merged[^1];
            var (start, end, gid) = ranges[i];
            if (start == prevEnd + 1 && gid == prevGid + (prevEnd - prevStart + 1))
                merged[^1] = (prevStart, end, prevGid);
            else
                merged.Add((start, end, gid));
        }
        return merged;
    }

    // --------------------------------------------------------------
    //  Big-endian primitive readers
    // --------------------------------------------------------------

    private static void RequireLength(byte[] data, int offset, int length, string tableName)
    {
        if (offset < 0 || (long)offset + length > data.Length)
            throw new ArgumentException($"Not a valid TrueType font: '{tableName}' table is truncated.");
    }

    private static ushort ReadU16(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));

    private static short ReadI16(byte[] data, int offset) =>
        BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(offset, 2));

    private static uint ReadU32(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));

    private static int ReadI32(byte[] data, int offset) =>
        BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, 4));
}
