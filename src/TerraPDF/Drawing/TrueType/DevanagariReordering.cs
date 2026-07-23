namespace TerraPDF.Drawing.TrueType;

/// <summary>
/// Decodes text to Unicode scalar values and reorders two things whose
/// <em>visual</em> position differs from where they're stored in Unicode text:
/// <list type="bullet">
/// <item>ि (<c>U+093F</c>, VOWEL SIGN I) is written after its consonant but
/// must render before it.</item>
/// <item>Reph — a cluster-initial र् (Ra+Virama immediately followed by more
/// consonants, e.g. र्म in धर्म) doesn't form a left-side conjunct; it's a
/// small hook rendered above the <em>end</em> of the cluster it attaches to,
/// so the र्  pair is moved there. This only reorders codepoints — turning
/// the trailing र् into the actual reph glyph is <c>TrueTypeFont</c>'s
/// <c>rphf</c>-derived ligature data (see <c>TrueTypeFont.Gsub.cs</c>),
/// applied afterward by <see cref="DevanagariConjuncts"/>.</item>
/// </list>
/// <para>
/// This is plain character reordering, not a general OpenType shaping engine
/// — no GPOS mark positioning, no Indic reordering beyond these two specific,
/// verified cases. See docs/custom-fonts.md's "Known limitations".
/// </para>
/// <para>
/// Used by both <see cref="CustomFontVariant.MeasureWidth"/> and
/// <see cref="PdfPage.EncodeIdentityHHex"/> — they must reorder identically,
/// otherwise <c>TextBlock</c>'s word-wrap (measured) and glyph drawing
/// (drawn) would disagree.
/// </para>
/// </summary>
internal static class DevanagariReordering
{
    private const int VowelSignI = 0x093F;
    private const int Virama     = 0x094D;
    private const int Ra         = 0x0930;

    /// <summary>True for the 33 standard Devanagari consonants and their nukta forms.</summary>
    private static bool IsConsonant(int cp) =>
        (cp >= 0x0915 && cp <= 0x0939) || (cp >= 0x0958 && cp <= 0x095F);

    /// <summary>
    /// Decodes <paramref name="text"/> into Unicode scalar values (surrogate pairs
    /// kept intact), moves each ि to just before the consonant cluster —
    /// a single consonant, or a conjunct chain of <c>Consonant (Virama Consonant)*</c>
    /// — that it attaches to, and moves a cluster-initial reph (र्, when
    /// followed by further consonants in the same cluster) to the end of that
    /// cluster. Text with neither ि nor a reph pattern (the common case,
    /// including all non-Devanagari text) is decoded but not otherwise altered.
    /// </summary>
    internal static List<int> DecodeAndReorder(string text)
    {
        var codepoints = new List<int>(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                codepoints.Add(char.ConvertToUtf32(text[i], text[i + 1]));
                i += 2;
            }
            else
            {
                codepoints.Add(text[i]);
                i += 1;
            }
        }

        bool hasVowelSignI = codepoints.Contains(VowelSignI);
        bool hasVirama     = codepoints.Contains(Virama);
        if (!hasVowelSignI && !hasVirama)
            return codepoints;

        var output = new List<int>(codepoints.Count);
        int n = codepoints.Count;
        i = 0;
        while (i < n)
        {
            int c = codepoints[i];
            if (IsConsonant(c))
            {
                var chain = new List<int> { c };
                i++;
                while (i + 1 < n && codepoints[i] == Virama && IsConsonant(codepoints[i + 1]))
                {
                    chain.Add(codepoints[i]);
                    chain.Add(codepoints[i + 1]);
                    i += 2;
                }

                // Reph: chain opens with र + halant + at least one more consonant
                // -> र् doesn't conjunct leftward here; move it to the cluster's end.
                if (chain.Count >= 3 && chain[0] == Ra && chain[1] == Virama)
                {
                    chain.RemoveRange(0, 2);
                    chain.Add(Ra);
                    chain.Add(Virama);
                }

                int clusterStart = output.Count;
                output.AddRange(chain);

                if (i < n && codepoints[i] == VowelSignI)
                {
                    output.Insert(clusterStart, codepoints[i]);
                    i++;
                }
            }
            else
            {
                output.Add(c);
                i++;
            }
        }
        return output;
    }
}
