namespace TerraPDF.Drawing.TrueType;

/// <summary>
/// Maps already-reordered Devanagari codepoints (see <see cref="DevanagariReordering"/>)
/// to glyph IDs, substituting conjunct-forming ligatures the font itself defines via
/// GSUB (parsed in <see cref="TrueTypeFont.TryGetDevanagariLigature"/>) — e.g. स्व draws
/// as its font-designed half-form + full glyph pair instead of three separate glyphs
/// with a visible ् mark. This is a plain greedy longest-match glyph-sequence scan, the
/// same mechanism a real shaper uses to apply a GSUB Type 4 (Ligature Substitution)
/// lookup — no notion of "cluster boundaries" is needed here: the font's own rules
/// already only ever start at a consonant glyph, so non-matching positions (vowel
/// signs, unmatched consonants) simply fall through to a plain 1:1 glyph unchanged.
/// <para>
/// र-conjuncts are covered for the two GSUB-ligature-modeled cases: reph (र् at a
/// cluster's start, once <see cref="DevanagariReordering"/> has moved it to the
/// cluster's end) collapses via <c>rphf</c>, and below/post-base 'ra' (क्र, त्र,
/// ष्ट्र, …) collapses via <c>rkrf</c> — both matched here exactly like
/// <c>half</c>/<c>akhn</c>/<c>cjct</c>, no special-casing needed. <c>blwf</c> (other,
/// non-र below-base forms) uses contextual GSUB lookups this reader doesn't parse, so
/// those still draw as separate glyphs.
/// </para>
/// </summary>
internal static class DevanagariConjuncts
{
    /// <summary>
    /// Maps <paramref name="codepoints"/> to (glyph ID, representative codepoint) pairs.
    /// A ligature-substituted glyph is paired with the <em>first</em> codepoint of the
    /// sequence it replaced, for <c>/ToUnicode</c> purposes — an accepted approximation
    /// (copy/paste won't recover the full original sequence), the same tradeoff already
    /// made by <see cref="DevanagariReordering"/> for matra placement.
    /// </summary>
    internal static List<(ushort GlyphId, int Codepoint)> MapToGlyphs(IReadOnlyList<int> codepoints, TrueTypeFont font)
    {
        var result = new List<(ushort, int)>(codepoints.Count);

        // Fast path: no virama at all means no possible conjunct in this text.
        bool hasVirama = false;
        for (int i = 0; i < codepoints.Count; i++)
        {
            if (codepoints[i] == 0x094D) { hasVirama = true; break; }
        }
        if (!hasVirama)
        {
            foreach (int cp in codepoints)
                result.Add((font.GetGlyphId(cp), cp));
            return result;
        }

        var rawGlyphs = new ushort[codepoints.Count];
        for (int i = 0; i < codepoints.Count; i++)
            rawGlyphs[i] = font.GetGlyphId(codepoints[i]);

        int pos = 0;
        while (pos < codepoints.Count)
        {
            if (font.TryGetDevanagariLigature(rawGlyphs.AsSpan(pos), out ushort ligatureGlyphId, out int consumed))
            {
                result.Add((ligatureGlyphId, codepoints[pos]));
                pos += consumed;
            }
            else
            {
                result.Add((rawGlyphs[pos], codepoints[pos]));
                pos += 1;
            }
        }
        return result;
    }
}
