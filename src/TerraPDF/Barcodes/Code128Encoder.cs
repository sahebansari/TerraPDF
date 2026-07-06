namespace TerraPDF.Barcodes;

/// <summary>
/// Encodes text into a Code128 (Subset B) module pattern — a sequence of
/// dark/light bars with no dependency on rendering. Subset B covers printable
/// ASCII 0x20–0x7E (space through '~'), which is the common general-purpose
/// case (product codes, shipping references, arbitrary alphanumeric text).
/// </summary>
internal static class Code128Encoder
{
    // Value 106 is STOP, appended after the checksum symbol by Encode().
    private const int StartB    = 104;
    private const int Stop      = 106;
    private const int ModulusN  = 103;

    // Code128 symbol table indexed by value (0-102 data/function, 103-105 START A/B/C,
    // 106 STOP). Each entry is the bar/space element widths in modules, always starting
    // with a bar. All rows sum to 11 modules except STOP, which sums to 13.
    internal static readonly string[] Patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213", // 0-9
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132", // 10-19
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211", // 20-29
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313", // 30-39
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331", // 40-49
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111", // 50-59
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214", // 60-69
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111", // 70-79
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141", // 80-89
        "214121", "214211", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141", // 90-99
        "114131", "311141", "411131", // 100-102
        "211412", "211214", "211232", // 103-105: START A, START B, START C
        "2331112",                    // 106: STOP (13 modules)
    ];

    /// <summary>
    /// Builds the full dark/light module sequence (START B + data + checksum + STOP),
    /// not including any quiet-zone padding — callers add that when rendering.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// <paramref name="text"/> contains a character outside printable ASCII (0x20-0x7E),
    /// which Subset B cannot represent.
    /// </exception>
    internal static bool[] Encode(string text)
    {
        var values = new List<int> { StartB };
        foreach (char c in text)
        {
            if (c < 0x20 || c > 0x7E)
                throw new NotSupportedException(
                    $"Code128 barcode only supports printable ASCII characters (0x20-0x7E). " +
                    $"Character '{c}' (0x{(int)c:X2}) is not supported.");
            values.Add(c - 0x20);
        }

        int checksum = 0;
        for (int i = 0; i < values.Count; i++)
            checksum += values[i] * (i == 0 ? 1 : i);
        checksum %= ModulusN;

        values.Add(checksum);
        values.Add(Stop);

        var modules = new List<bool>();
        foreach (int value in values)
            AppendPattern(modules, Patterns[value]);
        return [.. modules];
    }

    private static void AppendPattern(List<bool> modules, string widths)
    {
        bool dark = true; // every symbol pattern starts with a bar
        foreach (char digit in widths)
        {
            int width = digit - '0';
            for (int i = 0; i < width; i++)
                modules.Add(dark);
            dark = !dark;
        }
    }
}
