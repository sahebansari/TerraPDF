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

    // Finder-like 1:1:3:1:1 patterns with four light modules on one side (§7.8.3, rule 3).
    private static readonly byte[] FinderLikePatternA = [1, 0, 1, 1, 1, 0, 1, 0, 0, 0, 0];
    private static readonly byte[] FinderLikePatternB = [0, 0, 0, 0, 1, 0, 1, 1, 1, 0, 1];

    // The matrix is built on flat row-major arrays (index = row * size + col): one byte per
    // module (1 = dark) and one flag per function module. Mask selection flips the data
    // modules in place, scores them and flips them back, so no copy of the matrix is made.

    internal static bool[,] Build(int version, QrErrorCorrectionLevel level, byte[] codewords)
    {
        int size = 4 * version + 17;
        var dark       = new byte[size * size];
        var isFunction = new bool[size * size];

        PlaceFinderPattern(dark, isFunction, size, 0, 0);
        PlaceFinderPattern(dark, isFunction, size, 0, size - 7);
        PlaceFinderPattern(dark, isFunction, size, size - 7, 0);
        PlaceTimingPatterns(dark, isFunction, size);
        PlaceAlignmentPatterns(dark, isFunction, size, version);

        ReserveFormatInfoAreas(isFunction, size);
        if (version >= 7) ReserveVersionInfoAreas(isFunction, size);

        PlaceDataBits(dark, isFunction, size, codewords);

        var flips = new byte[size * size];
        int mask = SelectBestMask(dark, isFunction, flips, size);
        BuildMaskFlips(flips, isFunction, size, mask);
        Xor(dark, flips);

        WriteFormatInfo(dark, size, level, mask);
        if (version >= 7) WriteVersionInfo(dark, size, version);

        var result = new bool[size, size];
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                result[r, c] = dark[r * size + c] != 0;
        return result;
    }

    // -- Finder / timing / alignment patterns ---------------------------

    private static void PlaceFinderPattern(byte[] dark, bool[] isFunction, int size, int r0, int c0)
    {
        for (int dr = -1; dr <= 7; dr++)
        {
            for (int dc = -1; dc <= 7; dc++)
            {
                int r = r0 + dr, c = c0 + dc;
                if (r < 0 || r >= size || c < 0 || c >= size) continue;
                int i = r * size + c;
                isFunction[i] = true;
                if (dr == -1 || dr == 7 || dc == -1 || dc == 7)
                    dark[i] = 0; // separator
                else if (dr == 0 || dr == 6 || dc == 0 || dc == 6)
                    dark[i] = 1; // outer 7x7 border
                else if (dr is >= 2 and <= 4 && dc is >= 2 and <= 4)
                    dark[i] = 1; // inner 3x3
                else
                    dark[i] = 0; // ring between border and inner square
            }
        }
    }

    private static void PlaceTimingPatterns(byte[] dark, bool[] isFunction, int size)
    {
        for (int i = 8; i <= size - 9; i++)
        {
            byte bit = (byte)(i % 2 == 0 ? 1 : 0);
            isFunction[6 * size + i] = true; dark[6 * size + i] = bit;
            isFunction[i * size + 6] = true; dark[i * size + 6] = bit;
        }
    }

    private static void PlaceAlignmentPatterns(byte[] dark, bool[] isFunction, int size, int version)
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
                PlaceAlignmentPattern(dark, isFunction, size, row, col);
            }
        }
    }

    private static void PlaceAlignmentPattern(byte[] dark, bool[] isFunction, int size, int centerRow, int centerCol)
    {
        for (int dr = -2; dr <= 2; dr++)
        {
            for (int dc = -2; dc <= 2; dc++)
            {
                int i = (centerRow + dr) * size + centerCol + dc;
                isFunction[i] = true;
                dark[i] = (byte)(Math.Abs(dr) == 2 || Math.Abs(dc) == 2 || (dr == 0 && dc == 0) ? 1 : 0);
            }
        }
    }

    // -- Data placement (zigzag, 2 columns wide) -------------------------

    private static void PlaceDataBits(byte[] dark, bool[] isFunction, int size, byte[] codewords)
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
                    int idx = row * size + col - dc;
                    if (isFunction[idx]) continue;
                    bool bit = bitIndex < totalBits &&
                        ((codewords[bitIndex / 8] >> (7 - bitIndex % 8)) & 1) != 0;
                    dark[idx] = (byte)(bit ? 1 : 0);
                    bitIndex++;
                }
            }
            upward = !upward;
            col -= 2;
        }
    }

    // -- Masking ----------------------------------------------------------

    private static int SelectBestMask(byte[] dark, bool[] isFunction, byte[] flips, int size)
    {
        int bestMask    = 0;
        int bestPenalty = int.MaxValue;
        for (int mask = 0; mask < 8; mask++)
        {
            BuildMaskFlips(flips, isFunction, size, mask);
            Xor(dark, flips);                          // apply the mask …
            int penalty = ComputePenalty(dark, size);
            Xor(dark, flips);                          // … and undo it (XOR is its own inverse)
            if (penalty < bestPenalty)
            {
                bestPenalty = penalty;
                bestMask    = mask;
            }
        }
        return bestMask;
    }

    /// <summary>Sets <paramref name="flips"/> to 1 for every data module the mask inverts.</summary>
    /// <remarks>
    /// The eight mask conditions (ISO/IEC 18004 §7.8.2), for row r and column c:
    /// 0: (r + c) mod 2 = 0; 1: r mod 2 = 0; 2: c mod 3 = 0; 3: (r + c) mod 3 = 0;
    /// 4: (⌊r/2⌋ + ⌊c/3⌋) mod 2 = 0; 5: (r·c) mod 2 + (r·c) mod 3 = 0;
    /// 6: ((r·c) mod 2 + (r·c) mod 3) mod 2 = 0; 7: ((r + c) mod 2 + (r·c) mod 3) mod 2 = 0.
    /// They are evaluated from per-row and per-column residues instead of dividing for
    /// every module: (r + c) mod 3 = (r mod 3 + c mod 3) mod 3,
    /// (r·c) mod 3 = (r mod 3)·(c mod 3) mod 3, and r·c is odd exactly when both are odd.
    /// </remarks>
    private static void BuildMaskFlips(byte[] flips, bool[] isFunction, int size, int mask)
    {
        Span<int> colMod3 = stackalloc int[size];
        Span<int> colDiv3 = stackalloc int[size];
        for (int c = 0; c < size; c++) { colMod3[c] = c % 3; colDiv3[c] = c / 3 & 1; }

        for (int r = 0, i = 0; r < size; r++)
        {
            int r2 = r & 1, r3 = r % 3, rDiv2 = r / 2 & 1;
            for (int c = 0; c < size; c++, i++)
            {
                if (isFunction[i]) { flips[i] = 0; continue; }
                int c2 = c & 1, c3 = colMod3[c];
                bool flip = mask switch
                {
                    0 => (r2 ^ c2) == 0,
                    1 => r2 == 0,
                    2 => c3 == 0,
                    3 => (r3 + c3) % 3 == 0,
                    4 => (rDiv2 ^ colDiv3[c]) == 0,
                    5 => (r2 & c2) + r3 * c3 % 3 == 0,
                    6 => ((r2 & c2) + r3 * c3 % 3 & 1) == 0,
                    7 => ((r2 ^ c2) + r3 * c3 % 3 & 1) == 0,
                    _ => false,
                };
                flips[i] = (byte)(flip ? 1 : 0);
            }
        }
    }

    private static void Xor(byte[] dark, byte[] flips)
    {
        var d = dark.AsSpan();
        var f = flips.AsSpan(0, d.Length);
        int i = 0;
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            int width = System.Numerics.Vector<byte>.Count;
            for (; i <= d.Length - width; i += width)
                (new System.Numerics.Vector<byte>(d[i..]) ^ new System.Numerics.Vector<byte>(f[i..])).CopyTo(d[i..]);
        }
        for (; i < d.Length; i++) d[i] ^= f[i];
    }

    // -- Penalty scoring (ISO/IEC 18004 §7.8.3) ---------------------------
    //
    // Scored bit-parallel: every row and every column is packed into a 192-bit line
    // (a symbol is at most 177 modules wide), and each rule becomes a few shifts, ANDs and
    // population counts per line instead of a module-by-module scan.

    private static int ComputePenalty(byte[] m, int size)
    {
        Span<Line> rows = stackalloc Line[size];
        Span<Line> cols = stackalloc Line[size];
        for (int r = 0, i = 0; r < size; r++)
            for (int c = 0; c < size; c++, i++)
                if (m[i] != 0) { rows[r].Set(c); cols[c].Set(r); }

        Line valid2  = Line.LowBits(size - 1);   // positions c with c + 1 inside the line
        Line valid5  = Line.LowBits(size - 4);   // … with c + 4 inside
        Line valid11 = Line.LowBits(size - 10);  // … with c + 10 inside

        int penalty = 0, darkCount = 0;
        for (int i = 0; i < size; i++)
        {
            penalty += RunPenalty(rows[i], valid2, valid5) + RunPenalty(cols[i], valid2, valid5);
            penalty += 40 * (FinderLikeCount(rows[i], valid11) + FinderLikeCount(cols[i], valid11));
            if (i < size - 1) penalty += 3 * BlockCount(rows[i], rows[i + 1], valid2);
            darkCount += rows[i].PopCount();
        }
        return penalty + BalancePenalty(darkCount, size * size);
    }

    /// <summary>
    /// Rule 1: every run of L ≥ 5 same-colour modules costs 3 + (L − 5) = L − 2, which is
    /// the number of 5-module windows inside the run (L − 4) plus 2 for the run itself.
    /// </summary>
    private static int RunPenalty(Line x, Line valid2, Line valid5)
    {
        Line same = ~(x ^ x.ShiftRight(1)) & valid2;                 // module c equals c + 1
        Line five = same & same.ShiftRight(1) & same.ShiftRight(2) & same.ShiftRight(3) & valid5;
        Line runStarts = five & ~five.ShiftLeft1();                  // first window of each run
        return five.PopCount() + 2 * runStarts.PopCount();
    }

    /// <summary>Rule 2: 2×2 blocks of one colour, for the rows <paramref name="x"/> and <paramref name="y"/>.</summary>
    private static int BlockCount(Line x, Line y, Line valid2) =>
        (~(x ^ x.ShiftRight(1)) & ~(y ^ y.ShiftRight(1)) & ~(x ^ y) & valid2).PopCount();

    /// <summary>Rule 3: windows matching either finder-like pattern.</summary>
    private static int FinderLikeCount(Line x, Line valid11)
    {
        Line a = valid11, b = valid11;
        for (int i = 0; i < 11; i++)
        {
            Line shifted = i == 0 ? x : x.ShiftRight(i);
            a &= FinderLikePatternA[i] != 0 ? shifted : ~shifted;
            b &= FinderLikePatternB[i] != 0 ? shifted : ~shifted;
        }
        return (a | b).PopCount();
    }

    /// <summary>Rule 4: proportion of dark modules away from 50%.</summary>
    private static int BalancePenalty(int darkCount, int total)
    {
        int percentDark = darkCount * 100 / total;
        int lower = percentDark / 5 * 5;
        int upper = lower + 5;
        return Math.Min(Math.Abs(lower - 50) / 5, Math.Abs(upper - 50) / 5) * 10;
    }

    /// <summary>A row or column of up to 192 modules, bit c = module c (1 = dark).</summary>
    private struct Line
    {
        private ulong _a, _b, _c;   // bits 0–63, 64–127, 128–191

        internal void Set(int bit)
        {
            if (bit < 64) _a |= 1UL << bit;
            else if (bit < 128) _b |= 1UL << (bit - 64);
            else _c |= 1UL << (bit - 128);
        }

        /// <summary>A line with bits 0 … <paramref name="count"/> − 1 set.</summary>
        internal static Line LowBits(int count)
        {
            static ulong Word(int n) => n <= 0 ? 0 : n >= 64 ? ulong.MaxValue : (1UL << n) - 1;
            return new Line { _a = Word(count), _b = Word(count - 64), _c = Word(count - 128) };
        }

        /// <summary>Bit c of the result is bit c + <paramref name="k"/> of this line (0 &lt; k &lt; 64).</summary>
        internal readonly Line ShiftRight(int k) => new()
        {
            _a = (_a >> k) | (_b << (64 - k)),
            _b = (_b >> k) | (_c << (64 - k)),
            _c = _c >> k,
        };

        /// <summary>Bit c of the result is bit c − 1 of this line.</summary>
        internal readonly Line ShiftLeft1() => new()
        {
            _a = _a << 1,
            _b = (_b << 1) | (_a >> 63),
            _c = (_c << 1) | (_b >> 63),
        };

        internal readonly int PopCount() =>
            System.Numerics.BitOperations.PopCount(_a) + System.Numerics.BitOperations.PopCount(_b) +
            System.Numerics.BitOperations.PopCount(_c);

        public static Line operator &(Line x, Line y) => new() { _a = x._a & y._a, _b = x._b & y._b, _c = x._c & y._c };
        public static Line operator |(Line x, Line y) => new() { _a = x._a | y._a, _b = x._b | y._b, _c = x._c | y._c };
        public static Line operator ^(Line x, Line y) => new() { _a = x._a ^ y._a, _b = x._b ^ y._b, _c = x._c ^ y._c };
        public static Line operator ~(Line x) => new() { _a = ~x._a, _b = ~x._b, _c = ~x._c };
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

    private static void ReserveFormatInfoAreas(bool[] isFunction, int size)
    {
        foreach ((int r, int c) in FormatInfoCoordsCopy1(size)) isFunction[r * size + c] = true;
        foreach ((int r, int c) in FormatInfoCoordsCopy2(size)) isFunction[r * size + c] = true;
        isFunction[8 * size + size - 8] = true; // dark module
    }

    private static void ReserveVersionInfoAreas(bool[] isFunction, int size)
    {
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 6; c++)
            {
                isFunction[(size - 11 + r) * size + c] = true; // block A (bottom-left)
                isFunction[c * size + size - 11 + r] = true; // block B (top-right)
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

    private static void WriteFormatInfo(byte[] dark, int size, QrErrorCorrectionLevel level, int mask)
    {
        int combined = ComputeFormatInfoBits(level, mask);

        var c1 = FormatInfoCoordsCopy1(size);
        var c2 = FormatInfoCoordsCopy2(size);
        for (int i = 0; i < 15; i++)
        {
            byte bit = (byte)(combined >> (14 - i) & 1);
            dark[c1[i].Row * size + c1[i].Col] = bit;
            dark[c2[i].Row * size + c2[i].Col] = bit;
        }
        dark[8 * size + size - 8] = 1; // dark module
    }

    private static void WriteVersionInfo(byte[] dark, int size, int version)
    {
        int combined = ComputeVersionInfoBits(version);

        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 6; c++)
            {
                byte bit = (byte)(combined >> (c * 3 + r) & 1);
                dark[(size - 11 + r) * size + c] = bit; // block A
                dark[c * size + size - 11 + r] = bit; // block B
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
