namespace TerraPDF.Barcodes.QrCode;

/// <summary>
/// Builds the final QR module matrix: function patterns (finder, timing,
/// alignment), data-bit placement, mask selection, and format/version
/// information (ISO/IEC 18004 §6-8).
/// </summary>
internal static class QrMatrixBuilder
{
    private const int FormatInfoGenerator  = 0b10100110111;   // degree 10
    private const int FormatInfoXorMask    = 0b101010000010010;
    private const int VersionInfoGenerator = 0b1111100100101; // degree 12

    // 2-bit format indicator per QrErrorCorrectionLevel (L, M, Q, H order) — NOT the enum's own numeric order.
    private static readonly int[] LevelIndicator = [0b01, 0b00, 0b11, 0b10];

    private static readonly bool[] FinderLikePatternA =
        [true, false, true, true, true, false, true, false, false, false, false];
    private static readonly bool[] FinderLikePatternB =
        [false, false, false, false, true, false, true, true, true, false, true];

    internal static bool[,] Build(int version, QrErrorCorrectionLevel level, byte[] codewords)
    {
        int size = 4 * version + 17;
        var dark       = new bool[size, size];
        var isFunction = new bool[size, size];

        PlaceFinderPattern(dark, isFunction, size, 0, 0);
        PlaceFinderPattern(dark, isFunction, size, 0, size - 7);
        PlaceFinderPattern(dark, isFunction, size, size - 7, 0);
        PlaceTimingPatterns(dark, isFunction, size);
        PlaceAlignmentPatterns(dark, isFunction, size, version);

        ReserveFormatInfoAreas(isFunction, size);
        if (version >= 7) ReserveVersionInfoAreas(isFunction, size);

        PlaceDataBits(dark, isFunction, size, codewords);

        int mask = SelectBestMask(dark, isFunction, size);
        ApplyMask(dark, isFunction, size, mask);

        WriteFormatInfo(dark, size, level, mask);
        if (version >= 7) WriteVersionInfo(dark, size, version);

        return dark;
    }

    // -- Finder / timing / alignment patterns ---------------------------

    private static void PlaceFinderPattern(bool[,] dark, bool[,] isFunction, int size, int r0, int c0)
    {
        for (int dr = -1; dr <= 7; dr++)
        {
            for (int dc = -1; dc <= 7; dc++)
            {
                int r = r0 + dr, c = c0 + dc;
                if (r < 0 || r >= size || c < 0 || c >= size) continue;
                isFunction[r, c] = true;
                if (dr == -1 || dr == 7 || dc == -1 || dc == 7)
                    dark[r, c] = false; // separator
                else if (dr == 0 || dr == 6 || dc == 0 || dc == 6)
                    dark[r, c] = true; // outer 7x7 border
                else if (dr is >= 2 and <= 4 && dc is >= 2 and <= 4)
                    dark[r, c] = true; // inner 3x3
                else
                    dark[r, c] = false; // ring between border and inner square
            }
        }
    }

    private static void PlaceTimingPatterns(bool[,] dark, bool[,] isFunction, int size)
    {
        for (int i = 8; i <= size - 9; i++)
        {
            isFunction[6, i] = true; dark[6, i] = i % 2 == 0;
            isFunction[i, 6] = true; dark[i, 6] = i % 2 == 0;
        }
    }

    private static void PlaceAlignmentPatterns(bool[,] dark, bool[,] isFunction, int size, int version)
    {
        int[] coords = QrTables.GetAlignmentCoords(version);
        if (coords.Length == 0) return;
        int first = coords[0], last = coords[^1];

        foreach (int row in coords)
        {
            foreach (int col in coords)
            {
                if (row == first && col == first) continue; // top-left finder
                if (row == first && col == last) continue;  // top-right finder
                if (row == last && col == first) continue;  // bottom-left finder
                PlaceAlignmentPattern(dark, isFunction, row, col);
            }
        }
    }

    private static void PlaceAlignmentPattern(bool[,] dark, bool[,] isFunction, int centerRow, int centerCol)
    {
        for (int dr = -2; dr <= 2; dr++)
        {
            for (int dc = -2; dc <= 2; dc++)
            {
                int r = centerRow + dr, c = centerCol + dc;
                isFunction[r, c] = true;
                dark[r, c] = Math.Abs(dr) == 2 || Math.Abs(dc) == 2 || (dr == 0 && dc == 0);
            }
        }
    }

    // -- Data placement (zigzag, 2 columns wide) -------------------------

    private static void PlaceDataBits(bool[,] dark, bool[,] isFunction, int size, byte[] codewords)
    {
        int bitIndex  = 0;
        int totalBits = codewords.Length * 8;
        bool upward   = true;
        int col       = size - 1;

        while (col > 0)
        {
            if (col == 6) col--; // vertical timing pattern column is never used for data

            for (int i = 0; i < size; i++)
            {
                int row = upward ? size - 1 - i : i;
                for (int dc = 0; dc < 2; dc++)
                {
                    int c = col - dc;
                    if (isFunction[row, c]) continue;
                    bool bit = bitIndex < totalBits &&
                        ((codewords[bitIndex / 8] >> (7 - bitIndex % 8)) & 1) != 0;
                    dark[row, c] = bit;
                    bitIndex++;
                }
            }
            upward = !upward;
            col -= 2;
        }
    }

    // -- Masking ----------------------------------------------------------

    private static int SelectBestMask(bool[,] dark, bool[,] isFunction, int size)
    {
        int bestMask    = 0;
        int bestPenalty = int.MaxValue;
        for (int mask = 0; mask < 8; mask++)
        {
            var candidate = (bool[,])dark.Clone();
            ApplyMask(candidate, isFunction, size, mask);
            int penalty = ComputePenalty(candidate, size);
            if (penalty < bestPenalty)
            {
                bestPenalty = penalty;
                bestMask    = mask;
            }
        }
        return bestMask;
    }

    private static void ApplyMask(bool[,] dark, bool[,] isFunction, int size, int mask)
    {
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                if (!isFunction[r, c] && MaskCondition(mask, r, c))
                    dark[r, c] = !dark[r, c];
    }

    private static bool MaskCondition(int mask, int row, int col) => mask switch
    {
        0 => (row + col) % 2 == 0,
        1 => row % 2 == 0,
        2 => col % 3 == 0,
        3 => (row + col) % 3 == 0,
        4 => (row / 2 + col / 3) % 2 == 0,
        5 => row * col % 2 + row * col % 3 == 0,
        6 => (row * col % 2 + row * col % 3) % 2 == 0,
        7 => ((row + col) % 2 + row * col % 3) % 2 == 0,
        _ => false,
    };

    // -- Penalty scoring (ISO/IEC 18004 §7.8.3) ---------------------------

    private static int ComputePenalty(bool[,] m, int size) =>
        RunPenalty(m, size, rows: true) + RunPenalty(m, size, rows: false) +
        BlockPenalty(m, size) + FinderLikePenalty(m, size) + BalancePenalty(m, size);

    private static int RunPenalty(bool[,] m, int size, bool rows)
    {
        int penalty = 0;
        for (int i = 0; i < size; i++)
        {
            int   runLength = 1;
            bool? prev      = null;
            for (int j = 0; j < size; j++)
            {
                bool v = rows ? m[i, j] : m[j, i];
                if (prev.HasValue && v == prev.Value)
                {
                    runLength++;
                }
                else
                {
                    if (prev.HasValue && runLength >= 5) penalty += 3 + (runLength - 5);
                    runLength = 1;
                }
                prev = v;
            }
            if (runLength >= 5) penalty += 3 + (runLength - 5);
        }
        return penalty;
    }

    private static int BlockPenalty(bool[,] m, int size)
    {
        int penalty = 0;
        for (int r = 0; r < size - 1; r++)
            for (int c = 0; c < size - 1; c++)
            {
                bool v = m[r, c];
                if (m[r, c + 1] == v && m[r + 1, c] == v && m[r + 1, c + 1] == v)
                    penalty += 3;
            }
        return penalty;
    }

    private static int FinderLikePenalty(bool[,] m, int size)
    {
        int penalty = 0;
        for (int r = 0; r < size; r++)
            for (int c = 0; c <= size - 11; c++)
                if (MatchesFinderLike(m, r, c, horizontal: true)) penalty += 40;

        for (int c = 0; c < size; c++)
            for (int r = 0; r <= size - 11; r++)
                if (MatchesFinderLike(m, r, c, horizontal: false)) penalty += 40;

        return penalty;
    }

    private static bool MatchesFinderLike(bool[,] m, int r, int c, bool horizontal) =>
        MatchesPattern(m, r, c, horizontal, FinderLikePatternA) ||
        MatchesPattern(m, r, c, horizontal, FinderLikePatternB);

    private static bool MatchesPattern(bool[,] m, int r, int c, bool horizontal, bool[] pattern)
    {
        for (int i = 0; i < pattern.Length; i++)
        {
            bool v = horizontal ? m[r, c + i] : m[r + i, c];
            if (v != pattern[i]) return false;
        }
        return true;
    }

    private static int BalancePenalty(bool[,] m, int size)
    {
        int darkCount = 0;
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                if (m[r, c]) darkCount++;

        int percentDark = darkCount * 100 / (size * size);
        int lower = percentDark / 5 * 5;
        int upper = lower + 5;
        return Math.Min(Math.Abs(lower - 50) / 5, Math.Abs(upper - 50) / 5) * 10;
    }

    // -- Format / version information -------------------------------------

    private static (int Row, int Col)[] FormatInfoCoordsCopy1(int size) =>
    [
        (8, 0), (8, 1), (8, 2), (8, 3), (8, 4), (8, 5), (8, 7), (8, 8),
        (7, 8), (5, 8), (4, 8), (3, 8), (2, 8), (1, 8), (0, 8),
    ];

    private static (int Row, int Col)[] FormatInfoCoordsCopy2(int size) =>
    [
        (size - 1, 8), (size - 2, 8), (size - 3, 8), (size - 4, 8), (size - 5, 8), (size - 6, 8), (size - 7, 8), (size - 8, 8),
        (8, size - 7), (8, size - 6), (8, size - 5), (8, size - 4), (8, size - 3), (8, size - 2), (8, size - 1),
    ];

    private static void ReserveFormatInfoAreas(bool[,] isFunction, int size)
    {
        foreach ((int r, int c) in FormatInfoCoordsCopy1(size)) isFunction[r, c] = true;
        foreach ((int r, int c) in FormatInfoCoordsCopy2(size)) isFunction[r, c] = true;
        isFunction[8, size - 8] = true; // dark module
    }

    private static void ReserveVersionInfoAreas(bool[,] isFunction, int size)
    {
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 6; c++)
            {
                isFunction[size - 11 + r, c] = true; // block A (bottom-left)
                isFunction[c, size - 11 + r] = true; // block B (top-right)
            }
    }

    /// <summary>
    /// Computes the 15-bit format information value (2-bit EC level + 3-bit mask
    /// + 10-bit BCH(15,5) remainder, XOR-masked per ISO/IEC 18004 §8.9). Exposed
    /// internally so tests can independently verify the BCH remainder is valid
    /// without needing to know module placement coordinates.
    /// </summary>
    internal static int ComputeFormatInfoBits(QrErrorCorrectionLevel level, int mask)
    {
        int data = LevelIndicator[(int)level] << 3 | mask;
        int bch  = ComputeBchRemainder(data, FormatInfoGenerator, 10);
        return (data << 10 | bch) ^ FormatInfoXorMask;
    }

    /// <summary>
    /// Computes the 18-bit version information value (6-bit version + 12-bit
    /// BCH(18,6) remainder, ISO/IEC 18004 §8.10). Exposed internally for the
    /// same reason as <see cref="ComputeFormatInfoBits"/>.
    /// </summary>
    internal static int ComputeVersionInfoBits(int version)
    {
        int bch = ComputeBchRemainder(version, VersionInfoGenerator, 12);
        return version << 12 | bch;
    }

    private static void WriteFormatInfo(bool[,] dark, int size, QrErrorCorrectionLevel level, int mask)
    {
        int combined = ComputeFormatInfoBits(level, mask);

        var c1 = FormatInfoCoordsCopy1(size);
        var c2 = FormatInfoCoordsCopy2(size);
        for (int i = 0; i < 15; i++)
        {
            bool bit = (combined >> (14 - i) & 1) != 0;
            dark[c1[i].Row, c1[i].Col] = bit;
            dark[c2[i].Row, c2[i].Col] = bit;
        }
        dark[8, size - 8] = true; // dark module
    }

    private static void WriteVersionInfo(bool[,] dark, int size, int version)
    {
        int combined = ComputeVersionInfoBits(version);

        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 6; c++)
            {
                bool bit = (combined >> (c * 3 + r) & 1) != 0;
                dark[size - 11 + r, c] = bit; // block A
                dark[c, size - 11 + r] = bit; // block B
            }
    }

    private static int ComputeBchRemainder(int data, int generator, int eccBits)
    {
        int value             = data << eccBits;
        int generatorBitLength = BitLength(generator);
        while (BitLength(value) >= generatorBitLength)
            value ^= generator << (BitLength(value) - generatorBitLength);
        return value;
    }

    private static int BitLength(int value)
    {
        int len = 0;
        while (value != 0) { value >>= 1; len++; }
        return len;
    }
}
