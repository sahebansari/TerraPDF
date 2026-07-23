using System.Text;

namespace TerraPDF.Drawing.TrueType;

/// <summary>
/// Parses just enough of a font's <c>GSUB</c> table to extract Devanagari
/// conjunct-forming ligature rules: <c>half</c> (half-forms — verified, in
/// real fonts, to be modeled as a 2-component Consonant+Virama ligature
/// rather than a single-glyph substitution), <c>akhn</c> (the mandatory
/// क्ष/ज्ञ-style ligatures), <c>cjct</c> (additional common conjunct pairs
/// the script's default language system applies), <c>rphf</c> (Reph Form —
/// र्, i.e. Ra+Virama, collapses to the reph glyph: <c>[Ra, Virama] → reph</c>)
/// and <c>rkrf</c> (Rakar Form — a consonant conjunct whose second member is
/// र collapses directly to a single below/post-base glyph:
/// <c>[Consonant, Virama, Ra] → ligature</c>, e.g. क्र, त्र, ष्ट्र). All five
/// use GSUB <c>LookupType</c> 4 (Ligature Substitution) in every font this
/// was verified against.
/// <para>
/// र्'s reph form additionally needs the *codepoint order* corrected before
/// this substitution can apply — see <see cref="DevanagariReordering"/> —
/// since a cluster-initial र् must render after the cluster it attaches to,
/// not before it. <c>rkrf</c> needs no reordering: a consonant+virama+र
/// sequence is already in the right codepoint order, just not yet the right
/// glyph. <c>blwf</c> (below-base forms for other, non-र subjoined
/// consonants) uses contextual/chaining substitution (<c>LookupType</c> 6)
/// and is intentionally not parsed here — see docs/custom-fonts.md's
/// "Known limitations".
/// </para>
/// <para>
/// Parsing is defensive end-to-end: a missing <c>GSUB</c> table, no Devanagari
/// script entry, none of these features, or any unexpected/malformed
/// structure all simply yield no ligature data — this must never prevent a
/// font from loading, since it's purely a rendering enhancement.
/// </para>
/// </summary>
internal sealed partial class TrueTypeFont
{
    private static readonly string[] DevanagariLigatureFeatures = ["half", "akhn", "cjct", "rphf", "rkrf"];

    private static Dictionary<ushort, List<(ushort[] Tail, ushort Output)>> ParseDevanagariLigatures(
        byte[] data, Dictionary<string, (uint Offset, uint Length)> tables)
    {
        var result = new Dictionary<ushort, List<(ushort[], ushort)>>();
        if (!tables.TryGetValue("GSUB", out var gsubTable))
            return result;

        try
        {
            int gsub = (int)gsubTable.Offset;
            int scriptListPos  = gsub + ReadU16(data, gsub + 4);
            int featureListPos = gsub + ReadU16(data, gsub + 6);
            int lookupListPos  = gsub + ReadU16(data, gsub + 8);

            int scriptPos = FindDevanagariScript(data, scriptListPos);
            if (scriptPos < 0)
                return result;

            var featureIndices = ReadDefaultLangSysFeatureIndices(data, scriptPos);
            var lookupIndices  = CollectLigatureLookupIndices(data, featureListPos, featureIndices);

            foreach (int lookupIndex in lookupIndices)
                ParseLigatureLookup(data, lookupListPos, lookupIndex, result);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Any out-of-bounds read anywhere in this best-effort walk (malformed or
            // unexpectedly-structured GSUB data) — discard whatever was collected and
            // fall back to no ligature data rather than fail font parsing entirely.
            return new Dictionary<ushort, List<(ushort[], ushort)>>();
        }

        foreach (var candidates in result.Values)
            candidates.Sort((a, b) => b.Item1.Length.CompareTo(a.Item1.Length)); // longest match first

        return result;
    }

    /// <summary>Returns the offset of the 'dev2' script table if present, else 'deva', else -1.</summary>
    private static int FindDevanagariScript(byte[] data, int scriptListPos)
    {
        ushort scriptCount = ReadU16(data, scriptListPos);
        int dev1Offset = -1, dev2Offset = -1;
        for (int i = 0; i < scriptCount; i++)
        {
            int recPos = scriptListPos + 2 + i * 6;
            string tag = Encoding.ASCII.GetString(data, recPos, 4);
            int off = scriptListPos + ReadU16(data, recPos + 4);
            if (tag == "dev2") dev2Offset = off;
            else if (tag == "deva") dev1Offset = off;
        }
        return dev2Offset >= 0 ? dev2Offset : dev1Offset;
    }

    private static List<int> ReadDefaultLangSysFeatureIndices(byte[] data, int scriptPos)
    {
        ushort defaultLangSysOff = ReadU16(data, scriptPos);
        if (defaultLangSysOff == 0)
            return [];

        int langSysPos = scriptPos + defaultLangSysOff;
        ushort featureIndexCount = ReadU16(data, langSysPos + 4);
        var indices = new List<int>(featureIndexCount);
        for (int i = 0; i < featureIndexCount; i++)
            indices.Add(ReadU16(data, langSysPos + 6 + i * 2));
        return indices;
    }

    private static List<int> CollectLigatureLookupIndices(byte[] data, int featureListPos, List<int> featureIndices)
    {
        var lookups = new List<int>();
        foreach (int featureIndex in featureIndices)
        {
            int recPos = featureListPos + 2 + featureIndex * 6;
            string tag = Encoding.ASCII.GetString(data, recPos, 4);
            if (Array.IndexOf(DevanagariLigatureFeatures, tag) < 0)
                continue;

            int featPos = featureListPos + ReadU16(data, recPos + 4);
            ushort lookupCount = ReadU16(data, featPos + 2);
            for (int i = 0; i < lookupCount; i++)
                lookups.Add(ReadU16(data, featPos + 4 + i * 2));
        }
        return lookups;
    }

    private static void ParseLigatureLookup(byte[] data, int lookupListPos, int lookupIndex,
        Dictionary<ushort, List<(ushort[], ushort)>> result)
    {
        int lookupPos = lookupListPos + ReadU16(data, lookupListPos + 2 + lookupIndex * 2);
        ushort lookupType     = ReadU16(data, lookupPos);
        ushort subtableCount  = ReadU16(data, lookupPos + 4);

        for (int s = 0; s < subtableCount; s++)
        {
            int subPos = lookupPos + ReadU16(data, lookupPos + 6 + s * 2);
            int effectiveType = lookupType;
            int effectivePos  = subPos;

            if (lookupType == 7) // Extension Substitution — unwrap to the real subtable
            {
                effectiveType = ReadU16(data, subPos + 2);
                effectivePos  = subPos + (int)ReadU32(data, subPos + 4);
            }

            if (effectiveType == 4) // Ligature Substitution
                ParseLigatureSubstSubtable(data, effectivePos, result);
            // Other lookup types (contextual/chaining — used by rphf/blwf/rkrf for
            // reph and below-base 'ra' forms) are intentionally not handled; that
            // glyph's cluster simply gets no ligature match and draws unsubstituted.
        }
    }

    private static void ParseLigatureSubstSubtable(byte[] data, int subPos,
        Dictionary<ushort, List<(ushort[], ushort)>> result)
    {
        ushort format = ReadU16(data, subPos);
        if (format != 1)
            return;

        int coverage = subPos + ReadU16(data, subPos + 2);
        var coveredGlyphs = ReadCoverageGlyphs(data, coverage);
        ushort ligSetCount = ReadU16(data, subPos + 4);

        for (int i = 0; i < ligSetCount && i < coveredGlyphs.Count; i++)
        {
            ushort firstGlyph = coveredGlyphs[i];
            int ligSetPos = subPos + ReadU16(data, subPos + 6 + i * 2);
            ushort ligCount = ReadU16(data, ligSetPos);

            for (int l = 0; l < ligCount; l++)
            {
                int ligPos = ligSetPos + ReadU16(data, ligSetPos + 2 + l * 2);
                ushort ligatureGlyph  = ReadU16(data, ligPos);
                ushort componentCount = ReadU16(data, ligPos + 2);
                var rest = new ushort[componentCount - 1];
                for (int c = 0; c < rest.Length; c++)
                    rest[c] = ReadU16(data, ligPos + 4 + c * 2);

                if (!result.TryGetValue(firstGlyph, out var candidates))
                    result[firstGlyph] = candidates = [];
                candidates.Add((rest, ligatureGlyph));
            }
        }
    }

    /// <summary>Returns the glyph IDs covered by a Coverage table (format 1 or 2), in coverage-index order.</summary>
    private static List<ushort> ReadCoverageGlyphs(byte[] data, int covPos)
    {
        ushort format = ReadU16(data, covPos);
        var glyphs = new List<ushort>();
        if (format == 1)
        {
            ushort glyphCount = ReadU16(data, covPos + 2);
            for (int i = 0; i < glyphCount; i++)
                glyphs.Add(ReadU16(data, covPos + 4 + i * 2));
        }
        else if (format == 2)
        {
            ushort rangeCount = ReadU16(data, covPos + 2);
            for (int i = 0; i < rangeCount; i++)
            {
                int rp = covPos + 4 + i * 6;
                ushort start = ReadU16(data, rp);
                ushort end   = ReadU16(data, rp + 2);
                for (int gid = start; gid <= end; gid++)
                    glyphs.Add((ushort)gid);
            }
        }
        return glyphs;
    }
}
