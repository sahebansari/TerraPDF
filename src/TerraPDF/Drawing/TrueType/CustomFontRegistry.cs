using System.Collections.Concurrent;

namespace TerraPDF.Drawing.TrueType;

/// <summary>
/// Process-wide registry of custom (embedded) fonts, backing the public
/// <see cref="TerraPDF.Helpers.FontFamily"/> API. Font files are parsed once and cached —
/// safe to register once at startup and reuse across every document rendered afterwards,
/// including concurrently across requests in a long-lived server process.
/// </summary>
internal static class CustomFontRegistry
{
    private static readonly ConcurrentDictionary<(string Name, bool Bold, bool Italic), CustomFontVariant> _variants = new();

    internal static void Register(string familyName, byte[] fontFileBytes, bool bold, bool italic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyName);
        ArgumentNullException.ThrowIfNull(fontFileBytes);

        var font = TrueTypeFont.Parse(fontFileBytes);
        string key = NormalizeName(familyName);
        _variants[(key, bold, italic)] = new CustomFontVariant(familyName.Trim(), bold, italic, font);
    }

    /// <summary>
    /// Looks up a registered variant for <paramref name="familyName"/>. An exact
    /// (bold, italic) match is preferred; otherwise falls back to the closest
    /// registered variant in priority order (bold+italic → bold → italic → regular)
    /// rather than throwing, matching the library's existing graceful-fallback
    /// behaviour for unmappable glyphs and unresolved standard font names.
    /// </summary>
    internal static bool TryGet(string familyName, bool bold, bool italic, out CustomFontVariant? variant)
    {
        string key = NormalizeName(familyName);

        if (_variants.TryGetValue((key, bold, italic), out variant))
            return true;

        foreach (var (b, i) in FallbackOrder(bold, italic))
        {
            if (_variants.TryGetValue((key, b, i), out variant))
                return true;
        }

        variant = null;
        return false;
    }

    /// <summary>True if any variant is registered under <paramref name="familyName"/>, regardless of style.</summary>
    internal static bool IsRegisteredFamily(string familyName)
    {
        string key = NormalizeName(familyName);
        foreach (var (b, i) in FallbackOrder(false, false))
            if (_variants.ContainsKey((key, b, i)))
                return true;
        return _variants.ContainsKey((key, false, false));
    }

    private static IEnumerable<(bool, bool)> FallbackOrder(bool bold, bool italic)
    {
        // Try every combination, closest to the request first, without repeating it.
        (bool, bool)[] all = [(true, true), (true, false), (false, true), (false, false)];
        foreach (var combo in all)
            if (combo != (bold, italic))
                yield return combo;
    }

    private static string NormalizeName(string familyName) => familyName.Trim().ToLowerInvariant();
}
